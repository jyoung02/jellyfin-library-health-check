using System;
using System.Collections.Generic;

namespace LibraryHealthCheck.Models;

/// <summary>
/// Represents the result of a library health scan.
/// </summary>
public sealed class ScanResult
{
    /// <summary>
    /// Initializes a new instance for JSON deserialization.
    /// </summary>
    public ScanResult()
    {
        LibraryName = string.Empty;
        Issues = new List<HealthIssue>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ScanResult"/> class.
    /// </summary>
    public ScanResult(
        Guid id,
        Guid libraryId,
        string libraryName,
        DateTime startedAt)
    {
        Id = id;
        LibraryId = libraryId;
        LibraryName = libraryName ?? string.Empty;
        StartedAt = startedAt;
        Issues = new List<HealthIssue>();
    }

    /// <summary>Gets the unique identifier for this scan result.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the ID of the library that was scanned.</summary>
    public Guid LibraryId { get; init; }

    /// <summary>Gets the name of the library.</summary>
    public string LibraryName { get; init; }

    /// <summary>Gets when the scan started.</summary>
    public DateTime StartedAt { get; init; }

    /// <summary>Gets or sets when the scan completed.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Gets or sets the total number of items scanned.</summary>
    public int TotalItems { get; set; }

    /// <summary>Gets or sets the number of issues found.</summary>
    public int IssuesFound { get; set; }

    /// <summary>Gets or sets the list of issues found.</summary>
    public List<HealthIssue> Issues { get; set; }

    /// <summary>Gets a value indicating whether the scan is complete.</summary>
    public bool IsComplete => CompletedAt.HasValue;
}
