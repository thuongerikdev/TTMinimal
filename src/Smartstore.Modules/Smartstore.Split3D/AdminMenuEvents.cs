using Smartstore.Collections;
using Smartstore.Core.Content.Menus;
using Smartstore.Core.Localization;
using Smartstore.Events;

namespace Smartstore.Split3D;

/// <summary>
/// Hides configured admin menu items to keep the backend focused on selling Split3D keys.
/// Only visibility is changed; permissions and direct URLs stay as they are.
/// </summary>
public class AdminMenuEvents : IConsumer
{
    public Localizer T { get; set; } = NullLocalizer.Instance;

    public void HandleEvent(MenuBuiltEvent message, Split3DSettings settings)
    {
        if (message.Name.EqualsNoCase("MyAccount"))
        {
            AddMyKeysItem(message.Root);
            return;
        }

        if (!settings.SimplifyAdminMenu || !message.Name.EqualsNoCase("admin"))
        {
            return;
        }

        var hiddenIds = ParseIds(settings.HiddenAdminMenuItems);
        if (hiddenIds.Count > 0)
        {
            foreach (var child in message.Root.Children)
            {
                Apply(child, hiddenIds);
            }
        }
    }

    /// <summary>
    /// Adds "My keys" to the customer account menu, right after "Orders".
    /// </summary>
    private void AddMyKeysItem(TreeNode<MenuItem> root)
    {
        if (root.SelectNodeById("split3d-keys") != null)
        {
            return;
        }

        var item = new MenuItem
        {
            Id = "split3d-keys",
            Text = T("Plugins.Split3D.MyKeys"),
            Icon = "fal fa-key",
            ActionName = "Index",
            ControllerName = "Split3DKeys"
        };
        item.RouteValues["area"] = string.Empty;

        var node = new TreeNode<MenuItem>(item, item.Id);
        var ordersNode = root.SelectNodeById("orders");
        if (ordersNode?.Parent != null)
        {
            node.InsertAfter(ordersNode);
        }
        else
        {
            root.Append(node);
        }
    }

    public static HashSet<string> ParseIds(string value)
    {
        return (value ?? string.Empty)
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static void Apply(TreeNode<MenuItem> node, HashSet<string> hiddenIds)
    {
        if (node.Value.Id != null && hiddenIds.Contains(node.Value.Id))
        {
            node.Value.Visible = false;
            return;
        }

        if (!node.HasChildren)
        {
            return;
        }

        // Post-order: resolve children first so nested dropdowns collapse correctly.
        foreach (var child in node.Children)
        {
            Apply(child, hiddenIds);
        }

        HideOrphanedSeparators(node);

        if (!node.Value.HasRoute && !node.Children.Any(x => x.Value.Visible && !IsSeparator(x)))
        {
            node.Value.Visible = false;
        }
    }

    private static void HideOrphanedSeparators(TreeNode<MenuItem> node)
    {
        var visible = node.Children.Where(x => x.Value.Visible).ToList();

        for (var i = 0; i < visible.Count; i++)
        {
            if (IsSeparator(visible[i]) && (i == 0 || i == visible.Count - 1 || IsSeparator(visible[i + 1])))
            {
                visible[i].Value.Visible = false;
            }
        }
    }

    private static bool IsSeparator(TreeNode<MenuItem> node)
        => node.Value.Id?.Contains("-sep-", StringComparison.OrdinalIgnoreCase) == true;
}
