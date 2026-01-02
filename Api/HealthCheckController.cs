using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using LibraryHealthCheck.Models;
using LibraryHealthCheck.Services;
using LibraryHealthCheck.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LibraryHealthCheck.Api;

/// <summary>
/// API controller for library health check operations.
/// </summary>
[ApiController]
[Route("LibraryHealth")]
[Authorize]
[Produces(MediaTypeNames.Application.Json)]
public class HealthCheckController : ControllerBase
{
    private readonly LibraryScanner _libraryScanner;
    private readonly DataStore _dataStore;
    private readonly ILogger<HealthCheckController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthCheckController"/> class.
    /// </summary>
    public HealthCheckController(
        LibraryScanner libraryScanner,
        DataStore dataStore,
        ILogger<HealthCheckController> logger)
    {
        _libraryScanner = libraryScanner ?? throw new ArgumentNullException(nameof(libraryScanner));
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all available libraries.
    /// </summary>
    [HttpGet("Libraries")]
    [ProducesResponseType(typeof(List<LibraryInfo>), StatusCodes.Status200OK)]
    public ActionResult<List<LibraryInfo>> GetLibraries()
    {
        _logger.LogInformation("API request: GetLibraries");

        var libraries = _libraryScanner.GetLibraries()
            .Select(f => new LibraryInfo
            {
                Id = f.ItemId,
                Name = f.Name ?? "Unknown",
                CollectionType = f.CollectionType?.ToString() ?? "unknown"
            })
            .ToList();

        return Ok(libraries);
    }

    /// <summary>
    /// Gets all scan results.
    /// </summary>
    [HttpGet("Results")]
    [ProducesResponseType(typeof(List<ScanResult>), StatusCodes.Status200OK)]
    public ActionResult<List<ScanResult>> GetAllResults()
    {
        _logger.LogInformation("API request: GetAllResults");
        var results = _dataStore.GetAllScanResults();
        return Ok(results);
    }

    /// <summary>
    /// Gets scan result for a specific library.
    /// </summary>
    [HttpGet("Results/{libraryId}")]
    [ProducesResponseType(typeof(ScanResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ScanResult> GetLibraryResult([FromRoute] string libraryId)
    {
        var validatedId = ValidateLibraryIdParameter(libraryId);
        if (validatedId == null)
        {
            return BadRequest("Invalid library ID format.");
        }

        _logger.LogInformation("API request: GetLibraryResult for {LibraryId}", validatedId);

        var result = _dataStore.GetScanResult(validatedId.Value);
        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// Starts a scan for a specific library.
    /// </summary>
    [HttpPost("Scan/{libraryId}")]
    [ProducesResponseType(typeof(ScanResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ScanResult>> StartScan(
        [FromRoute] string libraryId,
        CancellationToken cancellationToken)
    {
        var validatedId = ValidateLibraryIdParameter(libraryId);
        if (validatedId == null)
        {
            return BadRequest("Invalid library ID format.");
        }

        if (_libraryScanner.IsScanning)
        {
            return Conflict("A scan is already in progress.");
        }

        _logger.LogInformation("API request: StartScan for {LibraryId}", validatedId);

        try
        {
            var result = await _libraryScanner.ScanLibraryAsync(validatedId.Value, cancellationToken)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    /// Gets the current scan status.
    /// </summary>
    [HttpGet("Status")]
    [ProducesResponseType(typeof(ScanStatus), StatusCodes.Status200OK)]
    public ActionResult<ScanStatus> GetStatus()
    {
        _logger.LogDebug("API request: GetStatus");

        var status = new ScanStatus
        {
            IsScanning = _libraryScanner.IsScanning,
            CurrentLibraryId = _libraryScanner.CurrentScanLibraryId != Guid.Empty
                ? _libraryScanner.CurrentScanLibraryId.ToString()
                : null
        };

        return Ok(status);
    }

    /// <summary>
    /// Deletes scan results for a library.
    /// </summary>
    [HttpDelete("Results/{libraryId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult DeleteResult([FromRoute] string libraryId)
    {
        var validatedId = ValidateLibraryIdParameter(libraryId);
        if (validatedId == null)
        {
            return BadRequest("Invalid library ID format.");
        }

        _logger.LogInformation("API request: DeleteResult for {LibraryId}", validatedId);

        var deleted = _dataStore.DeleteScanResult(validatedId.Value);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    private Guid? ValidateLibraryIdParameter(string? libraryId)
    {
        if (string.IsNullOrWhiteSpace(libraryId))
        {
            _logger.LogWarning("Empty library ID received");
            return null;
        }

        try
        {
            var sanitized = InputSanitizer.SanitizeString(libraryId, 50);
            return InputSanitizer.ValidateGuid(sanitized, "libraryId");
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid library ID format: {LibraryId}", libraryId);
            return null;
        }
    }
}

/// <summary>
/// Library information DTO.
/// </summary>
public class LibraryInfo
{
    /// <summary>Gets or sets the library ID.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the library name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the collection type.</summary>
    public string CollectionType { get; set; } = string.Empty;
}

/// <summary>
/// Scan status DTO.
/// </summary>
public class ScanStatus
{
    /// <summary>Gets or sets a value indicating whether a scan is in progress.</summary>
    public bool IsScanning { get; set; }

    /// <summary>Gets or sets the current library ID being scanned.</summary>
    public string? CurrentLibraryId { get; set; }
}
