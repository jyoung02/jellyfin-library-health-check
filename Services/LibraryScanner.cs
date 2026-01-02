using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using LibraryHealthCheck.Configuration;
using LibraryHealthCheck.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;

namespace LibraryHealthCheck.Services;

/// <summary>
/// Scans library items for health issues.
/// </summary>
public class LibraryScanner
{
    private readonly ILibraryManager _libraryManager;
    private readonly ISubtitleManager _subtitleManager;
    private readonly DataStore _dataStore;
    private readonly ILogger<LibraryScanner> _logger;

    private volatile bool _isScanning;
    private Guid _currentScanLibraryId;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryScanner"/> class.
    /// </summary>
    public LibraryScanner(
        ILibraryManager libraryManager,
        ISubtitleManager subtitleManager,
        DataStore dataStore,
        ILogger<LibraryScanner> logger)
    {
        _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
        _subtitleManager = subtitleManager ?? throw new ArgumentNullException(nameof(subtitleManager));
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets a value indicating whether a scan is currently in progress.
    /// </summary>
    public bool IsScanning => _isScanning;

    /// <summary>
    /// Gets the library ID currently being scanned.
    /// </summary>
    public Guid CurrentScanLibraryId => _currentScanLibraryId;

    /// <summary>
    /// Gets all virtual folders (libraries) in Jellyfin.
    /// </summary>
    public IEnumerable<VirtualFolderInfo> GetLibraries()
    {
        return _libraryManager.GetVirtualFolders();
    }

    /// <summary>
    /// Scans a library for health issues.
    /// </summary>
    public async Task<ScanResult> ScanLibraryAsync(Guid libraryId, CancellationToken cancellationToken = default)
    {
        if (_isScanning)
        {
            throw new InvalidOperationException("A scan is already in progress.");
        }

        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        if (!config.EnableScanning)
        {
            throw new InvalidOperationException("Scanning is disabled in configuration.");
        }

        _isScanning = true;
        _currentScanLibraryId = libraryId;

        try
        {
            var library = _libraryManager.GetVirtualFolders()
                .FirstOrDefault(f => f.ItemId == libraryId.ToString());

            if (library == null)
            {
                throw new ArgumentException($"Library with ID {libraryId} not found.", nameof(libraryId));
            }

            _logger.LogInformation("Starting health scan for library: {LibraryName}", library.Name);

            var scanResult = new ScanResult(
                Guid.NewGuid(),
                libraryId,
                library.Name ?? "Unknown",
                DateTime.UtcNow);

            // Get all items in the library
            var query = new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Series, BaseItemKind.Episode },
                Recursive = true,
                ParentId = libraryId
            };

            var items = _libraryManager.GetItemList(query);
            scanResult.TotalItems = items.Count;

            _logger.LogDebug("Found {Count} items to scan in library {LibraryName}", items.Count, library.Name);

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await CheckItemAsync(item, library.Name ?? "Unknown", scanResult, config).ConfigureAwait(false);
            }

            scanResult.CompletedAt = DateTime.UtcNow;
            scanResult.IssuesFound = scanResult.Issues.Count;

            _dataStore.SaveScanResult(scanResult);

            _logger.LogInformation(
                "Completed health scan for library {LibraryName}: {TotalItems} items, {IssuesFound} issues",
                library.Name,
                scanResult.TotalItems,
                scanResult.IssuesFound);

            return scanResult;
        }
        finally
        {
            _isScanning = false;
            _currentScanLibraryId = Guid.Empty;
        }
    }

    private Task CheckItemAsync(BaseItem item, string libraryName, ScanResult scanResult, PluginConfiguration config)
    {
        // Check for missing poster
        if (config.CheckMissingPoster && !item.HasImage(ImageType.Primary))
        {
            AddIssue(scanResult, item, libraryName, IssueType.MissingPoster, IssueSeverity.Warning);
        }

        // Check for missing overview
        if (config.CheckMissingOverview && string.IsNullOrWhiteSpace(item.Overview))
        {
            AddIssue(scanResult, item, libraryName, IssueType.MissingOverview, IssueSeverity.Info);
        }

        // Check for missing year
        if (config.CheckMissingYear && !item.ProductionYear.HasValue)
        {
            AddIssue(scanResult, item, libraryName, IssueType.MissingYear, IssueSeverity.Warning);
        }

        // Check for missing genre
        if (config.CheckMissingGenre && (item.Genres == null || item.Genres.Length == 0))
        {
            AddIssue(scanResult, item, libraryName, IssueType.MissingGenre, IssueSeverity.Info);
        }

        // Check for missing subtitles (only for video items)
        if (config.CheckMissingSubtitles && item is Video video)
        {
            var mediaStreams = video.GetMediaStreams();
            var hasSubtitles = mediaStreams?.Any(s => s.Type == MediaStreamType.Subtitle) ?? false;
            if (!hasSubtitles)
            {
                AddIssue(scanResult, item, libraryName, IssueType.MissingSubtitles, IssueSeverity.Info);
            }
        }

        return Task.CompletedTask;
    }

    private void AddIssue(ScanResult scanResult, BaseItem item, string libraryName, IssueType type, IssueSeverity severity)
    {
        var issue = new HealthIssue(
            Guid.NewGuid(),
            item.Id,
            item.Name ?? "Unknown",
            libraryName,
            type,
            severity,
            DateTime.UtcNow);

        scanResult.Issues.Add(issue);

        _logger.LogDebug(
            "Found issue: {IssueType} for item {ItemName} in {LibraryName}",
            type,
            item.Name,
            libraryName);
    }

    /// <summary>
    /// Downloads forced English subtitles for an item.
    /// </summary>
    /// <param name="itemId">The item ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if subtitles were downloaded, false otherwise.</returns>
    public async Task<SubtitleDownloadResult> DownloadSubtitlesAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        var item = _libraryManager.GetItemById(itemId);
        if (item is not Video video)
        {
            return new SubtitleDownloadResult(false, "Item is not a video.");
        }

        _logger.LogInformation("Searching for forced English subtitles for: {ItemName}", item.Name);

        try
        {
            // Search for English subtitles (isPerfectMatch: true, isAutomated: true)
            var results = await _subtitleManager.SearchSubtitles(video, "eng", true, true, cancellationToken)
                .ConfigureAwait(false);

            // Filter for forced/foreign-parts-only subtitles by name
            var forcedSubtitle = results
                .Where(s => (s.Name?.Contains("forced", StringComparison.OrdinalIgnoreCase) ?? false) ||
                           (s.Name?.Contains("foreign", StringComparison.OrdinalIgnoreCase) ?? false))
                .OrderByDescending(s => s.CommunityRating ?? 0)
                .FirstOrDefault();

            if (forcedSubtitle == null)
            {
                _logger.LogInformation("No forced English subtitles found for: {ItemName}", item.Name);
                return new SubtitleDownloadResult(false, "No forced English subtitles found.");
            }

            _logger.LogInformation(
                "Downloading subtitle: {SubtitleName} for {ItemName}",
                forcedSubtitle.Name,
                item.Name);

            // Download the subtitle
            await _subtitleManager.DownloadSubtitles(video, forcedSubtitle.Id, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation("Successfully downloaded subtitles for: {ItemName}", item.Name);
            return new SubtitleDownloadResult(true, $"Downloaded: {forcedSubtitle.Name}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download subtitles for: {ItemName}", item.Name);
            return new SubtitleDownloadResult(false, $"Error: {ex.Message}");
        }
    }
}

/// <summary>
/// Result of a subtitle download attempt.
/// </summary>
public class SubtitleDownloadResult
{
    public SubtitleDownloadResult(bool success, string message)
    {
        Success = success;
        Message = message;
    }

    public bool Success { get; }
    public string Message { get; }
}
