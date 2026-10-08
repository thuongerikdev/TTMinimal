using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Split3D.Migrations;

/// <summary>
/// Link to the user guide of an addon (<see cref="Split3DAddon.GuideUrl"/>).
/// </summary>
[MigrationVersion("2026-10-09 10:00:00", "Split3D: Addon guide URL")]
internal class AddonGuideUrl : Migration
{
    const string AddonTable = "Split3DAddon";

    public override void Up()
    {
        if (!Schema.Table(AddonTable).Column(nameof(Split3DAddon.GuideUrl)).Exists())
        {
            Alter.Table(AddonTable)
                .AddColumn(nameof(Split3DAddon.GuideUrl)).AsString(500).Nullable();
        }
    }

    public override void Down()
    {
    }
}
