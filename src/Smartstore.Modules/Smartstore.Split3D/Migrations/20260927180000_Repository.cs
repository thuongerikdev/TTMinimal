using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Split3D.Migrations;

/// <summary>
/// Per-key Blender extension repository: secret token used in the repository URL.
/// </summary>
[MigrationVersion("2026-09-27 18:00:00", "Split3D: Repository")]
internal class Repository : Migration
{
    const string LicenseTable = "Split3DLicense";

    public override void Up()
    {
        if (!Schema.Table(LicenseTable).Column(nameof(Split3DLicense.RepoToken)).Exists())
        {
            Alter.Table(LicenseTable)
                .AddColumn(nameof(Split3DLicense.RepoToken)).AsString(32).Nullable()
                    .Indexed("IX_Split3DLicense_RepoToken");
        }
    }

    public override void Down()
    {
    }
}
