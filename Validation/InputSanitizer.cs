using System;
using System.Text;
using System.Text.RegularExpressions;

namespace LibraryHealthCheck.Validation;

/// <summary>
/// Provides input validation and sanitization utilities.
/// </summary>
public static partial class InputSanitizer
{
    private static readonly Regex DangerousPatterns = CreateDangerousPatternRegex();

    /// <summary>
    /// Validates that a GUID is not empty.
    /// </summary>
    public static Guid ValidateGuid(Guid input, string paramName)
    {
        if (input == Guid.Empty)
        {
            throw new ArgumentException($"{paramName} cannot be empty.", paramName);
        }

        return input;
    }

    /// <summary>
    /// Validates and parses a string as a GUID.
    /// </summary>
    public static Guid ValidateGuid(string input, string paramName)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException($"{paramName} cannot be empty.", paramName);
        }

        if (!Guid.TryParse(input, out var result) || result == Guid.Empty)
        {
            throw new ArgumentException($"{paramName} is not a valid GUID.", paramName);
        }

        return result;
    }

    /// <summary>
    /// Sanitizes a string by removing dangerous patterns and limiting length.
    /// </summary>
    public static string SanitizeString(string? input, int maxLength)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        // Normalize unicode
        var normalized = input.Normalize(NormalizationForm.FormC);

        // Remove control characters except common whitespace
        var cleaned = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (!char.IsControl(c) || c == '\n' || c == '\r' || c == '\t')
            {
                cleaned.Append(c);
            }
        }

        var result = cleaned.ToString();

        // Check for dangerous patterns
        if (DangerousPatterns.IsMatch(result))
        {
            throw new ArgumentException("Input contains potentially dangerous content.");
        }

        // Enforce maximum length
        if (result.Length > maxLength)
        {
            result = result[..maxLength];
        }

        return result.Trim();
    }

    /// <summary>
    /// Validates a DateTime is within reasonable bounds.
    /// </summary>
    public static DateTime ValidateDateTime(DateTime input, string paramName)
    {
        if (input == default)
        {
            throw new ArgumentException($"{paramName} cannot be default.", paramName);
        }

        var minDate = new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var maxDate = DateTime.UtcNow.AddMinutes(5);

        if (input < minDate || input > maxDate)
        {
            throw new ArgumentException($"{paramName} is outside valid range.", paramName);
        }

        return DateTime.SpecifyKind(input, DateTimeKind.Utc);
    }

    [GeneratedRegex(@"<script|javascript:|data:|onclick|onerror|\.\.\/|\.\.\\|\x00", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CreateDangerousPatternRegex();
}
