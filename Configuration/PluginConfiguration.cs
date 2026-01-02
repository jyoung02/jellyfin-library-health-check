using MediaBrowser.Model.Plugins;

namespace LibraryHealthCheck.Configuration;

/// <summary>
/// Plugin configuration for Library Health Check.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        EnableScanning = true;
        CheckMissingPoster = true;
        CheckMissingOverview = true;
        CheckMissingYear = true;
        CheckMissingGenre = true;
    }

    /// <summary>
    /// Gets or sets a value indicating whether scanning is enabled.
    /// </summary>
    public bool EnableScanning { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to check for missing posters.
    /// </summary>
    public bool CheckMissingPoster { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to check for missing overviews.
    /// </summary>
    public bool CheckMissingOverview { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to check for missing years.
    /// </summary>
    public bool CheckMissingYear { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to check for missing genres.
    /// </summary>
    public bool CheckMissingGenre { get; set; }
}
