using Smartstore.Collections;
using Smartstore.Core.Content.Menus;
using Smartstore.Core.Security;
using Smartstore.Web.Rendering.Builders;

namespace Smartstore.Split3D;

/// <summary>
/// Adds a top-level "Split3D" admin menu right after "Dashboard" with keys, addons and settings.
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
        node.Append(CreateItem("split3d-configure", "Settings", "Admin.Common.Configure", "gear", "Configure", "Split3D"));

        var dashboardNode = rootNode.SelectNodeById("dashboard");
        if (dashboardNode?.Parent != null)
        {
            node.InsertAfter(dashboardNode);
        }
        else
        {
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
