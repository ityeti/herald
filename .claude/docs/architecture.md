# Herald Architecture

## Overview

Herald is a text-to-speech utility for Windows that:
1. Listens for global hotkeys
2. Grabs selected text, clipboard content, or OCR from screen regions
3. Speaks it aloud using neural voices (edge-tts) or offline voices (pyttsx3)
4. Provides playback controls and visual feedback via system tray

## Design Principles

- **Mirror whisper's approach** — Similar UX, systray-based, minimal UI
- **Swap-friendly TTS** — Abstract TTS engine so backends can be swapped
- **Graceful degradation** — Falls back to offline TTS when online fails
- **Verbal feedback** — Speaks errors aloud instead of silent failures

## Components

```
┌─────────────────────────────────────────────────────────────┐
│                        main.py                               │
│  - Entry point, main loop                                    │
│  - Hotkey registration (keyboard library)                    │
│  - Line queue management (next/prev navigation)              │
│  - Auto-read queue processing                                │
└────────────────────┬────────────────────────────────────────┘
                     │
    ┌────────────────┼────────────────┬───────────────────┐
    ▼                ▼                ▼                   ▼
┌──────────┐  ┌─────────────┐  ┌──────────────┐  ┌────────────┐
│ tray_app │  │ tts_engine  │  │  text_grab   │  │  region_   │
│          │  │             │  │              │  │  capture   │
│ - Icon   │  │ - EdgeTTS   │  │ - Clipboard  │  │            │
│ - Menu   │  │ - pyttsx3   │  │ - Auto-copy  │  │ - Overlay  │
│ - State  │  │ - Prefetch  │  │ - OCR        │  │ - Drawing  │
└──────────┘  │ - Errors    │  └──────────────┘  └────────────┘
              └─────────────┘
                     │
              ┌──────┴──────┐
              ▼             ▼
        ┌──────────┐  ┌──────────┐
        │ EdgeTTS  │  │ pyttsx3  │
        │ (online) │  │ (offline)│
        │          │  │          │
        │ - Aria   │  │ - Zira   │
        │ - Guy    │  │ - David  │
        │ - Jenny  │  │          │
        └──────────┘  └──────────┘
```

## TTS Engine Architecture

### EdgeTTSEngine (Primary)
- Uses Microsoft Azure neural voices via edge-tts library
- Generates MP3 files asynchronously, plays via pygame mixer
- Prefetches next line while current line plays
- Thread-safe with mixer lock

### Pyttsx3Engine (Fallback)
- Uses Windows SAPI voices (Zira, David)
- Synchronous, no file generation
- Used for offline mode and error announcements

### Error Handling Flow
```
EdgeTTS generation
       │
       ▼
  File exists? ──No──► _speak_error() ──► pyttsx3 ──► Windows beep
       │
      Yes
       │
       ▼
  Size > 0? ──No──► _speak_error("Audio generation failed...")
       │
      Yes
       │
       ▼
  pygame.mixer.music.play()
       │
       ▼
  Play failed? ──Yes──► _speak_error("Audio playback failed")
```

## File Structure

```
herald/
├── CLAUDE.md              # Project overview
├── README.md              # User documentation
├── Launch_Herald.bat      # UAC-elevated launcher
├── requirements.txt       # Dependencies
├── config/
│   └── settings.json      # User preferences (auto-created)
├── logs/
│   └── herald.log         # Application logs
├── temp/                   # Temporary MP3 files (auto-cleaned)
├── src/
│   ├── main.py            # Entry point, hotkeys, main loop
│   ├── tts_engine.py      # TTS abstraction layer
│   ├── text_grab.py       # Clipboard/selection/OCR
│   ├── region_capture.py  # Screen region selection overlay
│   ├── persistent_region.py # Continuous OCR monitoring
│   ├── tray_app.py        # System tray icon and menu
│   ├── text_filter.py     # Text cleanup for TTS (code, URLs, Claude output)
│   ├── config.py          # Settings management
│   └── utils.py           # Logging setup
├── tests/                  # pytest test suite
└── .claude/docs/
    ├── architecture.md    # This file
    ├── roadmap.md         # Features and known issues
    └── tts-options.md     # TTS engine research
```

## Threading Model

- **Main thread**: Hotkey callbacks, tray updates, line queue management
- **TTS thread**: Audio generation (edge-tts async) and playback monitoring
- **Prefetch thread**: Background generation of next line
- **Persistent region thread**: OCR polling for auto-read mode
- **Tray thread**: pystray icon event loop
- **Session monitor thread**: WTS session event listener (lock/unlock/RDP)

All pygame mixer operations are protected by `_mixer_lock` to prevent race conditions.

### Stability Mechanisms

- **Heartbeat monitor**: 60s interval checks queue length, speaking/paused state, mixer health, and hook thread liveness. Force-stops threads stuck for >60s.
- **Session event debounce**: RDP connect/disconnect fires rapid bursts of events. A 3s `threading.Timer` coalesces these into a single mixer reinit, preventing concurrent reinit storms.
- **Tray state tracking**: `_last_tray_state` variable prevents redundant icon updates. Only calls tray setters when state actually changes (idle/generating/speaking/paused), eliminating visual blink from 50ms polling.
- **Generation timeouts**: `asyncio.wait_for()` wraps edge-tts generation (30s) and prefetch (15s) to prevent indefinite hangs on network issues.
- **Single-instance mutex**: `CreateMutexW` with proper 64-bit `argtypes`/`restype` declarations prevents duplicate processes. Logs PID on startup.

## Settings Persistence

Settings stored in `config/settings.json`:
- `engine`: "edge" or "pyttsx3"
- `voice`: Voice name (aria, guy, jenny, christopher, zira, david)
- `rate`: Words per minute (50-1500)
- `hotkey_speak`: Speak hotkey (default: alt+s)
- `hotkey_pause`: Pause hotkey (default: alt+p)
- `line_delay`: Milliseconds between lines (default: 0)
- `read_mode`: "lines" or "continuous"
- `auto_copy`: Auto Ctrl+C before reading
- `ocr_to_clipboard`: Copy OCR results to clipboard
- `auto_read`: Auto-read when persistent region text changes
