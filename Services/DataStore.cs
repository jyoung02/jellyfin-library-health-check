using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using LibraryHealthCheck.Models;
using Microsoft.Extensions.Logging;

namespace LibraryHealthCheck.Services;

/// <summary>
/// Handles persistence of scan results.
/// Uses file-based JSON storage with proper locking.
/// </summary>
public sealed class DataStore : IDisposable
{
    private readonly string _dataDirectory;
    private readonly ILogger<DataStore> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;

    private const string ScanResultsFile = "scan_results.json";

    /// <summary>
    /// Initializes a new instance of the <see cref="DataStore"/> class.
    /// </summary>
    public DataStore(string dataDirectory, ILogger<DataStore> logger)
    {
        _dataDirectory = ValidateDirectoryPath(dataDirectory);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        EnsureDirectoryExists();
    }

    /// <summary>
    /// Saves a scan result to storage.
    /// </summary>
    public void SaveScanResult(ScanResult result)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        _lock.Wait();
        try
        {
            var results = LoadScanResultsInternal();

            // Remove any existing result for this library
            results.RemoveAll(r => r.LibraryId == result.LibraryId);

            results.Add(result);
            SaveScanResultsInternal(results);
            _logger.LogDebug("Saved scan result {ResultId} for library {LibraryId}", result.Id, result.LibraryId);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Gets all scan results.
    /// </summary>
    public List<ScanResult> GetAllScanResults()
    {
        _lock.Wait();
        try
        {
            return LoadScanResultsInternal();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Gets scan result for a specific library.
    /// </summary>
    public ScanResult? GetScanResult(Guid libraryId)
    {
        _lock.Wait();
        try
        {
            var results = LoadScanResultsInternal();
            return results.FirstOrDefault(r => r.LibraryId == libraryId);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Deletes scan result for a library.
    /// </summary>
    public bool DeleteScanResult(Guid libraryId)
    {
        _lock.Wait();
        try
        {
            var results = LoadScanResultsInternal();
            var originalCount = results.Count;
            results.RemoveAll(r => r.LibraryId == libraryId);

            if (results.Count < originalCount)
            {
                SaveScanResultsInternal(results);
                _logger.LogInformation("Deleted scan result for library {LibraryId}", libraryId);
                return true;
            }

            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string ValidateDirectoryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Data directory path cannot be empty.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        if (fullPath.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("Invalid directory path.", nameof(path));
        }

        return fullPath;
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_dataDirectory))
        {
            Directory.CreateDirectory(_dataDirectory);
            _logger.LogInformation("Created data directory: {Directory}", _dataDirectory);
        }
    }

    private List<ScanResult> LoadScanResultsInternal()
    {
        var filePath = Path.Combine(_dataDirectory, ScanResultsFile);
        return LoadFromFile<List<ScanResult>>(filePath) ?? new List<ScanResult>();
    }

    private void SaveScanResultsInternal(List<ScanResult> results)
    {
        var filePath = Path.Combine(_dataDirectory, ScanResultsFile);
        SaveToFile(filePath, results);
    }

    private T? LoadFromFile<T>(string filePath) where T : class
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON from {FilePath}", filePath);
            return null;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to read file {FilePath}", filePath);
            return null;
        }
    }

    private void SaveToFile<T>(string filePath, T data)
    {
        var tempPath = filePath + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, filePath, overwrite: true);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to save file {FilePath}", filePath);
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
            throw;
        }
    }

    /// <summary>
    /// Disposes of resources.
    /// </summary>
    public void Dispose()
    {
        _lock.Dispose();
    }
}
