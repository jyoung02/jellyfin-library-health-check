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
    private volatile bool _isDownloadingBatch;
    private BatchSubtitleProgress? _batchProgress;

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
    /// Gets a value indicating whether a batch subtitle download is in progress.
    /// </summary>
    public bool IsDownloadingBatch => _isDownloadingBatch;

    /// <summary>
    /// Gets the current batch download progress.
    /// </summary>
    public BatchSubtitleProgress? BatchProgress => _batchProgress;

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
    /// Checks if subtitle providers are configured in Jellyfin.
    /// </summary>
    /// <returns>True if at least one provider is available.</returns>
    public bool HasSubtitleProviders()
    {
        try
        {
            var providers = _subtitleManager.GetSupportedProviders(new Video());
            return providers != null && providers.Length > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check subtitle providers");
            return false;
        }
    }

    /// <summary>
    /// Downloads English subtitles for an item, preferring forced/foreign-only subtitles.
    /// Falls back to regular English subtitles if no forced subtitles are found.
    /// </summary>
    /// <param name="itemId">The item ID.</param>
    /// <param name="forcedOnly">If true, only download forced subtitles (no fallback).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success/failure and whether fallback was used.</returns>
    public async Task<SubtitleDownloadResult> DownloadSubtitlesAsync(
        Guid itemId,
        bool forcedOnly = false,
        CancellationToken cancellationToken = default)
    {
        var item = _libraryManager.GetItemById(itemId);
        if (item is not Video video)
        {
            return new SubtitleDownloadResult(false, "Item is not a video.");
        }

        // Check if subtitle providers are configured
        if (!HasSubtitleProviders())
        {
            return new SubtitleDownloadResult(
                false,
                "No subtitle providers configured. Install OpenSubtitles plugin in Jellyfin.");
        }

        _logger.LogInformation("Searching for English subtitles for: {ItemName}", item.Name);

        try
        {
            // Search for English subtitles (isPerfectMatch: true, isAutomated: true)
            var results = await _subtitleManager.SearchSubtitles(video, "eng", true, true, cancellationToken)
                .ConfigureAwait(false);

            if (results == null || results.Length == 0)
            {
                _logger.LogInformation("No English subtitles found for: {ItemName}", item.Name);
                return new SubtitleDownloadResult(false, "No English subtitles found.");
            }

            // Try to find forced/foreign-parts-only subtitles first
            var forcedSubtitle = results
                .Where(s => (s.Name?.Contains("forced", StringComparison.OrdinalIgnoreCase) ?? false) ||
                           (s.Name?.Contains("foreign", StringComparison.OrdinalIgnoreCase) ?? false) ||
                           (s.Name?.Contains("sdh", StringComparison.OrdinalIgnoreCase) == false &&
                            s.Name?.Contains("hi", StringComparison.OrdinalIgnoreCase) == false))
                .Where(s => s.Name?.Contains("forced", StringComparison.OrdinalIgnoreCase) == true ||
                           s.Name?.Contains("foreign", StringComparison.OrdinalIgnoreCase) == true)
                .OrderByDescending(s => s.CommunityRating ?? 0)
                .FirstOrDefault();

            if (forcedSubtitle != null)
            {
                _logger.LogInformation(
                    "Downloading forced subtitle: {SubtitleName} for {ItemName}",
                    forcedSubtitle.Name,
                    item.Name);

                await _subtitleManager.DownloadSubtitles(video, forcedSubtitle.Id, cancellationToken)
                    .ConfigureAwait(false);

                return new SubtitleDownloadResult(true, $"Downloaded forced: {forcedSubtitle.Name}", usedFallback: false);
            }

            // No forced subtitles found - check if we should fall back
            if (forcedOnly)
            {
                _logger.LogInformation("No forced subtitles found for: {ItemName} (fallback disabled)", item.Name);
                return new SubtitleDownloadResult(false, "No forced English subtitles found.");
            }

            // Fallback: get best regular English subtitle
            var regularSubtitle = results
                .OrderByDescending(s => s.CommunityRating ?? 0)
                .ThenByDescending(s => s.DownloadCount ?? 0)
                .FirstOrDefault();

            if (regularSubtitle == null)
            {
                _logger.LogInformation("No English subtitles available for: {ItemName}", item.Name);
                return new SubtitleDownloadResult(false, "No English subtitles found.");
            }

            _logger.LogInformation(
                "Downloading fallback subtitle: {SubtitleName} for {ItemName}",
                regularSubtitle.Name,
                item.Name);

            await _subtitleManager.DownloadSubtitles(video, regularSubtitle.Id, cancellationToken)
                .ConfigureAwait(false);

            return new SubtitleDownloadResult(
                true,
                $"Downloaded (fallback): {regularSubtitle.Name}",
                usedFallback: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download subtitles for: {ItemName}", item.Name);
            return new SubtitleDownloadResult(false, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Downloads subtitles for all items with missing subtitles in a library.
    /// </summary>
    /// <param name="libraryId">The library ID to process.</param>
    /// <param name="forcedOnly">If true, only download forced subtitles.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Batch progress with results.</returns>
    public async Task<BatchSubtitleProgress> DownloadAllSubtitlesAsync(
        Guid libraryId,
        bool forcedOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (_isDownloadingBatch)
        {
            throw new InvalidOperationException("A batch download is already in progress.");
        }

        // Check for subtitle providers first
        if (!HasSubtitleProviders())
        {
            return new BatchSubtitleProgress
            {
                Total = 0,
                IsRunning = false,
                Results = new List<BatchSubtitleItemResult>
                {
                    new BatchSubtitleItemResult
                    {
                        Success = false,
                        Message = "No subtitle providers configured. Install OpenSubtitles plugin in Jellyfin."
                    }
                }
            };
        }

        _isDownloadingBatch = true;
        _batchProgress = new BatchSubtitleProgress { IsRunning = true };

        try
        {
            // Get the last scan result for this library
            var scanResult = _dataStore.GetScanResult(libraryId);
            if (scanResult == null)
            {
                return new BatchSubtitleProgress
                {
                    Total = 0,
                    IsRunning = false,
                    Results = new List<BatchSubtitleItemResult>
                    {
                        new BatchSubtitleItemResult
                        {
                            Success = false,
                            Message = "No scan results found. Run a scan first."
                        }
                    }
                };
            }

            // Get items with missing subtitles
            var missingSubtitleIssues = scanResult.Issues
                .Where(i => i.Type == IssueType.MissingSubtitles)
                .ToList();

            _batchProgress.Total = missingSubtitleIssues.Count;

            _logger.LogInformation(
                "Starting batch subtitle download for {Count} items in library {LibraryId}",
                missingSubtitleIssues.Count,
                libraryId);

            foreach (var issue in missingSubtitleIssues)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _batchProgress.CurrentItem = issue.ItemName;

                var result = await DownloadSubtitlesAsync(issue.ItemId, forcedOnly, cancellationToken)
                    .ConfigureAwait(false);

                var itemResult = new BatchSubtitleItemResult
                {
                    ItemId = issue.ItemId,
                    ItemName = issue.ItemName,
                    Success = result.Success,
                    Message = result.Message,
                    UsedFallback = result.UsedFallback
                };

                _batchProgress.Results.Add(itemResult);
                _batchProgress.Completed++;

                if (result.Success)
                {
                    _batchProgress.Succeeded++;
                }
                else if (result.Message.Contains("No") && result.Message.Contains("found"))
                {
                    _batchProgress.NoSubtitlesFound++;
                }
                else
                {
                    _batchProgress.Failed++;
                }

                _logger.LogDebug(
                    "Batch progress: {Completed}/{Total} - {ItemName}: {Success}",
                    _batchProgress.Completed,
                    _batchProgress.Total,
                    issue.ItemName,
                    result.Success);
            }

            _batchProgress.IsRunning = false;
            _batchProgress.CurrentItem = null;

            _logger.LogInformation(
                "Batch subtitle download complete: {Succeeded} succeeded, {Failed} failed, {NotFound} not found",
                _batchProgress.Succeeded,
                _batchProgress.Failed,
                _batchProgress.NoSubtitlesFound);

            return _batchProgress;
        }
        finally
        {
            _isDownloadingBatch = false;
            if (_batchProgress != null)
            {
                _batchProgress.IsRunning = false;
            }
        }
    }
}

/// <summary>
/// Result of a subtitle download attempt.
/// </summary>
public class SubtitleDownloadResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubtitleDownloadResult"/> class.
    /// </summary>
    /// <param name="success">Whether the download was successful.</param>
    /// <param name="message">Result message.</param>
    /// <param name="usedFallback">Whether fallback subtitles were used.</param>
    public SubtitleDownloadResult(bool success, string message, bool usedFallback = false)
    {
        Success = success;
        Message = message;
        UsedFallback = usedFallback;
    }

    /// <summary>Gets a value indicating whether the download was successful.</summary>
    public bool Success { get; }

    /// <summary>Gets the result message.</summary>
    public string Message { get; }

    /// <summary>Gets a value indicating whether fallback (non-forced) subtitles were used.</summary>
    public bool UsedFallback { get; }
}

/// <summary>
/// Progress tracking for batch subtitle downloads.
/// </summary>
public class BatchSubtitleProgress
{
    /// <summary>Gets or sets the total number of items to process.</summary>
    public int Total { get; set; }

    /// <summary>Gets or sets the number of completed items.</summary>
    public int Completed { get; set; }

    /// <summary>Gets or sets the number of successful downloads.</summary>
    public int Succeeded { get; set; }

    /// <summary>Gets or sets the number of failed downloads.</summary>
    public int Failed { get; set; }

    /// <summary>Gets or sets the number of items where no subtitles were found.</summary>
    public int NoSubtitlesFound { get; set; }

    /// <summary>Gets or sets a value indicating whether the batch is still running.</summary>
    public bool IsRunning { get; set; }

    /// <summary>Gets or sets the name of the current item being processed.</summary>
    public string? CurrentItem { get; set; }

    /// <summary>Gets or sets the list of individual item results.</summary>
    public List<BatchSubtitleItemResult> Results { get; set; } = new();
}

/// <summary>
/// Result for a single item in batch download.
/// </summary>
public class BatchSubtitleItemResult
{
    /// <summary>Gets or sets the item ID.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Gets or sets the item name.</summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the download was successful.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets the result message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether fallback subtitles were used.</summary>
    public bool UsedFallback { get; set; }
}
