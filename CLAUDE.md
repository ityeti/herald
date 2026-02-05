# Herald

Text-to-speech utility for Windows — the inverse of [whisper-typer](../whisper-typer/). Press a hotkey, hear selected text read aloud.

## Session Start

**Read these before coding:**
1. `README.md` — User documentation, features, troubleshooting
2. `.claude/docs/tts-options.md` — TTS engine research

## Current Status

**Phase:** Feature-complete, public release
**GitHub:** https://github.com/ityeti/herald (public)
**Docs:** https://ityeti.com/herald/
**Release:** [v0.2.1](https://github.com/ityeti/herald/releases/tag/v0.2.1)

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

## Key Files

| File | Purpose |
|------|---------|
| `Launch_Herald.bat` | Main launcher (UAC auto-elevate) |
| `src/main.py` | Entry point, hotkey registration |
| `src/tts_engine.py` | TTS abstraction (edge-tts + pyttsx3) |
| `src/text_grab.py` | Clipboard handling + OCR |
| `src/region_capture.py` | Screen region selection overlay |
| `src/tray_app.py` | System tray icon and menu |
| `config/settings.json` | User preferences (auto-created) |

## Testing

```batch
:: Run all tests
test_runner.bat

:: Run only unit tests (fast)
test_runner.bat --unit
```

Cross-project TTS→STT validation tests are in `dev-oversight/tests/`.

## Default Hotkeys

| Hotkey | Action |
|--------|--------|
| Alt+S | Speak selection/clipboard (auto-copy, OCR images) |
| Alt+O | OCR region capture (one-time) |
| Alt+M | Toggle persistent OCR region (for PDFs/videos) |
| Alt+P | Pause/resume |
| Alt+N | Skip to next line |
| Alt+B | Go back to previous line |
| Alt+] | Speed up |
| Alt+[ | Slow down |
| Escape | Stop |
| Alt+Q | Quit |

## Settings

Saved to `config/settings.json`:
```json
{
  "engine": "edge",
  "voice": "aria",
  "rate": 500,
  "hotkey_speak": "alt+s",
  "hotkey_pause": "alt+p",
  "auto_copy": true,
  "auto_read": false
}
```

## Additional Documentation

| Topic | File | When to Read |
|-------|------|--------------|
| Architecture | `.claude/docs/architecture.md` | Understanding component design |
| TTS Options | `.claude/docs/tts-options.md` | TTS engine research and comparisons |
| Roadmap | `.claude/docs/roadmap.md` | Completed features, backlog, someday/maybe |

## Related

- [whisper-typer](../whisper-typer/) — Voice-to-text (input), herald is the inverse (output)
- [voxbox](../voxbox/) — Unified app combining both
- [dev-oversight](../dev-oversight/) — Project oversight and tracking
