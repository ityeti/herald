# Herald Architecture

## Overview

Herald is a minimal text-to-speech utility that:
1. Listens for a global hotkey
2. Grabs selected text (or clipboard)
3. Speaks it aloud
4. Provides basic playback controls

## Design Principles

- **Mirror whisper's approach** — Similar UX, systray-based, minimal UI
- **Start simple** — MVP is just hotkey → speak clipboard
- **Swap-friendly TTS** — Abstract TTS engine so we can switch later

## Components

```
┌─────────────────────────────────────────────┐
│                  main.py                     │
│  - Entry point                               │
│  - System tray setup                         │
│  - Hotkey registration                       │
└─────────────────┬───────────────────────────┘
                  │
        ┌─────────┴─────────┐
        ▼                   ▼
┌───────────────┐   ┌───────────────┐
│  tts_engine.py │   │  text_grab.py  │
│  - TTS wrapper │   │  - Clipboard   │
│  - Rate control│   │  - Selection   │
│  - Pause/stop  │   │                │
└───────────────┘   └───────────────┘
```

## Planned Features (by phase)

### Phase 1: MVP
- [ ] Global hotkey triggers TTS
- [ ] Read from clipboard
- [ ] Adjustable speech rate
- [ ] Stop with Escape key

### Phase 2: Polish
- [ ] System tray icon
- [ ] Pause/resume
- [ ] Voice selection
- [ ] Settings persistence

### Phase 3: Stretch
- [ ] Click-to-read region (OCR)
- [ ] Screenshot text extraction
- [ ] Multiple voice profiles

## File Structure (planned)

```
herald/
├── CLAUDE.md
├── README.md
├── requirements.txt
├── src/
│   ├── main.py          # Entry point, tray, hotkeys
│   ├── tts_engine.py    # TTS abstraction
│   ├── text_grab.py     # Clipboard/selection handling
│   └── config.py        # Settings management
└── .claude/docs/
    ├── tts-options.md   # TTS research
    └── architecture.md  # This file
```
