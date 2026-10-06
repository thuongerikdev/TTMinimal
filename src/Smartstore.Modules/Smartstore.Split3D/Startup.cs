using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Smartstore.Split3D.Filters;
using Smartstore.Web.Controllers;
using Smartstore.Core.Data;
using Smartstore.Data;
using Smartstore.Data.Providers;
using Smartstore.Engine;
using Smartstore.Engine.Builders;

namespace Smartstore.Split3D;

internal class Startup : StarterBase
{
    public override void ConfigureServices(IServiceCollection services, IApplicationContext appContext)
    {
        services.AddTransient<IDbContextConfigurationSource<SmartDbContext>, SmartDbContextConfigurer>();
        services.AddScoped<Split3DLicenseService>();
        services.AddScoped<Split3DDeviceService>();
        services.AddScoped<Split3DRepoService>();
        services.AddScoped<Split3DStorefrontSetup>();
        services.AddScoped<Split3DOrderQuery>();
        services.AddScoped<Split3DUpgradeService>();
        services.AddScoped<StudioStorefrontSetup>();
        services.AddScoped<PrintQuoteService>();
        services.AddScoped<PrintQuoteFollowUpService>();
        services.AddScoped<PrintOrderService>();
        services.AddScoped<BankQrService>();
        services.AddScoped<StudioMailService>();

        services.Configure<MvcOptions>(o =>
        {
            o.Filters.AddEndpointFilter<CheckoutCompletedFilter, SmartController>()
                .ForController("Checkout")
                .ForAction("Completed")
                .WhenNonAjax();

            // Two-step checkout: cart > confirm, the payment method is chosen on the confirm page.
            o.Filters.AddEndpointFilter<CheckoutPaymentFilter, SmartController>()
                .ForController("Checkout")
                .ForAction("PaymentMethod")
                .WhenNonAjaxGet();

            o.Filters.AddEndpointFilter<CheckoutConfirmPaymentFilter, SmartController>()
                .ForController("Checkout")
                .ForAction("Confirm")
                .WhenNonAjaxGet();

            o.Filters.AddEndpointFilter<CustomerInfoAddressesFilter, SmartController>()
                .ForController("Customer")
                .ForAction("Info")
                .WhenNonAjax();

            o.Filters.AddEndpointFilter<CustomerAddressesRedirectFilter, SmartController>()
                .ForController("Customer")
                .ForAction("Addresses")
                .WhenNonAjaxGet();

            o.Filters.AddEndpointFilter<ToolPackageRedirectFilter, SmartController>()
                .ForController("Product")
                .ForAction("ProductDetails")
                .WhenNonAjaxGet();

            o.Filters.AddEndpointFilter<TextListFilter, SmartController>()
                .ForController("Product")
                .ForAction("ProductDetails")
                .WhenNonAjaxGet();

            o.Filters.AddEndpointFilter<ToolPackageRedirectFilter, SmartController>()
                .ForController("Catalog")
                .ForAction("Category")
                .WhenNonAjaxGet();

            o.Filters.AddEndpointFilter<AdminStyleFilter, SmartController>()
                .WhenNonAjaxGet();
        });
    }

    class SmartDbContextConfigurer : IDbContextConfigurationSource<SmartDbContext>
    {
        public void Configure(IServiceProvider services, DbContextOptionsBuilder builder)
        {
            builder.UseDbFactory(b =>
            {
                b.AddModelAssembly(this.GetType().Assembly);
            });
        }
    }
}
