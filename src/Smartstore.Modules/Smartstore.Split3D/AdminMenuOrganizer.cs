using Smartstore.Collections;
using Smartstore.Core.Content.Menus;
using Smartstore.Web.Rendering.Builders;

namespace Smartstore.Split3D;

/// <summary>
/// Flattens the admin menu: the screens used every day (orders, keys, quote requests, ...) become
/// top-level links without children, everything else is grouped below one "Configuration" dropdown.
/// </summary>
internal static class AdminMenuOrganizer
{
    public const string ConfigurationId = "tt-config";

    // Daily work screens, shown as top-level links in this order (right after "Dashboard").
    private static readonly string[] _topLevelIds =
        ["orders", "tt-studio-jobs", "split3d-licenses", "split3d-devices", "tt-studio-quotes", "products", "customers"];

    // Groups moved into "Configuration", in this order. A null entry inserts a divider.
    private static readonly string[] _configurationIds =
        ["configuration", "split3d", "tt-studio", null, "catalog", "sales", "users", "promotions", "cms", null, "system", "modules"];

    public static void Organize(TreeNode<MenuItem> root)
    {
        if (root.SelectNodeById(ConfigurationId) != null)
        {
            return;
        }

        // 1. Promote daily work screens to top-level links.
        var anchor = root.SelectNodeById("dashboard");
        if (anchor?.Parent != root)
        {
            anchor = null;
        }

        foreach (var id in _topLevelIds)
        {
            var node = root.SelectNodeById(id);
            if (node == null || node.HasChildren)
            {
                continue;
            }

            Detach(node);

            if (anchor != null)
            {
                node.InsertAfter(anchor);
            }
            else
            {
                root.Prepend(node);
            }

            anchor = node;
        }

        if (root.SelectNodeById("products") is { } products && products.Parent == root)
        {
            products.Value.ResKey = "Admin.Catalog.Products";
            products.Value.Text = "Products";
        }

        // 2. Move all dropdown menus into one "Configuration" menu.
        var configItem = new MenuItem().ToBuilder()
            .Id(ConfigurationId)
            .Text("Configuration")
            .ResKey("Admin.Configuration")
            .Icon("icm icm-equalizer")
            .AsItem();

        var config = new TreeNode<MenuItem>(configItem, configItem.Id);

        var separatorIndex = 0;
        foreach (var id in _configurationIds)
        {
            if (id == null)
            {
                var separator = new MenuItem { Id = $"{ConfigurationId}-sep-{++separatorIndex}", Text = "[SKIP]", IsGroupHeader = true };
                config.Append(new TreeNode<MenuItem>(separator, separator.Id));
                continue;
            }

            if (root.SelectNodeById(id) is { } node && node.Parent == root)
            {
                MoveInto(config, node);
            }
        }

        // Dropdowns added by other modules.
        var others = root.Children.Where(x => x.HasChildren).ToList();
        if (others.Count > 0)
        {
            var separator = new MenuItem { Id = $"{ConfigurationId}-sep-{++separatorIndex}", Text = "[SKIP]", IsGroupHeader = true };
            config.Append(new TreeNode<MenuItem>(separator, separator.Id));

            foreach (var node in others)
            {
                MoveInto(config, node);
            }
        }

        // The core "Configuration" menu is now a group inside the new one: give it a distinct name.
        if (config.SelectNodeById("configuration") is { } storeSetup)
        {
            storeSetup.Value.ResKey = "Plugins.Split3D.AdminMenu.StoreSetup";
            storeSetup.Value.Text = "Store setup";
        }

        root.Append(config);
    }

    private static void MoveInto(TreeNode<MenuItem> config, TreeNode<MenuItem> node)
    {
        Detach(node);

        // A group with a single entry only adds a click: show the entry itself.
        var children = node.Children.Where(x => !x.Value.IsGroupHeader).ToList();
        if (!node.Value.HasRoute && children.Count == 1 && children[0].Value.HasRoute)
        {
            node = children[0];
            Detach(node);
        }

        config.Append(node);
    }

    private static void Detach(TreeNode<MenuItem> node)
    {
        node.Parent?.Remove(node);
    }
}
