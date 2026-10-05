using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Smartstore.Engine.Initialization;
using Smartstore.Engine.Modularity;
using Smartstore.Scheduling;

namespace Smartstore.PayOS.Tasks;

/// <summary>
/// Registers <see cref="PayOSSyncTask"/> on the first request, so that shops which installed the module
/// before the task existed get it without reinstalling.
/// </summary>
internal class PayOSTaskInitializer : IApplicationInitializer
{
    private readonly IModuleCatalog _moduleCatalog;
    private readonly ITaskStore _taskStore;

    public PayOSTaskInitializer(IModuleCatalog moduleCatalog, ITaskStore taskStore)
    {
        _moduleCatalog = moduleCatalog;
        _taskStore = taskStore;
    }

    public ILogger Logger { get; set; } = NullLogger.Instance;

    public int Order => int.MaxValue;
    public bool ThrowOnError => false;
    public int MaxAttempts => 3;

    public async Task InitializeAsync(HttpContext httpContext)
    {
        if (_moduleCatalog.GetModuleByAssembly(GetType().Assembly)?.IsInstalled() == true)
        {
            await RegisterTaskAsync(_taskStore);
        }
    }

    public Task OnFailAsync(Exception exception, bool willRetry)
    {
        Logger.Error(exception, "Registering the PayOS sync task failed.");
        return Task.CompletedTask;
    }

    internal static Task RegisterTaskAsync(ITaskStore taskStore)
    {
        return taskStore.GetOrAddTaskAsync<PayOSSyncTask>(x =>
        {
            x.Name = "PayOS: sync pending payments";
            x.CronExpression = "*/5 * * * *"; // Every 5 minutes
            x.Enabled = true;
            x.StopOnError = false;
        });
    }
}
