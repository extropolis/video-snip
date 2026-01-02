# Video Snip

A lightweight, native Windows screen recording tool inspired by the Snipping Tool. Select a region or window and instantly start recording - no bloat, no complex UI, just quick video captures.

[![Build and Release](../../actions/workflows/build.yml/badge.svg)](../../actions/workflows/build.yml)
![Windows](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/license-MIT-green)

## Features

- **Full Screen Capture** - Record your entire screen with one click
- **Region Capture** - Drag to select any area of your screen
- **Window Capture** - Click on any window to record it
- **Ultrawide Monitor Support** - Works with 5K+ ultrawide displays (auto-scales for encoder compatibility)
- **Fixed Resolution Presets** - Choose from common resolutions (4K, 1080p, 720p, etc.) with live preview
- **Aspect Ratio Constraints** - Lock to 16:9, 4:3, 1:1, or 9:16
- **Pause/Resume** - Pause recording and resume when ready
- **Global Hotkeys** - Control recording from any application
- **Recording Border** - Visual red border shows the recording area
- **Minimal UI** - Clean, dark-themed toolbar that stays out of your way
- **Hardware Accelerated** - Uses H.264 encoding for efficient, high-quality output
- **No Audio** - Video-only recording (by design, for quick screen captures)

## Screenshots

*Coming soon*

## Requirements

### For Running (End Users)
- Windows 10 version 1803 (April 2018 Update) or later
- Windows 11 supported
- [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (x64)

### For Building (Developers)
- Windows 10/11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (x64)
- Visual Studio 2022 (optional, for IDE development)

## Installation

### From Release (Recommended)

1. Install the [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) if not already installed
2. Download the latest release from the [Releases](../../releases) page
3. Extract and run `VideoSnip.exe`

### Build from Source

#### Prerequisites

1. **Install .NET 8.0 SDK**
   - Download from: https://dotnet.microsoft.com/download/dotnet/8.0
   - Choose the **SDK** (not just Runtime) for your platform (x64)
   - Run the installer and follow the prompts
   - Verify installation:
     ```bash
     dotnet --version
     ```
     Should show `8.0.x` or higher

#### Build and Run

```bash
# Clone the repository
git clone https://github.com/yourusername/video-snip.git
cd video-snip

# Restore NuGet packages (dependencies are downloaded automatically)
dotnet restore

# Build in Debug mode
dotnet build -p:Platform=x64

# Or build in Release mode
dotnet build -c Release -p:Platform=x64

# Run the application
dotnet run --project VideoSnip -p:Platform=x64
```

#### Dependencies

All dependencies are managed via NuGet and will be automatically downloaded during the build:

| Package | Version | Purpose |
|---------|---------|---------|
| [ScreenRecorderLib](https://www.nuget.org/packages/ScreenRecorderLib) | 6.6.0 | Screen capture and H.264 video encoding |

#### Running Tests

```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test -v normal
```

#### Creating a Self-Contained Build

To create a standalone executable that doesn't require .NET to be installed:

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:Platform=x64
```

The output will be in `VideoSnip/bin/Release/net8.0-windows10.0.26100.0/win-x64/publish/`

## Usage

1. **Launch Video Snip** - A minimal toolbar appears
2. **Choose capture mode:**
   - Click **Window** to record a specific window
   - Click **Region** to drag-select an area
3. **Select resolution** (optional) - Pick a preset from the dropdown for fixed dimensions
4. **Make your selection:**
   - For Window mode: Click on the window you want to record
   - For Region mode: Drag to select an area (or click to place fixed-size region)
5. **Recording starts automatically** with a red border indicator
6. **Control recording:**
   - Click **Pause** to pause, **Resume** to continue
   - Click **Stop** or press `Space`/`Enter`/`Esc` to stop
7. **Save your video** - Choose a location in the save dialog

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `Ctrl+Shift+S` | Stop recording (global) |
| `Ctrl+Shift+P` | Pause/Resume recording (global) |
| `Space` / `Enter` / `Esc` | Stop recording (when app focused) |
| `Esc` | Cancel region/window selection |

## Resolution Presets

| Preset | Dimensions |
|--------|------------|
| Free | Drag to any size |
| 16:9 | Aspect ratio constraint |
| 4:3 | Aspect ratio constraint |
| 1:1 | Square aspect ratio |
| 9:16 | Vertical/mobile aspect ratio |
| 3840x2160 (4K) | Fixed 4K resolution |
| 1920x1080 | Fixed Full HD resolution |
| 1280x720 | Fixed HD resolution |
| 800x600 | Fixed resolution |

## Technical Details

- **Framework:** .NET 8.0 with WPF
- **Video Encoding:** H.264 via [ScreenRecorderLib](https://github.com/sskodje/ScreenRecorderLib)
- **Output Format:** MP4
- **Bitrate:** Adaptive (8-20 Mbps based on resolution)
- **Frame Rate:** 30 FPS (24 FPS for 4K+)
- **Max Resolution:** 4096px width (ultrawide monitors auto-scale)
- **Platform:** x64 only

## Project Structure

```
VideoSnip/
├── VideoSnip/
│   ├── App.xaml              # Application entry point
│   ├── MainWindow.xaml       # Main toolbar UI
│   ├── Helpers/
│   │   └── NativeMethods.cs  # Win32 API interop
│   ├── Models/
│   │   └── RecordingRegion.cs # Region and preset models
│   ├── Services/
│   │   ├── RecordingController.cs    # Recording orchestration
│   │   └── RecordingBorderService.cs # Recording border overlay
│   └── Views/
│       └── RegionSelector.xaml # Region/window selection overlay
├── VideoSnip.Tests/          # Unit tests
└── VideoSnip.sln
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [ScreenRecorderLib](https://github.com/sskodje/ScreenRecorderLib) - Screen recording library
- Inspired by Windows Snipping Tool

## Roadmap

- [x] System tray integration
- [x] Global hotkeys
- [x] Full screen capture
- [ ] Audio recording option
- [ ] GIF export
- [ ] Auto-save option
- [ ] Custom quality presets
