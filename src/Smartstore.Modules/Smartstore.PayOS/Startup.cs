using Microsoft.Extensions.DependencyInjection;
using Smartstore.Engine;
using Smartstore.Engine.Builders;
using Smartstore.Net.Http;
using Smartstore.PayOS.Client;
using Smartstore.PayOS.Services;

namespace Smartstore.PayOS;

internal class Startup : StarterBase
{
    public override bool Matches(IApplicationContext appContext)
        => appContext.IsInstalled;

    public override void ConfigureServices(IServiceCollection services, IApplicationContext appContext)
    {
        services.AddHttpClient<PayOSHttpClient>()
            .AddSmartstoreUserAgent()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri(PayOSHttpClient.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            });

        services.AddScoped<PayOSService>();
    }
}
