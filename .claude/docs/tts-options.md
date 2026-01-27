# TTS Options Research

Research into text-to-speech engines for Windows.

## Options Evaluated

### 1. Windows SAPI (System.Speech / SAPI.SpVoice)

**Pros:**
- Built into Windows, no dependencies
- Works offline
- Simple COM interface: `ComObjCreate("SAPI.SpVoice")`
- Can list/switch voices via registry

**Cons:**
- Voice quality is dated (unless newer Windows voices installed)
- Limited voice options out of box

**Python usage:**
```python
import win32com.client
speaker = win32com.client.Dispatch("SAPI.SpVoice")
speaker.Speak("Hello world")
```

### 2. pyttsx3

**Pros:**
- Cross-platform Python wrapper
- Works offline
- Supports SAPI on Windows, nsss on Mac, espeak on Linux
- Easy rate/volume control

**Cons:**
- Essentially wraps SAPI on Windows (same voice quality)
- Additional dependency

**Python usage:**
```python
import pyttsx3
engine = pyttsx3.init()
engine.setProperty('rate', 150)
engine.say("Hello world")
engine.runAndWait()
```

### 3. edge-tts

**Pros:**
- High-quality Microsoft Azure neural voices
- Many voices/languages (same as Edge browser read-aloud)
- Free (uses Edge's TTS endpoint)
- Async support

**Cons:**
- Requires internet connection
- Technically uses Microsoft's service (may have rate limits)
- Outputs to file, needs playback handling

**Python usage:**
```python
import edge_tts
import asyncio

async def speak(text):
    communicate = edge_tts.Communicate(text, "en-US-AriaNeural")
    await communicate.save("output.mp3")
    # Then play the file

asyncio.run(speak("Hello world"))
```

### 4. Azure Cognitive Services

**Pros:**
- Production-grade neural voices
- Official API with SLA

**Cons:**
- Requires Azure subscription
- Costs money after free tier
- Overkill for personal utility

## Recommendation

**Start with pyttsx3** for MVP:
- Simple, works offline, good enough quality
- Easy to swap in edge-tts later for better voices

**Consider edge-tts** if voice quality matters:
- Significantly better than SAPI voices
- Worth the internet dependency for natural sound

## Hotkey Libraries

For global hotkeys on Windows:

| Library | Notes |
|---------|-------|
| `keyboard` | Simple, `keyboard.add_hotkey('ctrl+alt+r', func)` |
| `pynput` | More control, cross-platform |
| `global-hotkeys` | Lightweight alternative |

**Recommendation:** Start with `keyboard` library for simplicity.

## Sources

- [AutoHotkey TTS examples](https://www.autohotkey.com/boards/viewtopic.php?f=76&t=92937)
- [edge-tts GitHub](https://github.com/rany2/edge-tts)
- [pyttsx3 documentation](https://pyttsx3.readthedocs.io/)
