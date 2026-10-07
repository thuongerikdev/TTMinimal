#nullable enable

using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Smartstore.Core.Catalog.Attributes;
using Smartstore.Core.Catalog.Products;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Checkout.Shipping;
using Smartstore.Core.Common;
using Smartstore.Core.Data;
using Smartstore.Core.Identity;
using Smartstore.Core.Messaging;
using Smartstore.Core.Seo;
using Smartstore.Core.Stores;
using Smartstore.Engine;
using Smartstore.IO;
using Smartstore.Utilities;

namespace Smartstore.Split3D.Services;

/// <summary>
/// The price of a print job as the price list sees it.
/// </summary>
public sealed record PrintJobPrice
{
    public string Technology { get; init; } = string.Empty;
    public string Material { get; init; } = string.Empty;
    public int TotalGrams { get; init; }
    public int Pieces { get; init; }
    public decimal PricePerGram { get; init; }

    /// <summary>Weight × price per gram.</summary>
    public decimal Total { get; init; }

    /// <summary>Label of the price tier the job falls into, e.g. "Từ 500 g".</summary>
    public string? TierLabel { get; init; }

    public int DepositPercent { get; init; }

    /// <summary>What the customer pays with the order.</summary>
    public decimal Deposit { get; init; }

    public decimal Outstanding => Math.Max(0, Total - Deposit);
}

/// <summary>
/// Creates and advances paid 3D printing jobs (<see cref="PrintOrder"/>): prices the weighed models from the
/// studio price list, stores their files, puts the job into the cart as one line of a hidden print product,
/// links it to the order it was paid with and moves it through the studio workflow.
/// </summary>
public class PrintOrderService
{
    /// <summary>
    /// Hidden product that carries a print job in cart and order. One line per job; the line is priced with the
    /// deposit of the job named in its attributes, see <see cref="PrintOrderPriceCalculator"/>.
    /// </summary>
    public const string PrintProductSku = "TT-PRINT";
    public const string PrintProductName = "In 3D theo file";

    /// <summary>
    /// Name of the product attribute that shows which job a cart or order line belongs to.
    /// </summary>
    public const string JobAttributeName = "Hồ sơ in";

    /// <summary>
    /// Folder below the tenant root holding the model files of print jobs.
    /// </summary>
    public const string FileFolder = "PrintOrders";

    /// <summary>
    /// Weight of a single piece has to stay in this range; the browser weighs the models, so the value is not trusted.
    /// </summary>
    private const int MinGrams = 1;
    private const int MaxGrams = 500_000;

    private static readonly Regex _codeRegex = new(@"IN\d{5,}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly SmartDbContext _db;
    private readonly IApplicationContext _appContext;
    private readonly IShoppingCartService _cartService;
    private readonly IUrlService _urlService;
    private readonly IEmailAccountService _emailAccountService;
    private readonly IStoreContext _storeContext;
    private readonly StudioSettings _settings;

    public PrintOrderService(
        SmartDbContext db,
        IApplicationContext appContext,
        IShoppingCartService cartService,
        IUrlService urlService,
        IEmailAccountService emailAccountService,
        IStoreContext storeContext,
        StudioSettings settings)
    {
        _db = db;
        _appContext = appContext;
        _cartService = cartService;
        _urlService = urlService;
        _emailAccountService = emailAccountService;
        _storeContext = storeContext;
        _settings = settings;
    }

    public ILogger Logger { get; set; } = NullLogger.Instance;

    public int DepositPercent => Math.Clamp(_settings.DepositPercent, 1, 100);

    #region Pricing

    /// <summary>
    /// Prices a job from the studio price list. The total weight of all pieces picks the price tier,
    /// exactly like the calculator on the "In 3D" page.
    /// </summary>
    /// <param name="depositPercent">Deposit share, or <c>null</c> for the configured one.</param>
    public PrintJobPrice CalculatePrice(string? technology, string? material, IEnumerable<PrintOrderModel> models, int? depositPercent = null)
    {
        var technologies = PrintPriceList.Parse(_settings.PrintPriceTable);
        var tech = technologies.FirstOrDefault(x => x.Name.EqualsNoCase(technology)) ?? technologies.FirstOrDefault();
        var mat = tech?.FindMaterial(material);

        var list = models?.ToList() ?? [];
        var totalGrams = list.Sum(x => x.TotalGrams);
        var pieces = list.Sum(x => Math.Max(x.Quantity, 1));
        var tier = mat?.GetTier(totalGrams);
        var pricePerGram = tier?.PricePerGram ?? 0;
        var total = decimal.Round(totalGrams * pricePerGram, 0);
        var percent = Math.Clamp(depositPercent ?? DepositPercent, 1, 100);

        return new PrintJobPrice
        {
            Technology = tech?.Name ?? technology ?? string.Empty,
            Material = mat?.Name ?? material ?? string.Empty,
            TotalGrams = totalGrams,
            Pieces = pieces,
            PricePerGram = pricePerGram,
            Total = total,
            TierLabel = tier?.Label,
            DepositPercent = percent,
            Deposit = RoundDeposit(total, percent)
        };
    }

    /// <summary>
    /// Deposit for <paramref name="total"/>, rounded to full thousands so that the transfer amount stays simple.
    /// </summary>
    public static decimal RoundDeposit(decimal total, int percent)
    {
        if (total <= 0)
        {
            return 0;
        }

        if (percent >= 100)
        {
            return total;
        }

        var deposit = decimal.Round(total * percent / 100m / 1000m, 0, MidpointRounding.AwayFromZero) * 1000m;

        return Math.Clamp(deposit, 1000m, total);
    }

    /// <summary>
    /// Cleans up the models as sent by the browser: names, sane weights and quantities, at most 50 models.
    /// </summary>
    public static List<PrintOrderModel> SanitizeModels(IEnumerable<PrintOrderModel>? models)
    {
        return (models ?? [])
            .Where(x => x != null)
            .Take(50)
            .Select(x => new PrintOrderModel
            {
                Name = (Path.GetFileName(x.Name ?? string.Empty).NullEmpty() ?? "model").Truncate(200),
                Grams = Math.Clamp(x.Grams, MinGrams, MaxGrams),
                Quantity = Math.Clamp(x.Quantity, 1, 9999),
                Size = x.Size?.Truncate(100),
                Volume = Math.Clamp(x.Volume, 0, 10_000_000),
                Fill = x.Fill?.Truncate(60),
                Manual = x.Manual
            })
            .ToList();
    }

    #endregion

    #region Creating jobs

    /// <summary>
    /// Creates a job from the price calculator: prices it, stores the model files and returns it as a draft.
    /// </summary>
    public async Task<PrintOrder> CreateDraftAsync(
        PrintOrder job,
        IReadOnlyCollection<IFormFile>? files,
        CancellationToken cancelToken = default)
    {
        Guard.NotNull(job);

        var models = SanitizeModels(job.Models);
        var price = CalculatePrice(job.Technology, job.Material, models, job.DepositPercent > 0 ? job.DepositPercent : null);

        job.Models = models;
        job.Technology = price.Technology;
        job.Material = price.Material;
        job.TotalGrams = price.TotalGrams;
        job.Pieces = price.Pieces;
        job.ModelCount = models.Count;
        job.PricePerGram = price.PricePerGram;
        job.PriceEstimate = price.Total;
        job.DepositPercent = price.DepositPercent;
        job.DepositAmount = price.Deposit;
        job.Status = PrintOrderStatus.Draft;
        job.CreatedOnUtc = job.UpdatedOnUtc = DateTime.UtcNow;

        _db.PrintOrders().Add(job);
        await _db.SaveChangesAsync(cancelToken);

        job.Code = FormatCode(job.Id);
        await StoreFilesAsync(job, files, cancelToken);
        await _db.SaveChangesAsync(cancelToken);

        return job;
    }

    /// <summary>
    /// Creates a job from a quote request the studio settled a price for. The customer pays it through
    /// <see cref="PrintOrder.PayToken"/>; the model file of the request is copied over.
    /// </summary>
    public async Task<PrintOrder> CreateFromQuoteAsync(
        PrintQuoteRequest quote,
        decimal price,
        int? depositPercent = null,
        CancellationToken cancelToken = default)
    {
        Guard.NotNull(quote);
        Guard.IsPositive(price);

        var percent = Math.Clamp(depositPercent ?? DepositPercent, 1, 100);
        var models = quote.EstimatedGrams > 0
            ? new List<PrintOrderModel>
            {
                new()
                {
                    Name = quote.FileName.NullEmpty() ?? "Theo yêu cầu báo giá",
                    Grams = Math.Clamp(quote.EstimatedGrams.Value, MinGrams, MaxGrams),
                    Quantity = Math.Clamp(quote.Quantity, 1, 9999),
                    Manual = true
                }
            }
            : [];

        var job = new PrintOrder
        {
            CustomerId = quote.CustomerId,
            QuoteRequestId = quote.Id,
            PayToken = CommonHelper.GenerateRandomDigitCode(6) + Guid.NewGuid().ToString("N")[..16],
            Technology = quote.Technology,
            Material = quote.Material,
            TotalGrams = models.Sum(x => x.TotalGrams),
            Pieces = Math.Max(quote.Quantity, 1),
            ModelCount = models.Count,
            PriceEstimate = decimal.Round(price, 0),
            FinalPrice = decimal.Round(price, 0),
            DepositPercent = percent,
            DepositAmount = RoundDeposit(decimal.Round(price, 0), percent),
            RecipientName = quote.Name,
            Phone = quote.Phone,
            Email = quote.Email,
            Note = quote.Note,
            FileLink = quote.FileLink,
            Status = PrintOrderStatus.Draft,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        };

        job.Models = models;
        job.DeliveryMethod = _settings.AllowPickup ? PrintDeliveryMethod.Pickup : PrintDeliveryMethod.Shipping;

        _db.PrintOrders().Add(job);
        await _db.SaveChangesAsync(cancelToken);

        job.Code = FormatCode(job.Id);

        if (quote.FilePath.HasValue())
        {
            var source = await _appContext.TenantRoot.GetFileAsync(quote.FilePath);
            if (source.Exists)
            {
                var path = BuildFilePath(job, quote.FileName.NullEmpty() ?? source.Name);
                await _appContext.TenantRoot.CopyFileAsync(quote.FilePath, path, true);

                job.FileName = (quote.FileName.NullEmpty() ?? source.Name).Truncate(400);
                job.FilePath = path;
                job.FileSize = source.Length;
            }
        }

        quote.PrintOrderId = job.Id;
        quote.Status = PrintQuoteStatus.Quoted;
        quote.QuotedPrice = job.PriceEstimate;
        quote.UpdatedOnUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancelToken);

        return job;
    }

    /// <summary>
    /// Job number as shown to the customer and stored in the cart line.
    /// </summary>
    public static string FormatCode(int id)
        => "IN" + id.ToString("D5");

    private string BuildFilePath(PrintOrder job, string fileName)
        => PathUtility.Join(FileFolder, job.CreatedOnUtc.ToString("yyyy-MM"), $"{job.Id}-{SafeFileName(fileName)}");

    /// <summary>
    /// Stores the uploaded models, several of them together as one ZIP archive.
    /// </summary>
    private async Task StoreFilesAsync(PrintOrder job, IReadOnlyCollection<IFormFile>? files, CancellationToken cancelToken)
    {
        var uploads = (files ?? []).Where(x => x != null && x.Length > 0).ToList();
        if (uploads.Count == 0)
        {
            return;
        }

        var single = uploads.Count == 1;
        var fileName = single ? Path.GetFileName(uploads[0].FileName) : $"{job.Code}-models.zip";
        var path = BuildFilePath(job, fileName);
        var target = await _appContext.TenantRoot.GetFileAsync(path);

        if (single)
        {
            using var stream = uploads[0].OpenReadStream();
            await target.CreateAsync(stream, true, cancelToken);
        }
        else
        {
            await using var output = await target.OpenWriteAsync("application/zip", cancelToken);
            using var archive = new ZipArchive(output, ZipArchiveMode.Create);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var upload in uploads)
            {
                var entryName = SafeFileName(upload.FileName);
                for (var i = 2; !names.Add(entryName); i++)
                {
                    entryName = $"{Path.GetFileNameWithoutExtension(SafeFileName(upload.FileName))}-{i}{Path.GetExtension(upload.FileName)}";
                }

                // Model files compress poorly; fastest keeps big uploads quick.
                var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                await using var entryStream = entry.Open();
                using var source = upload.OpenReadStream();
                await source.CopyToAsync(entryStream, cancelToken);
            }
        }

        job.FileName = fileName.Truncate(400);
        job.FilePath = path;
        job.FileSize = (await _appContext.TenantRoot.GetFileAsync(path)).Length;
    }

    private static string SafeFileName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? "model");
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) || char.IsWhiteSpace(c) ? '_' : c).ToArray());

        return cleaned.Truncate(120).NullEmpty() ?? "model";
    }

    #endregion

    #region Cart and order

    /// <summary>
    /// Puts a draft job into the customer's cart as its own line, priced with the deposit.
    /// A line of the same job already in the cart is replaced.
    /// </summary>
    /// <returns>Warnings; empty on success.</returns>
    public async Task<IList<string>> AddToCartAsync(PrintOrder job, Customer customer, int storeId, CancellationToken cancelToken = default)
    {
        Guard.NotNull(job);
        Guard.NotNull(customer);

        if (job.DepositAmount <= 0)
        {
            return ["Đơn in chưa có giá, không thể thanh toán."];
        }

        var (product, attribute) = await EnsurePrintProductAsync(cancelToken);
        var cart = await _cartService.GetCartAsync(customer, ShoppingCartType.ShoppingCart, storeId);

        foreach (var existing in cart.Items.Where(x => x.Item.ProductId == product.Id && ReadJobCode(x.Item.RawAttributes) == job.Code))
        {
            await _cartService.DeleteCartItemAsync(existing.Item, false, true);
        }

        var selection = new ProductVariantAttributeSelection(null);
        selection.AddAttributeValue(attribute.Id, Describe(job));

        var ctx = new AddToCartContext
        {
            Customer = customer,
            Product = product,
            CartType = ShoppingCartType.ShoppingCart,
            StoreId = storeId,
            Quantity = 1,
            RawAttributes = selection.AsJson()
        };

        if (!await _cartService.AddToCartAsync(ctx))
        {
            return ctx.Warnings;
        }

        if (job.CustomerId == 0 && customer.IsRegistered())
        {
            job.CustomerId = customer.Id;
            job.UpdatedOnUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancelToken);
        }

        return [];
    }

    /// <summary>
    /// One-line description of a job, as shown on the cart line and in the order.
    /// </summary>
    public static string Describe(PrintOrder job)
    {
        var parts = new List<string> { job.Code ?? FormatCode(job.Id) };

        if (job.ModelCount > 0)
        {
            parts.Add($"{job.ModelCount} mẫu · {job.Pieces} cái");
        }

        if (job.TotalGrams > 0)
        {
            parts.Add(PrintPriceList.FormatWeight(job.TotalGrams));
        }

        if (job.Technology.HasValue())
        {
            parts.Add(string.Join(' ', new[] { job.Technology, job.Material }.Where(x => x.HasValue())));
        }

        if (job.DepositPercent < 100)
        {
            parts.Add($"tạm tính {PrintPriceList.FormatPrice(job.FinalPrice ?? job.PriceEstimate)}, cọc {job.DepositPercent}%");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// Gets the job code stored in the attributes of a cart or order line, or <c>null</c>.
    /// </summary>
    public static string? ReadJobCode(string? rawAttributes)
    {
        if (rawAttributes.IsEmpty())
        {
            return null;
        }

        var match = _codeRegex.Match(rawAttributes!);

        return match.Success ? match.Value.ToUpperInvariant() : null;
    }

    /// <summary>
    /// Links the jobs paid with <paramref name="order"/> to it (on order placement), copies the delivery address
    /// into the order and tells the studio about it.
    /// </summary>
    public async Task<List<PrintOrder>> AttachToOrderAsync(Order order, CancellationToken cancelToken = default)
    {
        Guard.NotNull(order);

        var codes = await _db.OrderItems
            .AsNoTracking()
            .Where(x => x.OrderId == order.Id)
            .Select(x => x.RawAttributes)
            .ToListAsync(cancelToken);

        var jobCodes = codes.Select(ReadJobCode).Where(x => x != null).Distinct().ToArray();
        if (jobCodes.Length == 0)
        {
            return [];
        }

        var jobs = await _db.PrintOrders()
            .Where(x => jobCodes.Contains(x.Code) && (x.OrderId == 0 || x.OrderId == order.Id))
            .ToListAsync(cancelToken);

        if (jobs.Count == 0)
        {
            return [];
        }

        foreach (var job in jobs)
        {
            job.OrderId = order.Id;
            job.Status = order.PaymentStatus == PaymentStatus.Paid ? PrintOrderStatus.Paid : PrintOrderStatus.AwaitingPayment;
            job.PaidOnUtc = order.PaymentStatus == PaymentStatus.Paid ? DateTime.UtcNow : null;
            job.UpdatedOnUtc = DateTime.UtcNow;

            if (job.CustomerId == 0)
            {
                job.CustomerId = order.CustomerId;
            }
        }

        await ApplyAddressAsync(order, jobs[0], cancelToken);
        await CloseQuotesAsync(jobs, cancelToken);

        _db.OrderNotes.Add(order, BuildOrderNote(jobs), displayToCustomer: true);
        await _db.SaveChangesAsync(cancelToken);

        foreach (var job in jobs)
        {
            QueueNotification(job, "Đơn in 3D mới", order);
        }

        await _db.SaveChangesAsync(cancelToken);

        return jobs;
    }

    /// <summary>
    /// Marks the jobs of a paid order as paid, so the studio can confirm them.
    /// </summary>
    public async Task<List<PrintOrder>> MarkPaidAsync(Order order, CancellationToken cancelToken = default)
    {
        Guard.NotNull(order);

        var jobs = await _db.PrintOrders()
            .Where(x => x.OrderId == order.Id && x.StatusId < (int)PrintOrderStatus.Paid)
            .ToListAsync(cancelToken);

        if (jobs.Count == 0)
        {
            return [];
        }

        foreach (var job in jobs)
        {
            job.Status = PrintOrderStatus.Paid;
            job.PaidOnUtc = DateTime.UtcNow;
            job.UpdatedOnUtc = DateTime.UtcNow;
        }

        _db.OrderNotes.Add(order,
            $"Đã nhận {PrintPriceList.FormatPrice(order.OrderTotal)} cho đơn in {string.Join(", ", jobs.Select(x => x.Code))}. " +
            "Studio sẽ kiểm tra file và xác nhận đơn, sau đó bắt đầu in.",
            displayToCustomer: true);

        await _db.SaveChangesAsync(cancelToken);

        foreach (var job in jobs)
        {
            QueueNotification(job, "Đơn in 3D đã thanh toán – cần xác nhận", order);
        }

        await _db.SaveChangesAsync(cancelToken);

        return jobs;
    }

    /// <summary>
    /// Moves a job to <paramref name="status"/> and writes what happened into the order as a note.
    /// </summary>
    /// <param name="message">Extra text for the order note, e.g. the printing time or the tracking number.</param>
    /// <param name="notifyCustomer">Whether the order note is visible to the customer.</param>
    public async Task SetStatusAsync(
        PrintOrder job,
        PrintOrderStatus status,
        string? message = null,
        bool notifyCustomer = true,
        CancellationToken cancelToken = default)
    {
        Guard.NotNull(job);

        job.Status = status;
        job.UpdatedOnUtc = DateTime.UtcNow;

        switch (status)
        {
            case PrintOrderStatus.Confirmed:
                job.ConfirmedOnUtc ??= DateTime.UtcNow;
                break;
            case PrintOrderStatus.Completed:
                job.CompletedOnUtc ??= DateTime.UtcNow;
                break;
        }

        if (job.OrderId > 0)
        {
            var order = await _db.Orders.FindByIdAsync(job.OrderId, false, cancelToken);
            if (order != null)
            {
                var note = $"Đơn in {job.Code}: {GetStatusText(status)}.";
                if (message.HasValue())
                {
                    note += " " + message!.Trim();
                }

                if (status == PrintOrderStatus.Ready && job.Outstanding > 0)
                {
                    note += $" Còn phải thanh toán {PrintPriceList.FormatPrice(job.Outstanding)} khi nhận hàng.";
                }

                _db.OrderNotes.Add(order, note, notifyCustomer);
            }
        }

        await _db.SaveChangesAsync(cancelToken);
    }

    public static string GetStatusText(PrintOrderStatus status) => status switch
    {
        PrintOrderStatus.Draft => "chưa thanh toán",
        PrintOrderStatus.AwaitingPayment => "chờ thanh toán",
        PrintOrderStatus.Paid => "đã nhận tiền, chờ studio xác nhận",
        PrintOrderStatus.Confirmed => "studio đã xác nhận, vào hàng chờ in",
        PrintOrderStatus.Printing => "đang in",
        PrintOrderStatus.Ready => "đã in xong, chờ giao / nhận hàng",
        PrintOrderStatus.Completed => "đã hoàn thành",
        PrintOrderStatus.Cancelled => "đã huỷ",
        _ => status.ToString()
    };

    private string BuildOrderNote(List<PrintOrder> jobs)
    {
        var lines = new List<string> { "Đơn in 3D theo file:" };

        foreach (var job in jobs)
        {
            lines.Add($"- {Describe(job)}");

            foreach (var model in job.Models)
            {
                lines.Add($"  · {model.Name}: {model.Grams:N0} g × {model.Quantity}" + (model.Size.HasValue() ? $" ({model.Size})" : string.Empty));
            }

            if (job.DepositPercent < 100)
            {
                lines.Add($"  Đã trả {PrintPriceList.FormatPrice(job.DepositAmount)}, còn lại khoảng {PrintPriceList.FormatPrice(job.Outstanding)} thanh toán khi nhận hàng.");
            }

            lines.Add(job.DeliveryMethod == PrintDeliveryMethod.Pickup
                ? $"  Nhận tại xưởng: {_settings.Address}"
                : $"  Giao tới: {string.Join(", ", new[] { job.RecipientName, job.Phone, job.AddressLine, job.City }.Where(x => x.HasValue()))}");

            if (job.Note.HasValue())
            {
                lines.Add("  Ghi chú: " + job.Note);
            }
        }

        lines.Add("Khối lượng tự cân là ước tính; studio kiểm tra file slice thật rồi chốt giá cuối cùng trước khi in.");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Moves the quote requests the jobs came from to <see cref="PrintQuoteStatus.Ordered"/>: the customer accepted
    /// the quote by paying, so the request needs no further follow-up.
    /// </summary>
    private async Task CloseQuotesAsync(List<PrintOrder> jobs, CancellationToken cancelToken)
    {
        var quoteIds = jobs.Where(x => x.QuoteRequestId > 0).Select(x => x.QuoteRequestId).Distinct().ToArray();
        if (quoteIds.Length == 0)
        {
            return;
        }

        var quotes = await _db.PrintQuoteRequests()
            .Where(x => quoteIds.Contains(x.Id) && x.StatusId < (int)PrintQuoteStatus.Ordered)
            .ToListAsync(cancelToken);

        foreach (var quote in quotes)
        {
            quote.Status = PrintQuoteStatus.Ordered;
            quote.FollowUpOnUtc = null;
            quote.UpdatedOnUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Copies the delivery address of the job into the order. The storefront checkout of this shop asks for
    /// no address (cart > confirm), so the address collected on the print order page is the only one there is.
    /// </summary>
    private async Task ApplyAddressAsync(Order order, PrintOrder job, CancellationToken cancelToken)
    {
        if (order.ShippingAddressId > 0 && order.BillingAddressId > 0)
        {
            return;
        }

        var name = (job.RecipientName ?? string.Empty).Trim();
        var index = name.LastIndexOf(' ');
        var countryId = await _db.Countries
            .Where(x => x.TwoLetterIsoCode == "VN")
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancelToken);

        Address CreateAddress() => new()
        {
            FirstName = (index > 0 ? name[..index] : name).NullEmpty() ?? "Khách hàng",
            LastName = index > 0 ? name[(index + 1)..] : string.Empty,
            Email = job.Email.NullEmpty(),
            PhoneNumber = job.Phone.NullEmpty(),
            Address1 = job.DeliveryMethod == PrintDeliveryMethod.Pickup
                ? "Nhận tại xưởng"
                : job.AddressLine.NullEmpty(),
            City = job.DeliveryMethod == PrintDeliveryMethod.Pickup ? null : job.City.NullEmpty(),
            CountryId = countryId,
            CreatedOnUtc = DateTime.UtcNow
        };

        if (order.BillingAddressId == 0)
        {
            order.BillingAddress = CreateAddress();
        }

        if (order.ShippingAddressId == 0 && order.ShippingStatus != ShippingStatus.ShippingNotRequired)
        {
            order.ShippingAddress = CreateAddress();
        }
    }

    /// <summary>
    /// Creates the hidden print product and its job attribute if they do not exist yet.
    /// </summary>
    public async Task<(Product Product, ProductVariantAttribute Attribute)> EnsurePrintProductAsync(CancellationToken cancelToken = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(x => x.Sku == PrintProductSku && !x.Deleted, cancelToken);
        if (product == null)
        {
            var template = await _db.ProductTemplates.FirstOrDefaultAsync(x => x.ViewPath == "Product", cancelToken)
                ?? await _db.ProductTemplates.FirstAsync(cancelToken);

            product = new Product
            {
                ProductType = ProductType.SimpleProduct,
                Sku = PrintProductSku,
                Name = PrintProductName,
                ShortDescription = "Đơn in 3D theo file khách gửi, tính theo khối lượng.",
                ProductTemplateId = template.Id,
                Price = 0,
                AllowCustomerReviews = false
            };

            _db.Products.Add(product);
        }

        product.Published = true;
        product.Visibility = ProductVisibility.Hidden;
        product.ManageInventoryMethod = ManageInventoryMethod.DontManageStock;
        product.OrderMinimumQuantity = 1;
        product.OrderMaximumQuantity = 1;
        product.QuantityStep = 1;
        // The line is priced from the job row (PrintOrderPriceCalculator), so no price travels with the request.
        product.CustomerEntersPrice = false;
        product.Price = 0;
        // Delivery is agreed with the customer after the studio confirms the job and the address comes from the /dat-in
        // form (AttachToOrderAsync), so the job needs no delivery block in checkout and the cart adds no shipping cost.
        product.IsShippingEnabled = false;
        product.IsFreeShipping = true;
        product.IsEsd = false;
        product.IsDownload = false;
        product.ShowOnHomePage = false;
        product.DisableWishlistButton = true;

        await _db.SaveChangesAsync(cancelToken);

        if ((await _urlService.GetActiveSlugAsync(product.Id, nameof(Product), 0)).IsEmpty())
        {
            await _urlService.SaveSlugAsync(product, "in-3d-theo-file", product.Name, true);
        }

        var attribute = await _db.ProductVariantAttributes
            .Include(x => x.ProductAttribute)
            .FirstOrDefaultAsync(x => x.ProductId == product.Id, cancelToken);

        if (attribute == null)
        {
            var productAttribute = await _db.ProductAttributes.FirstOrDefaultAsync(x => x.Name == JobAttributeName, cancelToken);
            if (productAttribute == null)
            {
                productAttribute = new ProductAttribute { Name = JobAttributeName };
                _db.ProductAttributes.Add(productAttribute);
                await _db.SaveChangesAsync(cancelToken);
            }

            attribute = new ProductVariantAttribute
            {
                ProductId = product.Id,
                ProductAttributeId = productAttribute.Id,
                AttributeControlTypeId = (int)AttributeControlType.TextBox,
                IsRequired = false,
                DisplayOrder = 1
            };

            _db.ProductVariantAttributes.Add(attribute);
            await _db.SaveChangesAsync(cancelToken);
        }

        return (product, attribute);
    }

    #endregion

    #region Files, queries, mail

    /// <summary>
    /// Gets the uploaded model file of a job, or <c>null</c> if there is none.
    /// </summary>
    public async Task<IFile?> GetFileAsync(PrintOrder? job)
    {
        if (job?.FilePath.IsEmpty() ?? true)
        {
            return null;
        }

        var file = await _appContext.TenantRoot.GetFileAsync(job!.FilePath!);

        return file.Exists ? file : null;
    }

    /// <summary>
    /// Deletes jobs together with their uploaded files.
    /// </summary>
    public async Task<int> DeleteAsync(IEnumerable<PrintOrder> jobs, CancellationToken cancelToken = default)
    {
        var list = jobs.ToList();

        foreach (var job in list)
        {
            // Jobs created from a quote request share nothing with it: the file was copied into the job folder.
            var file = await GetFileAsync(job);
            if (file != null && job.FilePath!.StartsWith(FileFolder, StringComparison.OrdinalIgnoreCase))
            {
                await file.DeleteAsync(cancelToken);
            }
        }

        _db.PrintOrders().RemoveRange(list);

        return await _db.SaveChangesAsync(cancelToken);
    }

    /// <summary>
    /// Gets a job by its payment token, or <c>null</c>.
    /// </summary>
    public Task<PrintOrder?> GetByTokenAsync(string? token, CancellationToken cancelToken = default)
    {
        if (token.IsEmpty() || token!.Length < 10)
        {
            return Task.FromResult<PrintOrder?>(null);
        }

        return _db.PrintOrders().FirstOrDefaultAsync(x => x.PayToken == token, cancelToken);
    }

    /// <summary>
    /// Absolute URL the customer pays a job created from a quote request with.
    /// </summary>
    public string BuildPayUrl(PrintOrder job)
        => _storeContext.CurrentStore.GetBaseUrl().TrimEnd('/') + "/dat-in/thanh-toan/" + job.PayToken;

    /// <summary>
    /// Queues an email to the studio about a job. Never throws: a job must not be lost because of a mail problem.
    /// </summary>
    public void QueueNotification(PrintOrder job, string subject, Order? order = null)
    {
        try
        {
            var emailAccount = _emailAccountService.GetDefaultEmailAccount();
            if (emailAccount == null)
            {
                Logger.Warn("Print order: no email account configured, notification not sent.");
                return;
            }

            var baseUrl = _storeContext.CurrentStore.GetBaseUrl().TrimEnd('/');

            string Row(string label, string? value) => value.IsEmpty()
                ? string.Empty
                : $"<tr><td style=\"padding:4px 12px 4px 0;color:#64748b\">{label}</td><td style=\"padding:4px 0\"><strong>{WebUtility.HtmlEncode(value)}</strong></td></tr>";

            var body =
                $"<p>{WebUtility.HtmlEncode(subject)} <strong>{job.Code}</strong>.</p>" +
                "<table style=\"border-collapse:collapse\">" +
                Row("Khách hàng", job.RecipientName) +
                Row("Điện thoại", job.Phone) +
                Row("Email", job.Email) +
                Row("Công nghệ", string.Join(' ', new[] { job.Technology, job.Material }.Where(x => x.HasValue()))) +
                Row("Mô hình", $"{job.ModelCount} mẫu · {job.Pieces} cái · {PrintPriceList.FormatWeight(job.TotalGrams)}") +
                Row("Tạm tính", PrintPriceList.FormatPrice(job.FinalPrice ?? job.PriceEstimate)) +
                Row("Đã thanh toán", $"{PrintPriceList.FormatPrice(job.DepositAmount)} ({job.DepositPercent}%)") +
                Row("Còn lại", job.Outstanding > 0 ? PrintPriceList.FormatPrice(job.Outstanding) : null) +
                Row("Nhận hàng", job.DeliveryMethod == PrintDeliveryMethod.Pickup
                    ? "Nhận tại xưởng"
                    : string.Join(", ", new[] { job.AddressLine, job.City }.Where(x => x.HasValue()))) +
                Row("Cần trước ngày", job.DesiredOnUtc?.ToString("dd/MM/yyyy")) +
                Row("File", job.FileName.HasValue() ? $"{job.FileName} ({Prettifier.HumanizeBytes(job.FileSize)})" : null) +
                Row("Link file", job.FileLink) +
                "</table>" +
                (job.Note.HasValue() ? $"<p style=\"white-space:pre-wrap\">{WebUtility.HtmlEncode(job.Note)}</p>" : string.Empty) +
                $"<p><a href=\"{baseUrl}/admin/printjob/edit/{job.Id}\">Mở đơn in trong trang quản trị</a>" +
                (order != null ? $" · <a href=\"{baseUrl}/admin/order/edit/{order.Id}\">đơn hàng {order.GetOrderNumber()}</a>" : string.Empty) +
                "</p>";

            _db.QueuedEmails.Add(new QueuedEmail
            {
                From = emailAccount.ToMailAddress().ToString(),
                To = _settings.QuoteNotifyEmail.NullEmpty() ?? emailAccount.Email,
                ReplyTo = job.Email.NullEmpty(),
                Subject = $"[{_settings.BrandName}] {subject} {job.Code} – {job.RecipientName}",
                Body = $"<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;line-height:1.5\">{body}</div>",
                CreatedOnUtc = DateTime.UtcNow,
                EmailAccountId = emailAccount.Id,
                Priority = 5
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Print order {job.Id}: notification email failed.");
        }
    }

    #endregion
}
