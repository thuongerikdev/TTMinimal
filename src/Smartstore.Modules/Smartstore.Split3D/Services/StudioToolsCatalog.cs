using Smartstore.Core.Catalog.Pricing;
using Smartstore.Core.Content.Media;
using Smartstore.Core.Data;
using Smartstore.Core.Security;
using Smartstore.Core.Stores;

namespace Smartstore.Split3D.Services;

/// <summary>
/// The tools board shown on the tools page and on the home page: every active addon with its purchasable packages.
/// </summary>
public class StudioToolsCatalog
{
    // Big enough for the detail panel, also used by the card.
    const int ImageSize = MediaSettings.ThumbnailSizeXl;

    private readonly SmartDbContext _db;
    private readonly IPriceCalculationService _priceCalculationService;
    private readonly IMediaService _mediaService;
    private readonly IAclService _aclService;
    private readonly IStoreMappingService _storeMappingService;
    private readonly Split3DSettings _settings;

    public StudioToolsCatalog(
        SmartDbContext db,
        IPriceCalculationService priceCalculationService,
        IMediaService mediaService,
        IAclService aclService,
        IStoreMappingService storeMappingService,
        Split3DSettings settings)
    {
        _db = db;
        _priceCalculationService = priceCalculationService;
        _mediaService = mediaService;
        _aclService = aclService;
        _storeMappingService = storeMappingService;
        _settings = settings;
    }

    /// <summary>
    /// Active tools that have at least one published package the current customer may buy.
    /// </summary>
    public async Task<List<ToolCardModel>> GetToolsAsync()
    {
        var addons = await _db.Split3DAddons().AsNoTracking()
            .Where(x => x.Active)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
            .ToListAsync();

        var mappings = await _db.Split3DAddonProducts().AsNoTracking().ToListAsync();
        var productIds = mappings.Select(x => x.ProductId).Distinct().ToArray();
        var products = await _db.Products.AsNoTracking()
            .Where(x => productIds.Contains(x.Id) && x.Published && !x.Deleted)
            .ToDictionaryAsync(x => x.Id);

        var options = _priceCalculationService.CreateDefaultOptions(true);
        var tools = new List<ToolCardModel>();

        foreach (var addon in addons)
        {
            var tool = new ToolCardModel
            {
                AddonId = addon.Id,
                Name = addon.Name,
                Version = addon.Version,
                Description = addon.Description
            };

            foreach (var mapping in mappings.Where(x => x.AddonId == addon.Id))
            {
                if (!products.TryGetValue(mapping.ProductId, out var product)
                    || !await _aclService.AuthorizeAsync(product)
                    || !await _storeMappingService.AuthorizeAsync(product))
                {
                    continue;
                }

                var price = await _priceCalculationService.CalculatePriceAsync(new PriceCalculationContext(product, options));
                var devices = mapping.MaxDevices ?? _settings.DefaultMaxDevices;

                tool.Packages.Add(new ToolPackageModel
                {
                    ProductId = product.Id,
                    Name = product.Name,
                    Duration = mapping.KeyType == Split3DPlans.Custom && mapping.Days.HasValue ? $"{mapping.Days} ngày" : mapping.KeyType,
                    Devices = Math.Max(devices, 1),
                    Price = price.FinalPrice.ToString(),
                    PriceValue = price.FinalPrice.Amount,
                    IsLifetime = mapping.KeyType == Split3DPlans.Lifetime,
                    DisplayOrder = product.DisplayOrder,
                    ImageUrl = product.MainPictureId > 0 ? await _mediaService.GetUrlAsync(product.MainPictureId, ImageSize, null, false) : null,
                    FullDescription = product.FullDescription
                });

                if (tool.Description.IsEmpty() && product.ShortDescription.HasValue())
                {
                    tool.Description = "<p>" + System.Net.WebUtility.HtmlEncode(product.ShortDescription) + "</p>";
                }
            }

            if (tool.Packages.Count == 0)
            {
                continue;
            }

            // Fewest devices first, then by price: "1 máy · 3 tháng" … "5 máy · vĩnh viễn".
            tool.Packages = tool.Packages.OrderBy(x => x.Devices).ThenBy(x => x.PriceValue).ToList();
            tool.DeviceOptions = tool.Packages.Select(x => x.Devices).Distinct().ToList();
            tools.Add(tool);
        }

        return tools;
    }
}
