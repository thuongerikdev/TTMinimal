using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Split3D.Migrations;

/// <summary>
/// Picture of a coming soon addon card (<see cref="Split3DAddon.MediaFileId"/>). The column was first added to
/// <see cref="AddonComingSoon"/> after that migration had already run on existing databases, so it gets its own version.
/// </summary>
[MigrationVersion("2026-10-08 23:00:00", "Split3D: Addon media file")]
internal class AddonMediaFile : Migration
{
    const string AddonTable = "Split3DAddon";

    public override void Up()
    {
        if (!Schema.Table(AddonTable).Column(nameof(Split3DAddon.MediaFileId)).Exists())
        {
            Alter.Table(AddonTable)
                .AddColumn(nameof(Split3DAddon.MediaFileId)).AsInt32().Nullable();
        }
    }

    public override void Down()
    {
    }
}
