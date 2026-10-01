using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Split3D.Migrations;

/// <summary>
/// Packages by number of devices: devices per key on the product mapping.
/// </summary>
[MigrationVersion("2026-10-02 09:00:00", "Split3D: Package devices")]
internal class PackageDevices : Migration
{
    const string MappingTable = "Split3DAddonProduct";

    public override void Up()
    {
        if (!Schema.Table(MappingTable).Column(nameof(Split3DAddonProduct.MaxDevices)).Exists())
        {
            Alter.Table(MappingTable)
                .AddColumn(nameof(Split3DAddonProduct.MaxDevices)).AsInt32().Nullable();
        }
    }

    public override void Down()
    {
    }
}
