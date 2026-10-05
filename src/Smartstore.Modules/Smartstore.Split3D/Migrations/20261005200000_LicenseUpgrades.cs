using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Split3D.Migrations;

/// <summary>
/// Plan upgrades of keys (customer purchases and admin upgrades).
/// </summary>
[MigrationVersion("2026-10-05 20:00:00", "Split3D: License upgrades")]
internal class LicenseUpgrades : Migration
{
    const string UpgradeTable = "Split3DLicenseUpgrade";

    public override void Up()
    {
        if (!Schema.Table(UpgradeTable).Exists())
        {
            Create.Table(UpgradeTable)
                .WithIdColumn()
                .WithColumn(nameof(Split3DLicenseUpgrade.Split3DLicenseId)).AsInt32().NotNullable()
                    .Indexed("IX_Split3DLicenseUpgrade_Split3DLicenseId")
                .WithColumn(nameof(Split3DLicenseUpgrade.StatusId)).AsInt32().NotNullable()
                .WithColumn(nameof(Split3DLicenseUpgrade.ByAdmin)).AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn(nameof(Split3DLicenseUpgrade.CustomerId)).AsInt32().NotNullable()
                .WithColumn(nameof(Split3DLicenseUpgrade.TargetProductId)).AsInt32().NotNullable()
                .WithColumn(nameof(Split3DLicenseUpgrade.OrderId)).AsInt32().NotNullable()
                    .Indexed("IX_Split3DLicenseUpgrade_OrderId")
                .WithColumn(nameof(Split3DLicenseUpgrade.FromKeyType)).AsString(50).NotNullable()
                .WithColumn(nameof(Split3DLicenseUpgrade.ToKeyType)).AsString(50).NotNullable()
                .WithColumn(nameof(Split3DLicenseUpgrade.ToDays)).AsInt32().Nullable()
                .WithColumn(nameof(Split3DLicenseUpgrade.FromExpiresOnUtc)).AsDateTime2().Nullable()
                .WithColumn(nameof(Split3DLicenseUpgrade.ToExpiresOnUtc)).AsDateTime2().Nullable()
                .WithColumn(nameof(Split3DLicenseUpgrade.FromMaxDevices)).AsInt32().Nullable()
                .WithColumn(nameof(Split3DLicenseUpgrade.ToMaxDevices)).AsInt32().Nullable()
                .WithColumn(nameof(Split3DLicenseUpgrade.Price)).AsDecimal(18, 4).NotNullable()
                .WithColumn(nameof(Split3DLicenseUpgrade.Notes)).AsString(int.MaxValue).Nullable()
                .WithColumn(nameof(Split3DLicenseUpgrade.CreatedOnUtc)).AsDateTime2().NotNullable()
                .WithColumn(nameof(Split3DLicenseUpgrade.AppliedOnUtc)).AsDateTime2().Nullable();

            Create.Index("IX_Split3DLicenseUpgrade_CustomerId_StatusId")
                .OnTable(UpgradeTable)
                .OnColumn(nameof(Split3DLicenseUpgrade.CustomerId)).Ascending()
                .OnColumn(nameof(Split3DLicenseUpgrade.StatusId)).Ascending()
                .WithOptions().NonClustered();
        }
    }

    public override void Down()
    {
    }
}
