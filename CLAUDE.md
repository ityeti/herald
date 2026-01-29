# Herald

Text-to-speech utility for Windows — the inverse of [whisper](../whisper-typer/). Press a hotkey, hear selected text read aloud.

## Session Start

**Read these before coding:**
1. `.claude/docs/tts-options.md` — TTS engine research and recommendations
2. `.claude/docs/architecture.md` — Component design and planned file structure

## Current Status

**Phase:** MVP Complete (tested and working)

**Decisions made:**
- TTS engine: **pyttsx3** for MVP (simple, offline) — can swap to edge-tts later for better voices
- Hotkey library: **keyboard** (`keyboard.add_hotkey()`)
- Hotkeys: **Alt+S** speak, **Alt+[/]** speed, **Escape** stop, **Alt+Q** quit
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
| src/tts_engine.py | TTS abstraction layer |
| src/text_grab.py | Clipboard/selection handling |
| src/config.py | Settings and constants |
| src/utils.py | Logging setup |
| logs/ | Application logs (herald.log) |
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

## Next Steps (Phase 2)

- [x] Speed up/slow down hotkeys (Alt+[ / Alt+]) with audio feedback
- [x] File logging (loguru → logs/herald.log)
- [x] Quit hotkey (Alt+Q)
- [ ] Module self-tests (`if __name__ == "__main__":`)
- [ ] System tray icon with controls
- [ ] Pause/resume functionality
- [ ] Voice selection
- [ ] Settings persistence (JSON config)

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
