using System.Globalization;
using FluentMigrator;
using Smartstore.Core.Data;
using Smartstore.Core.Data.Migrations;
using Smartstore.Data.Migrations;

namespace Smartstore.Split3D.Migrations;

/// <summary>
/// Introduces addons: each sellable addon has its own product code, and catalog products are
/// mapped to an addon and plan. Existing keys and plan products move to the default addon.
/// </summary>
[MigrationVersion("2026-09-27 09:00:00", "Split3D: Addons")]
internal class Addons : Migration, IDataSeeder<SmartDbContext>
{
    const string AddonTable = "Split3DAddon";
    const string AddonProductTable = "Split3DAddonProduct";
    const string LicenseTable = "Split3DLicense";

    public const string DefaultAddonName = "Split3D Print";
    public const string DefaultProductCode = "split3d-custom-109";

    public override void Up()
    {
        if (!Schema.Table(AddonTable).Exists())
        {
            Create.Table(AddonTable)
                .WithIdColumn()
                .WithColumn(nameof(Split3DAddon.Name)).AsString(200).NotNullable()
                .WithColumn(nameof(Split3DAddon.ProductCode)).AsString(100).NotNullable()
                    .Unique("IX_Split3DAddon_ProductCode")
                .WithColumn(nameof(Split3DAddon.Version)).AsString(50).Nullable()
                .WithColumn(nameof(Split3DAddon.Description)).AsMaxString().Nullable()
                .WithColumn(nameof(Split3DAddon.Active)).AsBoolean().NotNullable().WithDefaultValue(true)
                .WithColumn(nameof(Split3DAddon.DisplayOrder)).AsInt32().NotNullable().WithDefaultValue(0);
        }

        if (!Schema.Table(AddonProductTable).Exists())
        {
            Create.Table(AddonProductTable)
                .WithIdColumn()
                .WithColumn(nameof(Split3DAddonProduct.AddonId)).AsInt32().NotNullable()
                    .Indexed("IX_Split3DAddonProduct_AddonId")
                .WithColumn(nameof(Split3DAddonProduct.ProductId)).AsInt32().NotNullable()
                    .Indexed("IX_Split3DAddonProduct_ProductId")
                .WithColumn(nameof(Split3DAddonProduct.KeyType)).AsString(50).NotNullable()
                .WithColumn(nameof(Split3DAddonProduct.Days)).AsInt32().Nullable();
        }

        if (!Schema.Table(LicenseTable).Column(nameof(Split3DLicense.AddonId)).Exists())
        {
            Alter.Table(LicenseTable)
                .AddColumn(nameof(Split3DLicense.AddonId)).AsInt32().NotNullable().WithDefaultValue(0)
                    .Indexed("IX_Split3DLicense_AddonId");
        }

        if (!Schema.Table(LicenseTable).Column(nameof(Split3DLicense.ProductCode)).Exists())
        {
            Alter.Table(LicenseTable)
                .AddColumn(nameof(Split3DLicense.ProductCode)).AsString(100).Nullable();
        }
    }

    public override void Down()
    {
    }

    public DataSeederStage Stage => DataSeederStage.Early;
    public bool AbortOnFailure => false;

    public async Task SeedAsync(SmartDbContext context, CancellationToken cancelToken = default)
    {
        var settings = await context.Settings
            .Where(x => x.Name.StartsWith(nameof(Split3DSettings) + "."))
            .ToDictionaryAsync(x => x.Name[(nameof(Split3DSettings).Length + 1)..], x => x.Value, StringComparer.OrdinalIgnoreCase, cancelToken);

        var productCode = settings.GetValueOrDefault(nameof(Split3DSettings.ProductCode)).NullEmpty() ?? DefaultProductCode;

        var addon = await context.Split3DAddons().FirstOrDefaultAsync(x => x.ProductCode == productCode, cancelToken);
        if (addon == null)
        {
            addon = new Split3DAddon
            {
                Name = DefaultAddonName,
                ProductCode = productCode,
                Version = "3.0.121",
                Active = true
            };

            context.Split3DAddons().Add(addon);
            await context.SaveChangesAsync(cancelToken);
        }

        // Existing keys belong to the default addon.
        var orphans = await context.Split3DLicenses().Where(x => x.AddonId == 0).ToListAsync(cancelToken);
        foreach (var license in orphans)
        {
            license.AddonId = addon.Id;
            license.ProductCode ??= productCode;
        }

        // Plan products configured in settings become addon product mappings.
        var planSettings = new (string Setting, string Plan)[]
        {
            (nameof(Split3DSettings.ThreeMonthsProductId), "3 tháng"),
            (nameof(Split3DSettings.SixMonthsProductId), "6 tháng"),
            (nameof(Split3DSettings.OneYearProductId), "1 năm"),
            (nameof(Split3DSettings.LifetimeProductId), "Vĩnh viễn")
        };

        foreach (var (setting, plan) in planSettings)
        {
            if (int.TryParse(settings.GetValueOrDefault(setting), NumberStyles.Integer, CultureInfo.InvariantCulture, out var productId)
                && productId > 0
                && !await context.Split3DAddonProducts().AnyAsync(x => x.ProductId == productId, cancelToken))
            {
                context.Split3DAddonProducts().Add(new Split3DAddonProduct { AddonId = addon.Id, ProductId = productId, KeyType = plan });
            }
        }

        await context.SaveChangesAsync(cancelToken);
    }
}
