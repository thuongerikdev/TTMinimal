using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Split3D.Migrations;

/// <summary>
/// Marketplace add-ons: the shop injects licensing into the uploaded package.
/// Existing add-ons (Split3D Print) keep their own licensing.
/// </summary>
[MigrationVersion("2026-10-02 10:00:00", "Split3D: Managed licensing")]
internal class ManagedLicensing : Migration
{
    const string AddonTable = "Split3DAddon";

    public override void Up()
    {
        if (!Schema.Table(AddonTable).Column(nameof(Split3DAddon.ManagedLicensing)).Exists())
        {
            Alter.Table(AddonTable)
                .AddColumn(nameof(Split3DAddon.ManagedLicensing)).AsBoolean().NotNullable().WithDefaultValue(false);
        }
    }

    public override void Down()
    {
    }
}
