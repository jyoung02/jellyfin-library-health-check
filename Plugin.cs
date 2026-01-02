using System;
using System.Collections.Generic;
using LibraryHealthCheck.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace LibraryHealthCheck;

/// <summary>
/// Library Health Check plugin for Jellyfin.
/// Scans media libraries for issues like missing metadata.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Application paths.</param>
    /// <param name="xmlSerializer">XML serializer.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Library Health Check";

    /// <inheritdoc />
    public override string Description => "Scans media libraries for issues like missing metadata, posters, and descriptions.";

    /// <inheritdoc />
    public override Guid Id => new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    /// <summary>
    /// Gets the plugin pages.
    /// </summary>
    /// <returns>Collection of plugin page info.</returns>
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
            },
            new PluginPageInfo
            {
                Name = "Health Check Results",
                EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.healthPage.html",
                MenuSection = "admin",
                MenuIcon = "health_and_safety"
            }
        };
    }
}
