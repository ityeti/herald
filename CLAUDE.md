# Herald

Text-to-speech utility for Windows — the inverse of [whisper](../whisper-typer/). Press a hotkey, hear selected text read aloud.

## Session Start

**Read these before coding:**
1. `.claude/docs/tts-options.md` — TTS engine research and recommendations
2. `.claude/docs/architecture.md` — Component design and planned file structure

## Current Status

**Phase:** MVP Implementation (scaffolding complete)

**Decisions made:**
- TTS engine: **pyttsx3** for MVP (simple, offline) — can swap to edge-tts later for better voices
- Hotkey library: **keyboard** (`keyboard.add_hotkey()`)
- Default hotkey: **Ctrl+Alt+Shift+R**
- Stop hotkey: **Escape**

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
| .claude/docs/ | Research and design docs |

## Essential Commands

```bash
# Create virtual environment
python -m venv venv
venv\Scripts\activate

# Install dependencies
pip install -r requirements.txt

# Run (requires admin for global hotkeys)
python src/main.py
```

## MVP Tasks

- [x] Set up project structure (`src/`, `requirements.txt`)
- [x] Implement global hotkey listener (keyboard library)
- [x] Implement TTS engine wrapper (pyttsx3)
- [x] Implement clipboard text grabbing
- [x] Add stop functionality (Escape key)
- [ ] Add adjustable speech rate UI/hotkeys
- [ ] Test and debug on Windows

## MVP Scope

1. Global hotkey (Ctrl+Alt+Shift+R) triggers TTS
2. Reads clipboard text
3. Configurable speech rate (in config.py)
4. Stop with Escape key

## Next Steps (Phase 2)

- [ ] System tray icon with controls
- [ ] Pause/resume functionality
- [ ] Speed up/slow down hotkeys
- [ ] Voice selection
- [ ] Settings persistence

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
