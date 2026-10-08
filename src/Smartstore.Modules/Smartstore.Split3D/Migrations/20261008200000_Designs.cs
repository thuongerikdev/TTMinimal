using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Split3D.Migrations;

/// <summary>
/// Designs of the 3D product designer (<see cref="Split3DDesign"/>), looked up by code from the admin order page
/// to rebuild the print files.
/// </summary>
[MigrationVersion("2026-10-08 20:00:00", "Split3D: Designs")]
internal class Designs : Migration
{
    const string DesignTable = "Split3DDesign";

    public override void Up()
    {
        if (!Schema.Table(DesignTable).Exists())
        {
            Create.Table(DesignTable)
                .WithIdColumn()
                .WithColumn(nameof(Split3DDesign.Code)).AsString(20).NotNullable().Unique("IX_Split3DDesign_Code")
                .WithColumn(nameof(Split3DDesign.Kind)).AsString(30).Nullable()
                .WithColumn(nameof(Split3DDesign.SpecJson)).AsMaxString().NotNullable()
                .WithColumn(nameof(Split3DDesign.CreatedOnUtc)).AsDateTime2().NotNullable();
        }
    }

    public override void Down()
    {
    }
}
