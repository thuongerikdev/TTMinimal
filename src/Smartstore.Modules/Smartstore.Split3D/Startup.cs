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
        services.AddScoped<StudioStorefrontSetup>();
        services.AddScoped<PrintQuoteService>();

        services.Configure<MvcOptions>(o =>
        {
            o.Filters.AddEndpointFilter<CheckoutCompletedFilter, SmartController>()
                .ForController("Checkout")
                .ForAction("Completed")
                .WhenNonAjax();
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
