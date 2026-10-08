using FluentMigrator;
using Smartstore.Core.Data;
using Smartstore.Core.Data.Migrations;
using Smartstore.Data.Migrations;

namespace Smartstore.Split3D.Migrations;

/// <summary>
/// "Sắp ra mắt" teaser cards on the tools board become addons flagged as coming soon, so they are edited in the admin.
/// The cards that were hard-coded in _StudioTools.cshtml are created once.
/// </summary>
[MigrationVersion("2026-10-08 10:00:00", "Split3D: Addon coming soon")]
internal class AddonComingSoon : Migration, IDataSeeder<SmartDbContext>
{
    const string AddonTable = "Split3DAddon";

    public override void Up()
    {
        if (!Schema.Table(AddonTable).Column(nameof(Split3DAddon.ComingSoon)).Exists())
        {
            Alter.Table(AddonTable)
                .AddColumn(nameof(Split3DAddon.ComingSoon)).AsBoolean().NotNullable().WithDefaultValue(false);
        }
        if (!Schema.Table(AddonTable).Column(nameof(Split3DAddon.Kind)).Exists())
        {
            Alter.Table(AddonTable)
                .AddColumn(nameof(Split3DAddon.Kind)).AsString(100).Nullable();
        }
        if (!Schema.Table(AddonTable).Column(nameof(Split3DAddon.Icon)).Exists())
        {
            Alter.Table(AddonTable)
                .AddColumn(nameof(Split3DAddon.Icon)).AsString(50).Nullable();
        }
    }

    public override void Down()
    {
    }

    public DataSeederStage Stage => DataSeederStage.Early;
    public bool AbortOnFailure => false;

    public async Task SeedAsync(SmartDbContext context, CancellationToken cancelToken = default)
    {
        var teasers = new[]
        {
            new Split3DAddon
            {
                Name = "Công cụ tạo khuôn",
                ProductCode = "ttminimal-mold",
                Kind = "Addon Blender",
                Icon = "layers",
                Description = "Dựng khuôn đúc nhiều mảnh từ mô hình ngay trong Blender: tự chia mặt phân khuôn, thêm chốt định vị, lỗ rót và lỗ thoát khí, xuất file sẵn để in."
            },
            new Split3DAddon
            {
                Name = "App tạo khuôn",
                ProductCode = "ttminimal-mold-app",
                Kind = "Ứng dụng độc lập",
                Icon = "cube",
                Description = "Phiên bản chạy riêng trên máy tính, không cần cài Blender: mở STL / OBJ / 3MF, tạo khuôn vài cú bấm và xuất file in."
            },
            new Split3DAddon
            {
                Name = "App Split3D",
                ProductCode = "ttminimal-split3d-app",
                Kind = "Ứng dụng độc lập",
                Icon = "scissors",
                Description = "Cắt mô hình quá khổ thành nhiều mảnh vừa bàn in, tự tạo khớp nối — như addon Split3D Print nhưng không cần Blender."
            }
        };

        // Once only: after that the admin owns these rows, including deleting them.
        if (await context.Split3DAddons().AnyAsync(x => x.ComingSoon, cancelToken))
        {
            return;
        }

        var displayOrder = (await context.Split3DAddons().MaxAsync(x => (int?)x.DisplayOrder, cancelToken) ?? 0) + 1;
        var codes = teasers.Select(x => x.ProductCode).ToArray();
        var existing = await context.Split3DAddons().Where(x => codes.Contains(x.ProductCode)).Select(x => x.ProductCode).ToListAsync(cancelToken);

        foreach (var teaser in teasers.Where(x => !existing.Contains(x.ProductCode)))
        {
            teaser.Active = true;
            teaser.ComingSoon = true;
            teaser.ManagedLicensing = true;
            teaser.Version = "1.0.0";
            teaser.DisplayOrder = displayOrder++;
            context.Split3DAddons().Add(teaser);
        }

        await context.SaveChangesAsync(cancelToken);
    }
}
