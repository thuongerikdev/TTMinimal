using Smartstore.Core.Data;

namespace Smartstore.Split3D;

public static class SmartDbContextExtensions
{
    public static DbSet<Split3DLicense> Split3DLicenses(this SmartDbContext db)
        => db.Set<Split3DLicense>();

    public static DbSet<Split3DAddon> Split3DAddons(this SmartDbContext db)
        => db.Set<Split3DAddon>();

    public static DbSet<Split3DAddonProduct> Split3DAddonProducts(this SmartDbContext db)
        => db.Set<Split3DAddonProduct>();

    public static DbSet<Split3DDevice> Split3DDevices(this SmartDbContext db)
        => db.Set<Split3DDevice>();

    public static DbSet<PrintQuoteRequest> PrintQuoteRequests(this SmartDbContext db)
        => db.Set<PrintQuoteRequest>();
}
