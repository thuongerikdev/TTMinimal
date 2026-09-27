using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Split3D.Migrations;

/// <summary>
/// Online activation: devices a key is activated on, a per-key device limit and a block flag.
/// </summary>
[MigrationVersion("2026-09-27 15:00:00", "Split3D: Devices")]
internal class Devices : Migration
{
    const string DeviceTable = "Split3DDevice";
    const string LicenseTable = "Split3DLicense";

    public override void Up()
    {
        if (!Schema.Table(DeviceTable).Exists())
        {
            Create.Table(DeviceTable)
                .WithIdColumn()
                .WithColumn(nameof(Split3DDevice.Split3DLicenseId)).AsInt32().NotNullable()
                .WithColumn(nameof(Split3DDevice.DeviceId)).AsString(64).NotNullable()
                    .Indexed("IX_Split3DDevice_DeviceId")
                .WithColumn(nameof(Split3DDevice.DeviceName)).AsString(200).Nullable()
                .WithColumn(nameof(Split3DDevice.Platform)).AsString(100).Nullable()
                .WithColumn(nameof(Split3DDevice.BlenderVersion)).AsString(50).Nullable()
                .WithColumn(nameof(Split3DDevice.AddonVersion)).AsString(50).Nullable()
                .WithColumn(nameof(Split3DDevice.LastIpAddress)).AsString(100).Nullable()
                .WithColumn(nameof(Split3DDevice.FirstActivatedOnUtc)).AsDateTime2().NotNullable()
                .WithColumn(nameof(Split3DDevice.ActivatedOnUtc)).AsDateTime2().NotNullable()
                .WithColumn(nameof(Split3DDevice.LastSeenOnUtc)).AsDateTime2().NotNullable()
                .WithColumn(nameof(Split3DDevice.DeactivatedOnUtc)).AsDateTime2().Nullable()
                .WithColumn(nameof(Split3DDevice.DeactivatedBy)).AsString(20).Nullable();

            Create.Index("IX_Split3DDevice_Split3DLicenseId_DeviceId")
                .OnTable(DeviceTable)
                .OnColumn(nameof(Split3DDevice.Split3DLicenseId)).Ascending()
                .OnColumn(nameof(Split3DDevice.DeviceId)).Ascending()
                .WithOptions().Unique();
        }

        if (!Schema.Table(LicenseTable).Column(nameof(Split3DLicense.MaxDevices)).Exists())
        {
            Alter.Table(LicenseTable)
                .AddColumn(nameof(Split3DLicense.MaxDevices)).AsInt32().Nullable();
        }

        if (!Schema.Table(LicenseTable).Column(nameof(Split3DLicense.Blocked)).Exists())
        {
            Alter.Table(LicenseTable)
                .AddColumn(nameof(Split3DLicense.Blocked)).AsBoolean().NotNullable().WithDefaultValue(false);
        }
    }

    public override void Down()
    {
    }
}
