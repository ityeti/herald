# Herald - Text-to-Speech

A text-to-speech utility for Windows that reads clipboard text aloud using high-quality neural voices. The inverse of [Whisper Voice-to-Text](https://github.com/ityeti/whisper-typer).

## Features

- **Neural Voices**: Microsoft Edge neural voices (Aria, Jenny, Guy, Christopher) via edge-tts
- **Offline Fallback**: Windows SAPI voices (Zira, David) when offline
- **Global Hotkeys**: Works in any application
- **System Tray**: Unobtrusive tray icon with menu controls
- **Pause/Resume**: Pause mid-speech and resume later
- **Adjustable Speed**: 50-600 words per minute
- **Settings Persistence**: Remembers your voice and speed preferences

## Requirements

- **OS**: Windows 10/11
- **Python**: 3.10, 3.11, or 3.12
- **Internet**: Required for neural voices (offline voices work without)

## Quick Start

### 1. Install Python

Download and install Python from [python.org](https://www.python.org/downloads/)

Make sure to check **"Add Python to PATH"** during installation.

### 2. Run the Application

Double-click **`Launch_Herald.bat`** - it will:
- Request administrator privileges (required for global hotkeys)
- Create a virtual environment (first run only)
- Install dependencies (first run only)
- Launch the application

**That's it!** On subsequent runs, it starts immediately.

### 3. Usage

1. Copy text to your clipboard (Ctrl+C)
2. Press **Alt+S** to hear it read aloud
3. Press **Alt+P** to pause/resume
4. Press **Escape** to stop
5. Right-click the tray icon to change voice or speed

## Hotkeys

| Hotkey | Action |
|--------|--------|
| Alt+S | Speak clipboard text |
| Alt+P | Pause/resume |
| Alt+] | Speed up |
| Alt+[ | Slow down |
| Escape | Stop speaking |
| Alt+Q | Quit application |

## System Tray Menu

Right-click the tray icon to access:

- **Voice (Online)**: Aria, Jenny, Guy, Christopher (neural voices, requires internet)
- **Voice (Offline)**: Zira, David (Windows SAPI voices, no internet needed)
- **Speed**: Preset speeds (200, 350, 500, 600 wpm)
- **Pause/Resume**: Toggle when speaking
- **Console**: Show or hide the console window
- **Quit**: Exit the application

## Configuration

Settings are saved to `config/settings.json`:

```json
{
  "engine": "edge",
  "voice": "aria",
  "rate": 500
}
```

| Setting | Options | Description |
|---------|---------|-------------|
| engine | edge, pyttsx3 | TTS engine (auto-selected based on voice) |
| voice | aria, jenny, guy, christopher, zira, david | Voice name |
| rate | 50-600 | Words per minute |

## Available Voices

### Online (edge-tts)

| Voice | Description |
|-------|-------------|
| aria | Female, conversational |
| jenny | Female, news anchor |
| guy | Male, friendly |
| christopher | Male, professional |

### Offline (pyttsx3/SAPI)

| Voice | Description |
|-------|-------------|
| zira | Female, Windows default |
| david | Male, Windows default |

## Troubleshooting

### "No text to speak"
- Make sure you've copied text to the clipboard (Ctrl+C)
- Some applications use special clipboard formats; try copying from Notepad

### Neural voices not working
- Check your internet connection
- The app will fall back to offline voices if edge-tts fails

### Hotkeys not working
- Ensure the application is running as administrator
- Check for conflicting hotkeys in other applications

### Pause doesn't work
- Pause only works with neural voices (edge-tts)
- Offline voices (pyttsx3) don't support true pause

## Project Structure

```
herald/
├── Launch_Herald.bat     # Double-click to run
├── requirements.txt      # Python dependencies
├── config/
│   └── settings.json     # Your settings (auto-created)
├── logs/
│   └── herald.log        # Application logs
└── src/
    ├── main.py           # Application entry point
    ├── tts_engine.py     # TTS engine abstraction
    ├── tray_app.py       # System tray icon
    ├── text_grab.py      # Clipboard handling
    ├── config.py         # Settings management
    └── utils.py          # Logging setup
```

## Related Projects

- [Whisper Voice-to-Text](https://github.com/ityeti/whisper-typer) - The inverse: speech-to-text

## License

MIT License - feel free to use and modify.
