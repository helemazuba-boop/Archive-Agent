using ArchiveAgent.Services;
using ArchiveAgent.Services.Automations.Actions;
using ArchiveAgent.Services.Automations.Triggers;
using ArchiveAgent.Services.NotificationProviders;
using ArchiveAgent.Views.SettingPages;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArchiveAgent;

[PluginEntrance]
public class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        var pluginPaths = ArchivePluginPaths.CreatePrepared(PluginConfigFolder, Info.PluginFolderPath);
        ArchiveDiagnosticsLogger.Configure(pluginPaths.LogsDirectory);

        services.AddSingleton(pluginPaths);
        services.AddSingleton<IArchiveConfigManager, ArchiveConfigManager>();
        services.AddSingleton<IArchiveStateManager, ArchiveStateManager>();
        services.AddSingleton<IArchivePythonIpcService, ArchivePythonIpcService>();
        services.AddSingleton<ArchiveOrchestrator>();
        services.AddSingleton<ArchiveNotificationService>();
        services.AddSingleton<ArchiveAutomationBridgeService>();
        services.AddSingleton<ArchiveFileOperationService>();
        services.AddSingleton<ArchiveFileWatchService>();
        services.AddSingleton<ArchivePluginLifecycle>();

        services.AddNotificationProvider<ArchiveNotificationProvider>();
        services.AddSettingsPage<ArchiveMainSettingsPage>();
        services.AddTrigger<ArchiveOperationSucceededTrigger>();
        services.AddTrigger<ArchiveOperationFailedTrigger>();
        services.AddAction<RunArchiveOperationAction>();

        AppBase.Current.AppStarted += (_, _) =>
        {
            _ = IAppHost.GetService<ArchivePluginLifecycle>().StartAsync();
        };

        AppBase.Current.AppStopping += (_, _) =>
        {
            try
            {
                IAppHost.GetService<ArchivePluginLifecycle>().StopAsync().GetAwaiter().GetResult();
            }
            catch
            {
                ArchivePythonProcessTracker.CleanupTrackedProcesses();
            }
        };
    }
}
