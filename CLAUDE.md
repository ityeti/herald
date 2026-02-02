# Herald

Text-to-speech utility for Windows — the inverse of [whisper](../whisper-typer/). Press a hotkey, hear selected text read aloud.

## Session Start

**Read these before coding:**
1. `README.md` — User documentation, features, troubleshooting
2. `.claude/docs/tts-options.md` — TTS engine research

## Current Status

**Phase:** Feature-complete, ready for public release

**GitHub:** https://github.com/ityeti/herald (public)
**Docs:** https://ityeti.com/herald/
**Release:** [v0.2.0](https://github.com/ityeti/herald/releases/tag/v0.2.0)

## Tech Stack

| Component | Choice | Notes |
|-----------|--------|-------|
| Language | Python 3.12 | Tested and working |
| TTS (online) | edge-tts | Neural voices: Aria, Jenny, Guy, Christopher |
| TTS (offline) | pyttsx3 | Windows SAPI: Zira, David |
| Audio playback | pygame | For edge-tts MP3 playback |
| Hotkeys | keyboard | Global hotkeys, admin required |
| System tray | pystray + Pillow | Voice/speed/hotkey controls |
| Clipboard | pyperclip | Text grabbing |
| Logging | loguru | Rotating file logs |

## Key Files

| File | Purpose |
|------|---------|
| `Launch_Herald.bat` | Main launcher (UAC auto-elevate) |
| `Autostart_Enable.bat` | Add to Windows startup |
| `Autostart_Disable.bat` | Remove from startup |
| `src/main.py` | Entry point, hotkey registration |
| `src/tts_engine.py` | TTS abstraction (edge-tts + pyttsx3) |
| `src/text_grab.py` | Clipboard handling + OCR |
| `src/region_capture.py` | Screen region selection overlay |
| `src/tray_app.py` | System tray icon and menu |
| `src/config.py` | Settings management |
| `config/settings.json` | User preferences (auto-created) |

## Default Hotkeys

| Hotkey | Action | Configurable |
|--------|--------|--------------|
| Alt+S | Speak selection/clipboard (auto-copy, OCR images) | Yes (tray menu) |
| Alt+O | OCR region capture (one-time) | No |
| Alt+M | Toggle persistent OCR region (for PDFs/videos) | No |
| Alt+P | Pause/resume | Yes (tray menu) |
| Alt+N | Skip to next line | No |
| Alt+B | Go back to previous line | No |
| Alt+] | Speed up | No |
| Alt+[ | Slow down | No |
| Escape | Stop | No |
| Alt+Q | Quit | No |

## Settings

Saved to `config/settings.json`:
```json
{
  "engine": "edge",
  "voice": "aria",
  "rate": 500,
  "hotkey_speak": "alt+s",
  "hotkey_pause": "alt+p",
  "line_delay": 0,
  "read_mode": "lines",
  "log_preview": true,
  "auto_copy": true,
  "ocr_to_clipboard": true,
  "auto_read": false
}
```

## Completed Features

- [x] Neural voices via edge-tts (Aria, Jenny, Guy, Christopher)
- [x] Offline fallback via pyttsx3 (Zira, David)
- [x] Speed adjustment (150-900 wpm online, up to 1500 wpm offline)
- [x] Pause/resume (edge-tts only)
- [x] System tray with full controls
- [x] Configurable hotkeys (speak, pause)
- [x] Settings persistence
- [x] Auto-start scripts
- [x] Console show/hide
- [x] Visual feedback (tray icon states: idle/generating/speaking/paused)
- [x] README documentation
- [x] Line-by-line navigation (Alt+N/B to skip/back)
- [x] Prefetch next line while current plays (edge-tts)
- [x] Configurable delay between lines (tray menu)
- [x] Auto-copy selection (just select text and press Alt+S)
- [x] OCR for clipboard images (Win+Shift+S screenshots)
- [x] OCR region capture mode (Alt+O to draw box and read)
- [x] Copy OCR'd text to clipboard (toggle in tray menu)
- [x] Persistent OCR region (Alt+M) for PDFs and videos
- [x] Auto-read mode (polls region, reads when text changes 50%+)

## Release Status

- [x] Review code for sensitive info
- [x] Test fresh install on clean system
- [x] Make repo public
- [x] v0.1.0 released with standalone exe
- [x] SEO docs live at https://ityeti.com/herald/

## Someday/Maybe

- Configurable auto-read settings via tray menu (interval + text change threshold, currently hardcoded 2.5s / 50%)
- Voice profiles — save/load voice+speed combos for quick switching (e.g., "Reading" = Aria 400wpm, "Skim" = Guy 700wpm)
- MP3 export — save TTS output as audio file (personal use only due to edge-tts voice licensing)
- Position memory for ebook-style reading experience (low priority)
- Unified desktop app with Whisper-typer — see `c:\dev\dev-oversight\.claude\docs\someday-maybe.md` for details

## Backlog

- [ ] Add "OCR" label back to region selection window (upper left corner) for clarity

## Known Issues

- If keyboard gets stuck (rare), close Herald to release hooks
- **pygame pkg_resources warning**: Pygame uses deprecated setuptools API internally. Suppressed in main.py with warning filter. Will be fixed when pygame updates to importlib.resources. See: https://github.com/pygame/pygame/issues

## Related

- [whisper-typer](../whisper-typer/) — Voice-to-text (input), herald is the inverse (output)
- [dev-oversight](../dev-oversight/) — Project oversight and tracking
