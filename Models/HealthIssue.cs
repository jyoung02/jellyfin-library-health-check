using System;

namespace LibraryHealthCheck.Models;

/// <summary>
/// Severity level of a health issue.
/// </summary>
public enum IssueSeverity
{
    /// <summary>Informational issue.</summary>
    Info = 0,

    /// <summary>Warning - should be addressed.</summary>
    Warning = 1,

    /// <summary>Error - significant problem.</summary>
    Error = 2
}

/// <summary>
/// Type of health issue detected.
/// </summary>
public enum IssueType
{
    /// <summary>Item is missing a poster/primary image.</summary>
    MissingPoster = 0,

    /// <summary>Item is missing an overview/description.</summary>
    MissingOverview = 1,

    /// <summary>Item is missing a production year.</summary>
    MissingYear = 2,

    /// <summary>Item is missing genre information.</summary>
    MissingGenre = 3
}

/// <summary>
/// Represents a single health issue found in a library item.
/// </summary>
public sealed class HealthIssue
{
    /// <summary>
    /// Initializes a new instance for JSON deserialization.
    /// </summary>
    public HealthIssue()
    {
        ItemName = string.Empty;
        LibraryName = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthIssue"/> class.
    /// </summary>
    public HealthIssue(
        Guid id,
        Guid itemId,
        string itemName,
        string libraryName,
        IssueType type,
        IssueSeverity severity,
        DateTime detectedAt)
    {
        Id = id;
        ItemId = itemId;
        ItemName = itemName ?? string.Empty;
        LibraryName = libraryName ?? string.Empty;
        Type = type;
        Severity = severity;
        DetectedAt = detectedAt;
    }

    /// <summary>Gets the unique identifier for this issue.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the ID of the item with the issue.</summary>
    public Guid ItemId { get; init; }

    /// <summary>Gets the name of the item.</summary>
    public string ItemName { get; init; }

    /// <summary>Gets the name of the library containing the item.</summary>
    public string LibraryName { get; init; }

    /// <summary>Gets the type of issue.</summary>
    public IssueType Type { get; init; }

    /// <summary>Gets the severity of the issue.</summary>
    public IssueSeverity Severity { get; init; }

    /// <summary>Gets when the issue was detected.</summary>
    public DateTime DetectedAt { get; init; }
}
