using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Split3D.Migrations;

/// <summary>
/// Index on the job number: every cart line of a print job is priced by looking the job up by its code
/// (see <see cref="Services.PrintOrderPriceCalculator"/>).
/// </summary>
[MigrationVersion("2026-10-06 22:00:00", "Split3D: Print order code index")]
internal class PrintOrderCode : Migration
{
    const string OrderTable = "PrintOrder";
    const string IndexName = "IX_PrintOrder_Code";

    public override void Up()
    {
        if (Schema.Table(OrderTable).Exists() && !Schema.Table(OrderTable).Index(IndexName).Exists())
        {
            Create.Index(IndexName)
                .OnTable(OrderTable)
                .OnColumn(nameof(PrintOrder.Code)).Ascending()
                .WithOptions().NonClustered();
        }
    }

    public override void Down()
    {
    }
}
