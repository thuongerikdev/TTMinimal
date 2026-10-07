using Autofac;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Smartstore.Split3D.Filters;
using Smartstore.Web.Controllers;
using Smartstore.Core.Checkout.Orders;
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
        services.AddScoped<StudioOrderClassifier>();
        services.AddScoped<Split3DUpgradeService>();
        services.AddScoped<StudioStorefrontSetup>();
        services.AddScoped<PrintQuoteService>();
        services.AddScoped<PrintQuoteFollowUpService>();
        services.AddScoped<PrintOrderService>();
        services.AddScoped<BankQrService>();
        services.AddScoped<StudioMailService>();
        services.AddScoped<StudioDeliveryService>();

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

            // Delivery address and shipping method on the confirm page (GET), required before placing the order (POST).
            o.Filters.AddEndpointFilter<CheckoutDeliveryFilter, SmartController>()
                .ForController("Checkout")
                .ForAction("Confirm")
                .WhenNonAjax();

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

            // Admin products: grouped overview instead of the flat grid.
            o.Filters.AddEndpointFilter<ProductListRedirectFilter, SmartController>()
                .ForController("Product")
                .ForAction("List")
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

/// <summary>
/// Replaces Smartstore's checkout factory. Runs late so that it overrides the core registration.
/// </summary>
internal class CheckoutStartup : StarterBase
{
    public override int Order => StarterOrdering.Late;

    public override void ConfigureContainer(ContainerBuilder builder, IApplicationContext appContext)
    {
        // Carts with physical products require shipping in the two-step checkout too (see StudioCheckoutFactory).
        builder.RegisterType<StudioCheckoutFactory>().As<ICheckoutFactory>().InstancePerLifetimeScope();
    }
}
