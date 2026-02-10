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
test_runner.bat          :: All tests
test_runner.bat unit     :: Unit tests only (fast)
test_runner.bat coverage :: Coverage report
test_runner.bat fast     :: All except slow tests
```

**Baseline coverage:** 5% total (93% config, 23% text_filter — most modules need tests)

Cross-project TTS→STT validation tests are in `dev-oversight/tests/`.

When modifying code in this project:
1. Run existing tests first to confirm baseline passes
2. For bug fixes: write a failing test that reproduces the bug, then fix
3. For new features: write tests alongside or immediately after implementation
4. Run tests after changes to catch regressions
5. Do not reduce existing test coverage

## Default Hotkeys

All hotkeys are configurable via the tray menu and `config/settings.json`.

| Hotkey | Action |
|--------|--------|
| Ctrl+Shift+S | Speak selection/clipboard (auto-copy, OCR images) |
| Ctrl+Shift+O | OCR region capture (one-time) |
| Ctrl+Shift+M | Toggle persistent OCR region (for PDFs/videos) |
| Ctrl+Shift+P | Pause/resume |
| Ctrl+Shift+N | Skip to next line |
| Ctrl+Shift+B | Go back to previous line |
| Ctrl+Shift+] | Speed up |
| Ctrl+Shift+[ | Slow down |
| Escape | Stop |
| Ctrl+Shift+Q | Quit |

## Settings

Saved to `config/settings.json`:
```json
{
  "engine": "edge",
  "voice": "aria",
  "rate": 500,
  "hotkey_speak": "ctrl+shift+s",
  "hotkey_pause": "ctrl+shift+p",
  "hotkey_stop": "escape",
  "hotkey_speed_up": "ctrl+shift+]",
  "hotkey_speed_down": "ctrl+shift+[",
  "hotkey_next": "ctrl+shift+n",
  "hotkey_prev": "ctrl+shift+b",
  "hotkey_ocr": "ctrl+shift+o",
  "hotkey_monitor": "ctrl+shift+m",
  "hotkey_quit": "ctrl+shift+q",
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
