# Jellyfin Library Health Check Plugin

## Overview
A Jellyfin plugin that scans media libraries for issues (missing metadata, duplicates, corrupt files) and displays a health report in the dashboard.

## Project Structure
```
LibraryHealthCheck/
├── LibraryHealthCheck.csproj
├── Plugin.cs
├── PluginServiceRegistrator.cs
├── Configuration/
│   ├── PluginConfiguration.cs
│   ├── configPage.html
│   └── healthPage.html
├── Models/
│   ├── HealthIssue.cs
│   └── ScanResult.cs
├── Services/
│   ├── DataStore.cs
│   └── LibraryScanner.cs
├── Api/
│   └── HealthCheckController.cs
└── Validation/
    └── InputSanitizer.cs
```

## Implemented Features

### v1.0.0 - Initial Release
- Missing poster/primary image detection
- Missing overview/description detection
- Missing year detection
- Missing genre detection
- Web UI with library selector and scan button
- Results table with severity indicators
- Click to navigate to Jellyfin item page

### v1.1.0 - Subtitle Check
- Missing subtitles detection for video items
- Configurable via settings

### v1.2.0 - Subtitle Download
- Download forced English subtitles for items missing subtitles
- Uses Jellyfin's ISubtitleManager to search OpenSubtitles
- Filters for "forced" or "foreign parts only" subtitles
- Fallback to regular English subtitles if no forced subs found
- Subtitle provider check with user warning if none configured
- Batch download for all items missing subtitles
- Progress tracking with live UI updates

## REST API

### Existing Endpoints
- `GET /LibraryHealth/Libraries` - List available libraries
- `GET /LibraryHealth/Results` - Get all scan results
- `GET /LibraryHealth/Results/{libraryId}` - Get results for specific library
- `POST /LibraryHealth/Scan/{libraryId}` - Trigger scan
- `GET /LibraryHealth/Status` - Check if scan is running
- `DELETE /LibraryHealth/Results/{libraryId}` - Delete scan results

### Subtitle Endpoints (v1.2.0)
- `POST /LibraryHealth/DownloadSubtitles/{itemId}` - Download subtitles for single item
- `GET /LibraryHealth/SubtitleProviders` - Check if subtitle providers are configured
- `POST /LibraryHealth/DownloadAllSubtitles/{libraryId}?forcedOnly=false` - Batch download
- `GET /LibraryHealth/BatchProgress` - Get batch download progress

## Future Features
- Duplicate detection
- Corrupt file detection (ffprobe)
- Resolution/codec statistics
- Scheduled automatic scans
- Configurable subtitle language preference

## Key Jellyfin APIs
- `ILibraryManager` - Access library items
- `ISubtitleManager` - Search and download subtitles
- `item.HasImage(ImageType.Primary)` - Check for poster
- `item.Overview` - Check for description
- `item.ProductionYear` - Check for year
- `item.Genres` - Check for genres
- `video.GetMediaStreams()` - Check for subtitle streams
