# Herald Uninstall Guide

This guide covers how to completely remove Herald from your system.

## Quick Uninstall (Development Version)

If you installed Herald from source, run:

```batch
Uninstall_All.bat
```

This removes:
- Virtual environment (`venv/`)
- Logs (`logs/`)
- Temporary audio files (`temp/`)
- Settings (`config/settings.json`)
- Startup shortcuts
- Python cache folders
- Build artifacts

**To completely remove**: Delete the entire Herald folder after running the uninstaller.

---

## Manual Uninstall (Portable EXE)

If you're using the standalone executable, follow these steps:

### 1. Remove Startup Entry (if enabled)

Check and delete if present:
```
%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\Herald.lnk
```

### 2. Delete Application Files

Delete the folder where you extracted Herald:
- `Herald.exe`
- `config/` folder
- `logs/` folder
- `temp/` folder

### 3. Remove Settings (Optional)

Herald stores settings in the application folder (`config/settings.json`), not in AppData or Registry.

---

## Data Locations Reference

| Data | Location | Notes |
|------|----------|-------|
| Application | Where you extracted it | Portable, no installer |
| Settings | `<app folder>/config/settings.json` | JSON config file |
| Logs | `<app folder>/logs/` | Rotating log files |
| Temp Audio | `<app folder>/temp/` | TTS audio cache, auto-cleaned |
| Startup Shortcut | `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\` | Only if auto-start enabled |

---

## Development Environment Cleanup

For developers, additional cleanup:

### Python Cache
```batch
:: Remove all __pycache__ folders
for /d /r %d in (__pycache__) do rmdir /s /q "%d"

:: Remove pytest cache
rmdir /s /q .pytest_cache
```

### Virtual Environment
```batch
rmdir /s /q venv
```

### Build Artifacts
```batch
rmdir /s /q build
rmdir /s /q dist
del *.spec
```

### Git Cleanup (Optional)
```batch
:: Remove untracked files (careful!)
git clean -fdx
```

---

## Reinstallation

To reinstall after uninstalling:

**Development version:**
```batch
Launch_Herald.bat
```
This recreates the virtual environment and installs dependencies.

**Portable EXE:**
Download the latest release from [GitHub Releases](https://github.com/ityeti/herald/releases).

---

## Troubleshooting

### "Herald is already running"

If you see this error, the previous instance didn't close properly:

1. Check system tray for Herald icon
2. Or open Task Manager and end `python.exe` or `Herald.exe`
3. Or restart Windows to clear the mutex lock

### Settings Not Saving

Settings are saved to `config/settings.json`. Ensure the config folder exists and is writable.

### Startup Shortcut Won't Delete

Run the uninstaller as Administrator, or manually delete from:
```
%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\
```
