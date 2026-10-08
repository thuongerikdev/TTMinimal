using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Split3D.Migrations;

/// <summary>
/// Kind of a studio job (<see cref="PrintOrder.Kind"/>): ordinary orders with physical products now run through the
/// same workflow as print jobs. Existing rows are print jobs from files (0).
/// </summary>
[MigrationVersion("2026-10-09 20:00:00", "Split3D: Print job kind")]
internal class PrintOrderKind : Migration
{
    const string OrderTable = "PrintOrder";

    public override void Up()
    {
        if (Schema.Table(OrderTable).Exists() && !Schema.Table(OrderTable).Column(nameof(PrintOrder.KindId)).Exists())
        {
            Alter.Table(OrderTable)
                .AddColumn(nameof(PrintOrder.KindId)).AsInt32().NotNullable().WithDefaultValue(0);
        }
    }

    public override void Down()
    {
    }
}
