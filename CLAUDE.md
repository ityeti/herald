# Herald

Text-to-speech utility for Windows — the inverse of [whisper](../whisper/). Press a hotkey, hear selected text read aloud.

## Session Start

**Read these before coding:**
1. `.claude/docs/tts-options.md` — TTS engine research and recommendations
2. `.claude/docs/architecture.md` — Component design and planned file structure

## Current Status

**Phase:** Ready to implement MVP (research complete)

**Decisions made:**
- TTS engine: **pyttsx3** for MVP (simple, offline) — can swap to edge-tts later for better voices
- Hotkey library: **keyboard** (`keyboard.add_hotkey()`)
- Default hotkey: **Ctrl+Alt+Shift+R**

## Tech Stack

| Component | Choice | Notes |
|-----------|--------|-------|
| Language | Python | Matches whisper, shared tooling |
| TTS Engine | pyttsx3 | Simple wrapper, works offline |
| Hotkey | keyboard | `pip install keyboard` |
| GUI | Minimal/systray | Like whisper's approach |

## Key Directories

| Directory | Purpose |
|-----------|---------|
| src/ | Main application code |
| .claude/docs/ | Research and design docs |

## Essential Commands

```bash
# Install dependencies (once setup)
pip install -r requirements.txt

# Run
python src/main.py
```

## MVP Tasks

- [ ] Set up project structure (`src/`, `requirements.txt`)
- [ ] Implement global hotkey listener (keyboard library)
- [ ] Implement TTS engine wrapper (pyttsx3)
- [ ] Implement clipboard/selection text grabbing
- [ ] Add adjustable speech rate
- [ ] Add stop functionality (Escape key)

## MVP Scope

1. Global hotkey (Ctrl+Alt+Shift+R) triggers TTS
2. Reads clipboard or selected text
3. Adjustable speech rate
4. Stop with Escape key

## Additional Documentation

| Topic | File | When to Read |
|-------|------|--------------|
| TTS Research | `.claude/docs/tts-options.md` | Choosing TTS engine |
| Architecture | `.claude/docs/architecture.md` | Understanding design decisions |

## Related

- [whisper](../whisper/) — Voice-to-text (input), herald is the inverse (output)
- [dev-oversight BACKLOG](../dev-oversight/BACKLOG.md) — Project tracked there
