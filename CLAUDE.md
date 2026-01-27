# Herald

Text-to-speech utility for Windows — the inverse of [whisper](../whisper/). Press a hotkey, hear selected text read aloud.

## Tech Stack

| Component | Choice | Notes |
|-----------|--------|-------|
| Language | Python | Matches whisper, shared tooling |
| TTS Engine | TBD | Options: SAPI, pyttsx3, edge-tts |
| Hotkey | TBD | Global hotkey library needed |
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

## MVP Scope

1. Global hotkey (e.g., Ctrl+Alt+Shift+R) triggers TTS
2. Reads clipboard or selected text
3. Adjustable speech rate
4. System tray icon with basic controls (pause/stop)

## Additional Documentation

| Topic | File | When to Read |
|-------|------|--------------|
| TTS Research | `.claude/docs/tts-options.md` | Choosing TTS engine |
| Architecture | `.claude/docs/architecture.md` | Understanding design decisions |

## Related

- [whisper](../whisper/) — Voice-to-text (input), herald is the inverse (output)
- [dev-oversight BACKLOG](../dev-oversight/BACKLOG.md) — Project tracked there
