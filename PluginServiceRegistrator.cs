using System.IO;
using LibraryHealthCheck.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LibraryHealthCheck;

/// <summary>
/// Registers plugin services with the Jellyfin DI container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <summary>
    /// Registers services for the plugin.
    /// </summary>
    /// <param name="serviceCollection">The service collection.</param>
    /// <param name="serverApplicationHost">The server application host.</param>
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost serverApplicationHost)
    {
        serviceCollection.AddSingleton<DataStore>(sp =>
        {
            var appPaths = sp.GetRequiredService<IApplicationPaths>();
            var dataDir = Path.Combine(appPaths.PluginConfigurationsPath, "LibraryHealthCheck");
            var logger = sp.GetRequiredService<ILogger<DataStore>>();
            return new DataStore(dataDir, logger);
        });

        serviceCollection.AddSingleton<LibraryScanner>();
    }
}
