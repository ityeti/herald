# Herald Packaging

This folder contains scripts to build Herald as a standalone Windows executable.

## Quick Build

```batch
cd C:\dev\herald
packaging\build.bat
```

Output will be in `dist\Herald\`.

## Requirements

- Python 3.10+ with venv (run `Launch_Herald.bat` first to set up)
- PyInstaller (installed automatically by build script)

## Output Structure

```
dist/Herald/
├── Herald.exe           # Main executable (requests admin)
├── Launch_Herald.bat    # Optional launcher
├── config/              # Settings folder (user-writable)
│   └── settings.example.json
├── logs/                # Log folder (user-writable)
└── [DLLs and libs]      # Python runtime and dependencies
```

## Distribution

To distribute Herald:

1. Run `packaging\build.bat`
2. Zip the entire `dist\Herald\` folder
3. Users extract and run `Herald.exe`

### Size Estimate

- ~150-200 MB (includes Python runtime and all dependencies)

### First Run Notes

- Windows SmartScreen may warn about unrecognized app (click "More info" > "Run anyway")
- UAC prompt will appear (admin required for global hotkeys)
- Settings are stored in `config\settings.json` (created on first run)
- Logs are stored in `logs\` folder

## Reducing AV False Positives

The `keyboard` library hooks system input, which some antivirus software flags. Options:

1. **Code signing** - Purchase a code signing certificate (~$100-300/year)
2. **Submit to AV vendors** - Request whitelisting from major AV vendors
3. **Use installer** - Proper installers (Inno Setup, NSIS) are more trusted

## Advanced: Creating an Installer

For a more professional distribution, consider using Inno Setup:

```
packaging/
├── build.bat           # Build exe
├── herald.spec         # PyInstaller spec
├── installer.iss       # Inno Setup script (future)
└── README.md           # This file
```

## Troubleshooting

### Build fails with missing module

Add the module to `hiddenimports` in `herald.spec`.

### Exe doesn't start

Run from command prompt to see error messages:
```batch
cd dist\Herald
Herald.exe
```

### Hotkeys don't work

Ensure the exe is running as administrator. The spec file includes `uac_admin=True` which should trigger the UAC prompt.

### edge-tts doesn't work

Requires internet connection for neural voices. Offline voices (Zira, David) work without internet.
