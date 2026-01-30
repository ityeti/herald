# Changelog

All notable changes to Herald will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Initial public release preparation

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

[Unreleased]: https://github.com/ityeti/herald/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/ityeti/herald/releases/tag/v0.1.0
