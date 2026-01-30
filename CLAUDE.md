# Herald

Text-to-speech utility for Windows — the inverse of [whisper](../whisper-typer/). Press a hotkey, hear selected text read aloud.

## Session Start

**Read these before coding:**
1. `README.md` — User documentation, features, troubleshooting
2. `.claude/docs/tts-options.md` — TTS engine research

## Current Status

**Phase:** Feature-complete, ready for public release

**GitHub:** https://github.com/ityeti/herald (currently private)

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
| `src/tray_app.py` | System tray icon and menu |
| `src/config.py` | Settings management |
| `config/settings.json` | User preferences (auto-created) |

## Default Hotkeys

| Hotkey | Action | Configurable |
|--------|--------|--------------|
| Alt+S | Speak clipboard | Yes (tray menu) |
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
  "hotkey_pause": "alt+p"
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

## Next Steps (for public release)

- [x] Review code for sensitive info
- [x] Test fresh install on clean system
- [ ] Make repo public

## Someday/Maybe

- OCR for images/screenshots
- Double-click text to read selection
- More hotkey options (speed, voice switching)

## Related

- [whisper-typer](../whisper-typer/) — Voice-to-text (input), herald is the inverse (output)
- [dev-oversight](../dev-oversight/) — Project oversight and tracking
