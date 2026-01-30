# Herald

Text-to-speech utility for Windows — the inverse of [whisper](../whisper-typer/). Press a hotkey, hear selected text read aloud.

## Session Start

**Read these before coding:**
1. `.claude/docs/tts-options.md` — TTS engine research and recommendations
2. `.claude/docs/architecture.md` — Component design and planned file structure

## Current Status

**Phase:** MVP Complete (tested and working)

**Decisions made:**
- TTS engines: **edge-tts** (default, neural voices) + **pyttsx3** (offline fallback)
- Hotkey library: **keyboard** (`keyboard.add_hotkey()`)
- Hotkeys: **Alt+S** speak, **Alt+P** pause, **Alt+[/]** speed, **Escape** stop, **Alt+Q** quit
- System tray: **pystray** with voice/speed/console controls
- Logging: **loguru** → `logs/herald.log` (10MB rotation, 7 day retention)

## Tech Stack

| Component | Choice | Notes |
|-----------|--------|-------|
| Language | Python 3.10+ | Matches whisper, shared tooling |
| TTS Engine | pyttsx3 | Simple wrapper, works offline |
| Hotkey | keyboard | `pip install keyboard` |
| Clipboard | pyperclip | Cross-platform clipboard access |
| GUI | Minimal/systray | Like whisper's approach (Phase 2) |

## Key Directories

| Directory | Purpose |
|-----------|---------|
| src/ | Main application code |
| src/main.py | Entry point, hotkey registration |
| src/tts_engine.py | TTS abstraction (edge-tts + pyttsx3) |
| src/tray_app.py | System tray icon and menu |
| src/text_grab.py | Clipboard/selection handling |
| src/config.py | Settings management |
| src/utils.py | Logging setup |
| config/ | User settings (settings.json) |
| logs/ | Application logs (herald.log) |
| temp/ | Temporary audio files (edge-tts) |
| .claude/docs/ | Research and design docs |

## Essential Commands

```bash
# Recommended: Double-click Launch_Herald.bat (auto-elevates, auto-installs)

# Manual setup (if needed)
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt
python src/main.py   # requires admin terminal
```

## MVP Tasks

- [x] Set up project structure (`src/`, `requirements.txt`)
- [x] Implement global hotkey listener (keyboard library)
- [x] Implement TTS engine wrapper (pyttsx3)
- [x] Implement clipboard text grabbing
- [x] Add stop functionality (Escape key)
- [x] UAC-elevating launcher (`Launch_Herald.bat`)
- [x] Test on Windows (Python 3.12)

## MVP Scope

1. Global hotkey (Alt+S) triggers TTS
2. Reads clipboard text
3. Configurable speech rate (in config.py)
4. Stop with Escape key
5. Double-click launcher with auto-setup

## Completed Features

- [x] Speed up/slow down hotkeys (Alt+[ / Alt+]) with audio feedback
- [x] File logging (loguru → logs/herald.log)
- [x] Quit hotkey (Alt+Q)
- [x] Settings persistence (config/settings.json)
- [x] Voice selection via tray menu
- [x] System tray icon with controls
- [x] Pause/resume functionality (Alt+P)
- [x] edge-tts integration (Aria, Jenny, Guy, Christopher)
- [x] Console show/hide via tray menu

## Remaining Tasks

- [ ] Module self-tests (`if __name__ == "__main__":`)
- [ ] README for public GitHub release
- [ ] OCR for images (someday/maybe)
- [ ] Double-click to read selection (someday/maybe)

## Additional Documentation

| Topic | File | When to Read |
|-------|------|--------------|
| TTS Research | `.claude/docs/tts-options.md` | Choosing TTS engine |
| Architecture | `.claude/docs/architecture.md` | Understanding design decisions |

## Tracking

**Central backlog:** [../dev-oversight/BACKLOG.md](../dev-oversight/BACKLOG.md)

When completing significant work, update the Herald section in the dev-oversight BACKLOG.

## Related

- [whisper-typer](../whisper-typer/) — Voice-to-text (input), herald is the inverse (output)
- [dev-oversight](../dev-oversight/) — Project oversight and tracking
