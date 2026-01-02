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
│   ├── LibraryScanner.cs
│   └── ScanScheduler.cs (IScheduledTask)
├── Api/
│   └── HealthCheckController.cs
└── Validation/
    └── InputSanitizer.cs
```

## MVP Features (Phase 1)
1. **Missing Metadata Detection**
   - No poster/primary image
   - No overview/description
   - No year
   - No genre

2. **Basic UI**
   - Library selector dropdown
   - "Scan Now" button
   - Results table: Item, Issue Type, Severity
   - Click to open item in Jellyfin

3. **REST API**
   - `GET /LibraryHealth/Results` - Get latest scan results
   - `POST /LibraryHealth/Scan/{libraryId}` - Trigger scan
   - `GET /LibraryHealth/Status` - Check if scan is running

## Future Features (Phase 2+)
- Duplicate detection
- Missing subtitles
- Corrupt file detection (ffprobe)
- Resolution/codec statistics
- Scheduled automatic scans
- Severity thresholds in config

## Implementation Order
1. Create project structure and csproj
2. Create Plugin.cs and PluginServiceRegistrator.cs
3. Create Models (HealthIssue, ScanResult)
4. Create DataStore (copy pattern from Invoice plugin)
5. Create LibraryScanner service
6. Create HealthCheckController API
7. Create healthPage.html UI
8. Create configPage.html (enable/disable, thresholds)
9. Build and test

## Key Jellyfin APIs
- `ILibraryManager.GetItemList()` - Get all items
- `item.HasImage(ImageType.Primary)` - Check for poster
- `item.Overview` - Check for description
- `item.ProductionYear` - Check for year
- `item.Genres` - Check for genres
