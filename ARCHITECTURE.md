# Cloud Music Player - Architecture

## Overview

Cloud Music Player is a cross-platform music player built with .NET MAUI that streams and caches audio files from Google Drive. It targets Android, iOS, and Windows.

**Target Framework:** .NET 9.0 (`net9.0-android`, `net9.0-ios`, `net9.0-windows10.0.19041.0`)

## Project Structure

```
src/CloudMusicPlayer/
├── App.xaml/.cs                    # Application entry point, theme initialization
├── AppShell.xaml/.cs               # Shell navigation (tab bar + route registration)
├── MauiProgram.cs                  # DI container configuration
├── Constants.cs                    # Google API credentials, paths, defaults
│
├── Models/                         # Data models
│   ├── AudioTrack.cs               # Song metadata (SQLite entity)
│   ├── Playlist.cs                 # Playlist (SQLite entity)
│   ├── PlaylistTrack.cs            # Playlist-Track junction (SQLite entity)
│   ├── CachedFile.cs               # Cache tracking (SQLite entity)
│   ├── DriveFileItem.cs            # Google Drive file/folder DTO
│   ├── EqualizerPreset.cs          # Equalizer preset definition
│   └── SavedFolder.cs              # User-saved folder reference
│
├── Services/
│   ├── Interfaces/                 # Service contracts (11 interfaces)
│   ├── GoogleAuthService.cs        # OAuth2 PKCE authentication
│   ├── GoogleDriveService.cs       # Drive API: list folders, download files
│   ├── AudioPlaybackService.cs     # Playback engine: queue, shuffle, repeat
│   ├── StreamingProxyService.cs    # Local HTTP proxy for streaming playback
│   ├── CacheService.cs             # Download + LRU cache management
│   ├── MetadataService.cs          # TagLibSharp metadata/album art extraction
│   ├── DatabaseService.cs          # SQLite CRUD operations
│   ├── PlaylistService.cs          # Playlist management
│   ├── FavoritesService.cs         # Favorites toggle
│   └── SearchService.cs            # Local DB search
│
├── ViewModels/                     # MVVM ViewModels (1 per page)
│   ├── BaseViewModel.cs            # Shared: IsBusy, Title, IsEmpty, error handling
│   ├── LoginViewModel.cs
│   ├── FolderBrowserViewModel.cs
│   ├── LibraryViewModel.cs
│   ├── NowPlayingViewModel.cs
│   ├── SearchViewModel.cs
│   ├── PlaylistsViewModel.cs
│   ├── PlaylistDetailViewModel.cs
│   ├── EqualizerViewModel.cs
│   └── SettingsViewModel.cs
│
├── Views/                          # XAML pages (1 per ViewModel)
│   ├── LoginPage.xaml              # Google sign-in
│   ├── FolderBrowserPage.xaml      # Drive folder navigation with breadcrumbs
│   ├── LibraryPage.xaml            # Saved folders and track listing
│   ├── NowPlayingPage.xaml         # Full-screen player with controls
│   ├── SearchPage.xaml             # Search by title/artist/album
│   ├── PlaylistsPage.xaml          # Playlist listing
│   ├── PlaylistDetailPage.xaml     # Tracks within a playlist
│   ├── EqualizerPage.xaml          # Band sliders + presets
│   └── SettingsPage.xaml           # Cache, theme, sign-out
│
├── Controls/
│   └── MiniPlayerControl.xaml      # Persistent mini-player at bottom of shell
│
├── Converters/                     # XAML value converters
│   ├── BoolToIconConverter.cs
│   ├── FileSizeConverter.cs
│   ├── InverseBoolConverter.cs
│   ├── PlayPauseConverter.cs
│   └── TimeSpanToStringConverter.cs
│
└── Platforms/
    ├── Android/
    │   ├── AndroidManifest.xml                 # Permissions: INTERNET, FOREGROUND_SERVICE
    │   ├── WebAuthenticatorCallbackActivity.cs # OAuth2 redirect handler
    │   └── Services/AndroidEqualizerService.cs # Android.Media.Audiofx.Equalizer
    ├── iOS/
    │   ├── Info.plist                          # UIBackgroundModes: audio
    │   └── Services/iOSEqualizerService.cs     # Stub (not supported in v1)
    └── Windows/
        ├── Package.appxmanifest                # internetClient capability
        └── Services/WindowsEqualizerService.cs # Stub (not supported in v1)
```

## Architecture Pattern: MVVM

The application follows the **Model-View-ViewModel** pattern using `CommunityToolkit.Mvvm`.

```
View (XAML) ──binds to──> ViewModel ──calls──> Service ──uses──> Model/DB
```

- **Views** contain no business logic; they bind to ViewModel properties and commands
- **ViewModels** extend `BaseViewModel` (which provides `IsBusy`, `Title`, `IsEmpty`, and `ExecuteAsync` error wrapper)
- **Services** are registered as singletons via DI in `MauiProgram.cs` and injected into ViewModels via constructor injection
- **Models** are SQLite entities decorated with `[PrimaryKey]`, `[AutoIncrement]`, and `[Ignore]` attributes

## Navigation

Shell-based navigation with 4 tabs and 4 detail routes:

```
AppShell (TabBar)
├── Tab: Library     → LibraryPage
├── Tab: Search      → SearchPage
├── Tab: Playlists   → PlaylistsPage
└── Tab: Settings    → SettingsPage

Detail Routes (push navigation):
├── folderbrowser    → FolderBrowserPage
├── nowplaying       → NowPlayingPage
├── playlistdetail   → PlaylistDetailPage
└── equalizer        → EqualizerPage
```

The login page is shown conditionally before the shell when no auth token is stored.

## Key Subsystems

### 1. Authentication (GoogleAuthService)

```
User → OAuth2 PKCE → Google → Access Token → SecureStorage
```

- **Android/iOS:** Uses `WebAuthenticator` for system browser redirect
- **Windows:** Starts a local `HttpListener` on a random port, opens the browser, and captures the authorization code via localhost callback
- Tokens are persisted in `SecureStorage` and auto-refreshed on expiry

### 2. Audio Playback Pipeline

```
Google Drive → StreamingProxyService → MediaElement → Audio Output
                    ↓ (async)
              CacheService → Local File (LRU)
```

Two playback paths:

1. **Cached:** If the file is already in the local cache, `MediaElement` plays directly from `file://` URI
2. **Streaming:** If not cached, `StreamingProxyService` (a local HTTP server) fetches the file from Google Drive, streams it to `MediaElement` via `http://localhost:{port}/`, and simultaneously writes it to disk for future cache hits

**AudioPlaybackService** manages:
- Play queue (original + shuffled variants)
- Shuffle mode (Fisher-Yates algorithm, current track stays at index 0)
- Repeat modes: None, One, All
- Auto-advance on track end
- Pre-caching of the next track in the background
- Background metadata extraction after playback starts

### 3. Cache Management (CacheService)

```
LRU Cache (default 2GB)
├── Download with semaphore (max 3 concurrent)
├── Track by DriveFileId in SQLite CachedFile table
├── Evict oldest-accessed files when limit exceeded
└── User can clear or resize via SettingsPage
```

- Cache directory: `{AppData}/cache/music/`
- Files are named `{DriveFileId}.{extension}`
- `CachedFile` SQLite table tracks: path, size, cached-at, last-accessed timestamps
- LRU eviction runs before each download if total size + new file > limit

### 4. Metadata Extraction (MetadataService)

- Uses **TagLibSharp** to read ID3/Vorbis/FLAC tags from cached files
- Extracts: title, artist, album, track number, duration
- Album art is extracted and saved as a separate image file for display
- Runs asynchronously after playback begins so there is no startup delay

### 5. Database (DatabaseService)

SQLite database with 4 tables:

| Table | Purpose |
|-------|---------|
| `AudioTrack` | Song metadata, file references, play stats |
| `Playlist` | User-created playlists |
| `PlaylistTrack` | M:N junction between Playlist and AudioTrack |
| `CachedFile` | LRU cache tracking |

- Thread-safe initialization via `SemaphoreSlim` (double-check locking)
- All operations are async via `sqlite-net-pcl`'s `SQLiteAsyncConnection`

### 6. Equalizer (Platform-Specific)

- **Android:** Full 5-band equalizer using `Android.Media.Audiofx.Equalizer`, with presets (Rock, Pop, Jazz, Classical, Bass Boost, etc.)
- **iOS / Windows:** Stub services returning "not supported" (MediaElement on these platforms does not expose audio processing APIs)

## Data Flow Diagrams

### Playing a Track

```
User taps track
    → ViewModel calls PlaybackService.PlayAsync(tracks, index)
        → SetQueue(tracks) + PlayCurrentTrackAsync()
            → CacheService.GetCachedFilePathAsync(driveFileId)
            → [Cached?]
                Yes → MediaElement.Source = file://path
                No  → StreamingProxy.GetStreamUrl(id, ext)
                      MediaElement.Source = http://localhost:port/stream/...
            → MediaElement.Play()
            → Background: ExtractMetadata + PreCacheNextTrack
```

### Browsing Google Drive

```
User navigates to FolderBrowserPage
    → ViewModel calls DriveService.GetFilesAsync(folderId)
        → Google Drive API: files.list(parents=folderId)
        → Filter: folders + audio MIME types
        → Display in ListView with breadcrumb navigation
    → User selects folder → "Add to Library"
        → Scan all audio files recursively
        → Save as AudioTrack records in SQLite
        → Add to saved_folders_v2 in Preferences
```

## Dependency Injection Map

All services are registered as **singletons** in `MauiProgram.cs`:

```
IDatabaseService        → DatabaseService
IGoogleAuthService      → GoogleAuthService
IGoogleDriveService     → GoogleDriveService
ICacheService           → CacheService
IStreamingProxyService  → StreamingProxyService
IMetadataService        → MetadataService
IAudioPlaybackService   → AudioPlaybackService
IPlaylistService        → PlaylistService
IFavoritesService       → FavoritesService
ISearchService          → SearchService
IEqualizerService       → AndroidEqualizerService (Android)
                        → iOSEqualizerService (iOS)
                        → WindowsEqualizerService (Windows)
```

ViewModels and Pages are registered as **transient**.

## Audio Format Support

| Format | Android | iOS | Windows |
|--------|---------|-----|---------|
| MP3    | OK      | OK  | OK      |
| AAC    | OK      | OK  | OK      |
| WAV    | OK      | OK  | OK      |
| FLAC   | OK      | OK (iOS 11+) | OK |
| OGG    | OK      | Skip + notify | Skip + notify |
| WMA    | Varies  | Skip + notify | OK |

Unsupported formats are skipped with a user notification.

## Configuration

Key settings stored in `Preferences`:

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `dark_theme` | bool | false | Dark/light theme toggle |
| `cache_size_limit` | long | 2 GB | Maximum cache disk usage |
| `saved_folders_v2` | JSON string | [] | List of saved Google Drive folders |
| `google_auth_token` | (SecureStorage) | - | OAuth2 token (encrypted) |
