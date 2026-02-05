# Roadmap

Feature tracking and version history for Herald.

## Release Status

- [x] Review code for sensitive info
- [x] Test fresh install on clean system
- [x] Make repo public
- [x] v0.1.0 released with standalone exe
- [x] v0.2.0 released with OCR support
- [x] SEO docs live at https://ityeti.com/herald/

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
- [x] Verbal error alerts (speaks errors via offline fallback when edge-tts fails)
- [x] Diagnostic logging for audio generation and playback

## Backlog

- [ ] Add "OCR" label back to region selection window (upper left corner) for clarity

## Someday/Maybe

- Configurable auto-read settings via tray menu (interval + text change threshold, currently hardcoded 2.5s / 50%)
- Voice profiles — save/load voice+speed combos for quick switching (e.g., "Reading" = Aria 400wpm, "Skim" = Guy 700wpm)
- MP3 export — save TTS output as audio file (personal use only due to edge-tts voice licensing)
- Position memory for ebook-style reading experience (low priority)
- Unified desktop app with Whisper-typer — see `c:\dev\dev-oversight\.claude\docs\someday-maybe.md` for details

## Known Issues

- If keyboard gets stuck (rare), close Herald to release hooks
- **pygame pkg_resources warning**: Pygame uses deprecated setuptools API internally. Suppressed in main.py with warning filter. Will be fixed when pygame updates to importlib.resources. See: https://github.com/pygame/pygame/issues
- **Edge-tts silent failures**: Network issues can cause edge-tts to generate 0-byte MP3 files without throwing exceptions. Fixed in v0.2.1+ with file validation and verbal error alerts using pyttsx3 fallback.

## Troubleshooting

### No audio plays but tray shows "speaking"
1. Check `logs/herald.log` for error messages
2. Look for "Edge TTS generated empty file" - indicates network/API issue
3. Herald will now speak "Audio generation failed" via offline voice when this happens
4. Try again - usually transient network issue with Microsoft's edge-tts API
