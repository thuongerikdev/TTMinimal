using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Split3D.Migrations;

/// <summary>
/// Paid 3D printing jobs (<see cref="PrintOrder"/>) and the contact log of quote requests
/// (<see cref="PrintQuoteContact"/>) with the follow-up columns of the requests themselves.
/// </summary>
[MigrationVersion("2026-10-06 21:00:00", "Split3D: Print orders and quote follow-up")]
internal class PrintOrders : Migration
{
    const string OrderTable = "PrintOrder";
    const string ContactTable = "PrintQuoteContact";
    const string QuoteTable = "PrintQuoteRequest";

    public override void Up()
    {
        if (!Schema.Table(OrderTable).Exists())
        {
            Create.Table(OrderTable)
                .WithIdColumn()
                .WithColumn(nameof(PrintOrder.CreatedOnUtc)).AsDateTime2().NotNullable()
                    .Indexed("IX_PrintOrder_CreatedOnUtc")
                .WithColumn(nameof(PrintOrder.UpdatedOnUtc)).AsDateTime2().NotNullable()
                .WithColumn(nameof(PrintOrder.Code)).AsString(30).Nullable()
                .WithColumn(nameof(PrintOrder.PayToken)).AsString(40).Nullable()
                    .Indexed("IX_PrintOrder_PayToken")
                .WithColumn(nameof(PrintOrder.CustomerId)).AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn(nameof(PrintOrder.QuoteRequestId)).AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn(nameof(PrintOrder.Technology)).AsString(100).Nullable()
                .WithColumn(nameof(PrintOrder.Material)).AsString(400).Nullable()
                .WithColumn(nameof(PrintOrder.Fill)).AsString(200).Nullable()
                .WithColumn(nameof(PrintOrder.TotalGrams)).AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn(nameof(PrintOrder.Pieces)).AsInt32().NotNullable().WithDefaultValue(1)
                .WithColumn(nameof(PrintOrder.ModelCount)).AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn(nameof(PrintOrder.PricePerGram)).AsDecimal(18, 4).NotNullable().WithDefaultValue(0)
                .WithColumn(nameof(PrintOrder.PriceEstimate)).AsDecimal(18, 4).NotNullable().WithDefaultValue(0)
                .WithColumn(nameof(PrintOrder.DepositPercent)).AsInt32().NotNullable().WithDefaultValue(100)
                .WithColumn(nameof(PrintOrder.DepositAmount)).AsDecimal(18, 4).NotNullable().WithDefaultValue(0)
                .WithColumn(nameof(PrintOrder.FinalPrice)).AsDecimal(18, 4).Nullable()
                .WithColumn(nameof(PrintOrder.ShippingFee)).AsDecimal(18, 4).Nullable()
                .WithColumn(nameof(PrintOrder.ModelsJson)).AsMaxString().Nullable()
                .WithColumn(nameof(PrintOrder.FileName)).AsString(400).Nullable()
                .WithColumn(nameof(PrintOrder.FilePath)).AsString(500).Nullable()
                .WithColumn(nameof(PrintOrder.FileSize)).AsInt64().NotNullable().WithDefaultValue(0)
                .WithColumn(nameof(PrintOrder.FileLink)).AsString(1000).Nullable()
                .WithColumn(nameof(PrintOrder.DeliveryMethodId)).AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn(nameof(PrintOrder.RecipientName)).AsString(200).Nullable()
                .WithColumn(nameof(PrintOrder.Phone)).AsString(50).Nullable()
                .WithColumn(nameof(PrintOrder.Email)).AsString(255).Nullable()
                .WithColumn(nameof(PrintOrder.AddressLine)).AsString(400).Nullable()
                .WithColumn(nameof(PrintOrder.City)).AsString(150).Nullable()
                .WithColumn(nameof(PrintOrder.DesiredOnUtc)).AsDateTime2().Nullable()
                .WithColumn(nameof(PrintOrder.Note)).AsMaxString().Nullable()
                .WithColumn(nameof(PrintOrder.StatusId)).AsInt32().NotNullable().WithDefaultValue(0)
                    .Indexed("IX_PrintOrder_StatusId")
                .WithColumn(nameof(PrintOrder.OrderId)).AsInt32().NotNullable().WithDefaultValue(0)
                    .Indexed("IX_PrintOrder_OrderId")
                .WithColumn(nameof(PrintOrder.PaidOnUtc)).AsDateTime2().Nullable()
                .WithColumn(nameof(PrintOrder.ConfirmedOnUtc)).AsDateTime2().Nullable()
                .WithColumn(nameof(PrintOrder.CompletedOnUtc)).AsDateTime2().Nullable()
                .WithColumn(nameof(PrintOrder.AdminNote)).AsMaxString().Nullable()
                .WithColumn(nameof(PrintOrder.IpAddress)).AsString(100).Nullable();
        }

        if (!Schema.Table(ContactTable).Exists())
        {
            Create.Table(ContactTable)
                .WithIdColumn()
                .WithColumn(nameof(PrintQuoteContact.PrintQuoteRequestId)).AsInt32().NotNullable()
                    .Indexed("IX_PrintQuoteContact_PrintQuoteRequestId")
                .WithColumn(nameof(PrintQuoteContact.CreatedOnUtc)).AsDateTime2().NotNullable()
                .WithColumn(nameof(PrintQuoteContact.ChannelId)).AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn(nameof(PrintQuoteContact.IsIncoming)).AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn(nameof(PrintQuoteContact.Message)).AsMaxString().Nullable()
                .WithColumn(nameof(PrintQuoteContact.UserId)).AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn(nameof(PrintQuoteContact.UserName)).AsString(200).Nullable();
        }

        if (!Schema.Table(QuoteTable).Column(nameof(PrintQuoteRequest.ContactCount)).Exists())
        {
            Alter.Table(QuoteTable)
                .AddColumn(nameof(PrintQuoteRequest.ContactCount)).AsInt32().NotNullable().WithDefaultValue(0)
                .AddColumn(nameof(PrintQuoteRequest.LastContactOnUtc)).AsDateTime2().Nullable()
                .AddColumn(nameof(PrintQuoteRequest.FollowUpOnUtc)).AsDateTime2().Nullable()
                .AddColumn(nameof(PrintQuoteRequest.PrintOrderId)).AsInt32().NotNullable().WithDefaultValue(0);
        }
    }

    public override void Down()
    {
    }
}
