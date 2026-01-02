# Jellyfin Library Health Check Plugin

A Jellyfin plugin that scans your media libraries for common issues like missing metadata and subtitles, helping you maintain a healthy media collection.

## Features

- **Missing Poster Detection** - Find items without primary images
- **Missing Overview Detection** - Find items without descriptions
- **Missing Year Detection** - Find items without production year
- **Missing Genre Detection** - Find items without genres
- **Missing Subtitles Detection** - Find video items without subtitle tracks
- **Forced Subtitle Download** - One-click download of forced/foreign English subtitles via OpenSubtitles

## Installation

1. Download `LibraryHealthCheck.dll` from the [latest release](https://github.com/jyoung02/jellyfin-library-health-check/releases)
2. Copy to your Jellyfin plugins folder:
   - Windows: `C:\ProgramData\Jellyfin\Server\plugins\LibraryHealthCheck`
   - Linux: `/var/lib/jellyfin/plugins/LibraryHealthCheck`
   - Docker: `/config/plugins/LibraryHealthCheck`
3. Restart Jellyfin
4. Navigate to Dashboard > Plugins to verify installation

## Usage

1. Go to Dashboard > Library Health Check
2. Select a library from the dropdown
3. Click "Scan Library"
4. Review the results table showing all issues found
5. Click any item name to navigate to its Jellyfin page
6. For items missing subtitles, click "Get Subs" to download forced English subtitles

## Configuration

Navigate to Dashboard > Plugins > Library Health Check to configure:

- Enable/disable scanning
- Toggle individual checks (poster, overview, year, genre, subtitles)

## API Endpoints

All endpoints require authentication.

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/LibraryHealth/Libraries` | List available libraries |
| GET | `/LibraryHealth/Results` | Get all scan results |
| GET | `/LibraryHealth/Results/{libraryId}` | Get results for specific library |
| POST | `/LibraryHealth/Scan/{libraryId}` | Start a scan |
| GET | `/LibraryHealth/Status` | Check if scan is running |
| DELETE | `/LibraryHealth/Results/{libraryId}` | Delete scan results |
| POST | `/LibraryHealth/DownloadSubtitles/{itemId}` | Download forced English subtitles |

## Requirements

- Jellyfin 10.9.x
- For subtitle download: OpenSubtitles plugin must be installed and configured with an API key

## Issue Severity Levels

| Severity | Color | Description |
|----------|-------|-------------|
| Error | Red | Critical issues affecting playback |
| Warning | Orange | Missing important metadata (poster, year) |
| Info | Blue | Minor issues (missing description, genre, subtitles) |

## Building from Source

```bash
git clone https://github.com/jyoung02/jellyfin-library-health-check.git
cd jellyfin-library-health-check
dotnet build -c Release
```

The compiled DLL will be in `bin/Release/net8.0/`.

## License

MIT License
