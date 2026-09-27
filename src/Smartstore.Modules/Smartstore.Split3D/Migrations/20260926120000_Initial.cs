using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Split3D.Migrations;

[MigrationVersion("2026-09-26 12:00:00", "Split3D: Initial")]
internal class Initial : Migration
{
    const string TableName = "Split3DLicense";

    public override void Up()
    {
        if (Schema.Table(TableName).Exists())
        {
            return;
        }

        Create.Table(TableName)
            .WithIdColumn()
            .WithColumn(nameof(Split3DLicense.LicenseId)).AsString(64).NotNullable()
                .Unique("IX_Split3DLicense_LicenseId")
            .WithColumn(nameof(Split3DLicense.Email)).AsString(254).NotNullable()
                .Indexed("IX_Split3DLicense_Email")
            .WithColumn(nameof(Split3DLicense.CustomerName)).AsString(400).NotNullable()
            .WithColumn(nameof(Split3DLicense.Phone)).AsString(100).Nullable()
            .WithColumn(nameof(Split3DLicense.KeyType)).AsString(50).NotNullable()
            .WithColumn(nameof(Split3DLicense.Days)).AsInt32().Nullable()
            .WithColumn(nameof(Split3DLicense.Price)).AsDecimal(18, 4).NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(Split3DLicense.PurchaseDateUtc)).AsDateTime2().Nullable()
            .WithColumn(nameof(Split3DLicense.IssuedOnUtc)).AsDateTime2().NotNullable()
            .WithColumn(nameof(Split3DLicense.ExpiresOnUtc)).AsDateTime2().Nullable()
            .WithColumn(nameof(Split3DLicense.Token)).AsMaxString().NotNullable()
            .WithColumn(nameof(Split3DLicense.Notes)).AsMaxString().Nullable()
            .WithColumn(nameof(Split3DLicense.OrderId)).AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(Split3DLicense.OrderItemId)).AsInt32().NotNullable().WithDefaultValue(0)
                .Indexed("IX_Split3DLicense_OrderItemId")
            .WithColumn(nameof(Split3DLicense.CustomerId)).AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(Split3DLicense.EmailSent)).AsBoolean().NotNullable().WithDefaultValue(false);
    }

    public override void Down()
    {
    }
}
