using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Split3D.Migrations;

/// <summary>
/// 3D printing quote requests sent from the "In 3D" page.
/// </summary>
[MigrationVersion("2026-09-29 10:00:00", "Split3D: Print quotes")]
internal class PrintQuotes : Migration
{
    const string QuoteTable = "PrintQuoteRequest";

    public override void Up()
    {
        if (!Schema.Table(QuoteTable).Exists())
        {
            Create.Table(QuoteTable)
                .WithIdColumn()
                .WithColumn(nameof(PrintQuoteRequest.CreatedOnUtc)).AsDateTime2().NotNullable()
                    .Indexed("IX_PrintQuoteRequest_CreatedOnUtc")
                .WithColumn(nameof(PrintQuoteRequest.UpdatedOnUtc)).AsDateTime2().NotNullable()
                .WithColumn(nameof(PrintQuoteRequest.CustomerId)).AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn(nameof(PrintQuoteRequest.Name)).AsString(200).NotNullable()
                .WithColumn(nameof(PrintQuoteRequest.Phone)).AsString(50).NotNullable()
                .WithColumn(nameof(PrintQuoteRequest.Email)).AsString(255).Nullable()
                .WithColumn(nameof(PrintQuoteRequest.Technology)).AsString(100).Nullable()
                .WithColumn(nameof(PrintQuoteRequest.Material)).AsString(400).Nullable()
                .WithColumn(nameof(PrintQuoteRequest.Quantity)).AsInt32().NotNullable().WithDefaultValue(1)
                .WithColumn(nameof(PrintQuoteRequest.EstimatedGrams)).AsInt32().Nullable()
                .WithColumn(nameof(PrintQuoteRequest.NeedsDesign)).AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn(nameof(PrintQuoteRequest.Note)).AsMaxString().Nullable()
                .WithColumn(nameof(PrintQuoteRequest.FileLink)).AsString(1000).Nullable()
                .WithColumn(nameof(PrintQuoteRequest.FileName)).AsString(400).Nullable()
                .WithColumn(nameof(PrintQuoteRequest.FilePath)).AsString(500).Nullable()
                .WithColumn(nameof(PrintQuoteRequest.FileSize)).AsInt64().NotNullable().WithDefaultValue(0)
                .WithColumn(nameof(PrintQuoteRequest.StatusId)).AsInt32().NotNullable().WithDefaultValue(0)
                    .Indexed("IX_PrintQuoteRequest_StatusId")
                .WithColumn(nameof(PrintQuoteRequest.QuotedPrice)).AsDecimal(18, 4).Nullable()
                .WithColumn(nameof(PrintQuoteRequest.AdminNote)).AsMaxString().Nullable()
                .WithColumn(nameof(PrintQuoteRequest.IpAddress)).AsString(100).Nullable();
        }
    }

    public override void Down()
    {
    }
}
