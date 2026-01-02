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

### v1.2.0 - Subtitle Download (In Progress)
- Download forced English subtitles for items missing subtitles
- Uses Jellyfin's ISubtitleManager to search OpenSubtitles
- Filters for "forced" or "foreign parts only" subtitles
- Matches content by hash/metadata

## REST API

### Existing Endpoints
- `GET /LibraryHealth/Libraries` - List available libraries
- `GET /LibraryHealth/Results` - Get all scan results
- `GET /LibraryHealth/Results/{libraryId}` - Get results for specific library
- `POST /LibraryHealth/Scan/{libraryId}` - Trigger scan
- `GET /LibraryHealth/Status` - Check if scan is running
- `DELETE /LibraryHealth/Results/{libraryId}` - Delete scan results

### New Endpoints (v1.2.0)
- `POST /LibraryHealth/DownloadSubtitles/{itemId}` - Download forced English subtitles

## Future Features
- Duplicate detection
- Corrupt file detection (ffprobe)
- Resolution/codec statistics
- Scheduled automatic scans
- Batch subtitle download for all missing items

## Key Jellyfin APIs
- `ILibraryManager` - Access library items
- `ISubtitleManager` - Search and download subtitles
- `item.HasImage(ImageType.Primary)` - Check for poster
- `item.Overview` - Check for description
- `item.ProductionYear` - Check for year
- `item.Genres` - Check for genres
- `video.GetMediaStreams()` - Check for subtitle streams
