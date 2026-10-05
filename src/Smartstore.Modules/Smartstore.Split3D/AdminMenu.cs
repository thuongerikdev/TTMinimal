using Smartstore.Collections;
using Smartstore.Core.Content.Menus;
using Smartstore.Core.Security;
using Smartstore.Web.Rendering.Builders;

namespace Smartstore.Split3D;

/// <summary>
/// Adds the top-level "TT Minimal Studio" (quote requests, studio settings) and "Split3D" (keys, addons, settings)
/// admin menus right after "Dashboard".
/// </summary>
public class AdminMenu : IMenuProvider
{
    public string MenuName => "admin";

    public int Ordinal => 0;

    public void BuildMenu(TreeNode<MenuItem> rootNode)
    {
        var parent = new MenuItem().ToBuilder()
            .Id("split3d")
            .Text("Split3D")
            .ResKey("Plugins.Split3D.MenuTitle")
            .Icon("key", "bi")
            .PermissionNames(Permissions.Configuration.Module.Read)
            .AsItem();

        var node = new TreeNode<MenuItem>(parent, parent.Id);
        node.Append(CreateItem("split3d-licenses", "License keys", "Plugins.Split3D.Licenses", "key", "List", "Split3D"));
        node.Append(CreateItem("split3d-devices", "Devices", "Plugins.Split3D.Devices", "pc-display", "List", "Split3DDevice"));
        node.Append(CreateItem("split3d-addons", "Addons & plans", "Plugins.Split3D.Addons", "boxes", "List", "Split3DAddon"));
        node.Append(CreateItem("split3d-mail", "Key email", "Plugins.Split3D.Mail.KeyMenuTitle", "envelope-paper", "Index", "StudioMail"));
        node.Append(CreateItem("split3d-configure", "Settings", "Admin.Common.Configure", "gear", "Configure", "Split3D"));

        var studio = new MenuItem().ToBuilder()
            .Id("tt-studio")
            .Text("TT Minimal Studio")
            .ResKey("Plugins.Split3D.Studio.MenuTitle")
            .Icon("printer", "bi")
            .PermissionNames(Permissions.Configuration.Module.Read)
            .AsItem();

        var studioNode = new TreeNode<MenuItem>(studio, studio.Id);
        studioNode.Append(CreateItem("tt-studio-quotes", "Print & design requests", "Plugins.Split3D.Studio.Quotes", "printer", "List", "PrintQuote"));
        studioNode.Append(CreateItem("tt-studio-mail", "Emails", "Plugins.Split3D.Mail.MenuTitle", "envelope-paper-heart", "Index", "StudioMail"));
        studioNode.Append(CreateItem("tt-studio-mail", "Emails", "Plugins.Split3D.Mail.MenuTitle", "envelope-paper-heart", "Index", "StudioMail"));
        studioNode.Append(CreateItem("tt-studio-settings", "Studio settings", "Plugins.Split3D.Studio.Settings", "shop", "Settings", "PrintQuote"));

        var dashboardNode = rootNode.SelectNodeById("dashboard");
        if (dashboardNode?.Parent != null)
        {
            studioNode.InsertAfter(dashboardNode);
            node.InsertAfter(studioNode);
        }
        else
        {
            rootNode.Append(studioNode);
            rootNode.Append(node);
        }
    }

    private static TreeNode<MenuItem> CreateItem(string id, string text, string resKey, string icon, string action, string controller)
    {
        var item = new MenuItem().ToBuilder()
            .Id(id)
            .Text(text)
            .ResKey(resKey)
            .Icon(icon, "bi")
            .PermissionNames(Permissions.Configuration.Module.Read)
            .Action(action, controller, new { area = "Admin" })
            .AsItem();

        return new TreeNode<MenuItem>(item, item.Id);
    }
}
