# Changelog

All notable changes to Herald will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.2.0] - 2026-02-01

### Added
- **Persistent OCR region** (Alt+M) - Set a screen region with visible green border for continuous reading
- **Auto-read mode** - Automatically reads when text changes in persistent region (polls every 2.5s, triggers on 50%+ change)
- **One-time OCR region** (Alt+O) - Draw a box on screen to OCR and read immediately
- **Screenshot OCR** - Win+Shift+S screenshots are automatically OCR'd when you press Alt+S
- **Copy OCR to Clipboard** toggle in tray menu
- **Grab & Speak Selection** toggle (auto-copy when selecting text)
- Line-by-line navigation (Alt+N next, Alt+B back)
- Line delay setting (0-2000ms pause between lines)
- Read mode toggle (Line by Line vs Continuous)

### Changed
- Standalone executable now includes OCR helper tools (region_selector.exe, overlay_border.exe)
- Suppressed pygame pkg_resources deprecation warning (pygame internal issue)

### Fixed
- Keyboard hook issues that could freeze Windows hotkeys
- DPI awareness for correct multi-monitor coordinates
- Mutex not releasing properly on exit (added --force flag support)
- "Run loop already started" TTS error in auto-read mode

### Technical
- OCR helpers bundled as separate executables for PyInstaller compatibility
- Queue-based TTS to avoid asyncio conflicts from background threads

## [0.1.0] - 2026-01-30

### Added
- Neural voice support via edge-tts (Aria, Jenny, Guy, Christopher)
- Offline voice fallback via pyttsx3 (Zira, David)
- Global hotkey support (Alt+S to speak, Alt+P to pause)
- System tray interface with voice/speed/hotkey controls
- Configurable speech rate (150-1500 wpm)
- Pause/resume functionality (neural voices only)
- Settings persistence to config/settings.json
- Launch script with automatic UAC elevation
- Auto-start scripts for Windows startup integration
- Console show/hide functionality
- Visual feedback with tray icon states (idle/generating/speaking/paused)
- Speed adjustment hotkeys (Alt+] up, Alt+[ down)
- Stop hotkey (Escape)
- Quit hotkey (Alt+Q)
- Comprehensive README with troubleshooting guide
- MIT License

### Technical
- Python 3.10+ support
- Rotating log files in logs/herald.log
- Graceful error handling for internet connectivity
- Automatic engine selection based on voice choice

[Unreleased]: https://github.com/ityeti/herald/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/ityeti/herald/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/ityeti/herald/releases/tag/v0.1.0
