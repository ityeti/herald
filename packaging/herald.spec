# -*- mode: python ; coding: utf-8 -*-
"""
Herald PyInstaller Spec File

Build command (from project root):
    pyinstaller packaging/herald.spec

Or use the build script:
    packaging\build.bat
"""

import sys
from pathlib import Path

# Get the project root (parent of packaging folder)
SPEC_DIR = Path(SPECPATH)
PROJECT_ROOT = SPEC_DIR.parent

block_cipher = None

a = Analysis(
    [str(PROJECT_ROOT / 'src' / 'main.py')],
    pathex=[str(PROJECT_ROOT / 'src')],
    binaries=[],
    datas=[],
    hiddenimports=[
        # pystray and PIL
        'pystray._win32',
        'PIL._tkinter_finder',

        # edge-tts async support
        'edge_tts',
        'edge_tts.communicate',
        'aiohttp',
        'asyncio',

        # pyttsx3 drivers
        'pyttsx3.drivers',
        'pyttsx3.drivers.sapi5',
        'win32com.client',
        'win32api',

        # pygame audio
        'pygame',
        'pygame.mixer',

        # keyboard hooks
        'keyboard',
        'keyboard._winkeyboard',

        # clipboard
        'pyperclip',

        # logging
        'loguru',

        # Windows ctypes
        'ctypes',
        'ctypes.wintypes',
    ],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[
        # Exclude unnecessary modules to reduce size
        'tkinter',
        'matplotlib',
        'numpy',
        'pandas',
        'scipy',
        'test',
        'unittest',
    ],
    win_no_prefer_redirects=False,
    win_private_assemblies=False,
    cipher=block_cipher,
    noarchive=False,
)

pyz = PYZ(a.pure, a.zipped_data, cipher=block_cipher)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name='Herald',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    console=True,  # Console app (can be hidden via tray menu)
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
    # Request admin privileges (required for global hotkeys)
    uac_admin=True,
    uac_uiaccess=False,
    # Version info (optional)
    version=None,
    # Icon (optional - will use default if not provided)
    icon=None,
)

coll = COLLECT(
    exe,
    a.binaries,
    a.zipfiles,
    a.datas,
    strip=False,
    upx=True,
    upx_exclude=[],
    name='Herald',
)
