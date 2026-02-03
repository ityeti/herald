"""
Herald Update Checker

Checks GitHub releases for new versions.
Provides both silent background check and manual check with notification.
"""

import json
import threading
import webbrowser
from datetime import datetime, timedelta
from urllib.request import urlopen, Request
from urllib.error import URLError
from packaging import version as pkg_version
from loguru import logger

from config import load_settings, save_settings

# Version info
VERSION = "0.2.0"
GITHUB_REPO = "ityeti/herald"
GITHUB_API_URL = f"https://api.github.com/repos/{GITHUB_REPO}/releases/latest"
RELEASES_URL = f"https://github.com/{GITHUB_REPO}/releases"

# Update check settings
CHECK_INTERVAL_HOURS = 24


def get_current_version() -> str:
    """Get the current application version."""
    return VERSION


def check_for_updates() -> tuple[bool, str | None, str | None]:
    """
    Check GitHub for new releases.

    Returns:
        Tuple of (update_available, latest_version, download_url)
    """
    try:
        logger.debug("Checking for updates...")

        request = Request(
            GITHUB_API_URL,
            headers={
                "Accept": "application/vnd.github.v3+json",
                "User-Agent": f"Herald/{VERSION}"
            }
        )

        with urlopen(request, timeout=5) as response:
            data = json.loads(response.read().decode('utf-8'))

        latest_tag = data.get("tag_name", "").lstrip("v")
        html_url = data.get("html_url", "")

        if not latest_tag:
            logger.debug("No release found")
            return (False, None, None)

        # Compare versions
        try:
            current = pkg_version.parse(VERSION)
            latest = pkg_version.parse(latest_tag)

            if latest > current:
                logger.info(f"Update available: {VERSION} -> {latest_tag}")
                return (True, latest_tag, html_url)
            else:
                logger.debug(f"Already up to date ({VERSION})")
                return (False, latest_tag, html_url)

        except Exception as e:
            logger.warning(f"Version comparison failed: {e}")
            return (False, latest_tag, html_url)

    except URLError as e:
        logger.debug(f"Update check failed (network): {e}")
        return (False, None, None)
    except Exception as e:
        logger.warning(f"Update check failed: {e}")
        return (False, None, None)


def should_check_for_updates() -> bool:
    """
    Determine if we should check for updates based on last check time.

    Returns:
        True if enough time has passed since last check
    """
    settings = load_settings()
    last_check_str = settings.get("update_last_check")

    if not last_check_str:
        return True

    try:
        last_check = datetime.fromisoformat(last_check_str)
        next_check = last_check + timedelta(hours=CHECK_INTERVAL_HOURS)
        return datetime.now() >= next_check
    except (ValueError, TypeError):
        return True


def record_update_check():
    """Record the current time as the last update check."""
    settings = load_settings()
    settings["update_last_check"] = datetime.now().isoformat()
    save_settings(settings)


# Cached update info
_update_info = {
    "available": False,
    "version": None,
    "url": None,
}


def check_for_updates_async(callback=None, force: bool = False):
    """
    Check for updates in the background.

    Args:
        callback: Optional function to call with (available, version, url)
        force: If True, check even if recently checked
    """
    def _check():
        global _update_info

        # Skip if recently checked (unless forced)
        if not force and not should_check_for_updates():
            logger.debug("Skipping update check (recently checked)")
            if callback:
                callback(_update_info["available"], _update_info["version"], _update_info["url"])
            return

        available, version, url = check_for_updates()

        # Update cached info
        _update_info["available"] = available
        _update_info["version"] = version
        _update_info["url"] = url

        # Record check time
        record_update_check()

        if callback:
            callback(available, version, url)

    thread = threading.Thread(target=_check, daemon=True)
    thread.start()


def get_update_info() -> dict:
    """Get cached update information."""
    return _update_info.copy()


def open_releases_page():
    """Open the GitHub releases page in the default browser."""
    if _update_info.get("url"):
        webbrowser.open(_update_info["url"])
    else:
        webbrowser.open(RELEASES_URL)


def get_version_string() -> str:
    """Get a formatted version string for display."""
    return f"Herald v{VERSION}"


# Self-test
if __name__ == "__main__":
    import sys

    logger.remove()
    logger.add(sys.stderr, level="DEBUG")

    print(f"Current version: {get_version_string()}")
    print(f"Should check: {should_check_for_updates()}")
    print("\nChecking for updates...")

    available, version, url = check_for_updates()

    if available:
        print(f"\nUpdate available: v{version}")
        print(f"Download: {url}")
    else:
        print(f"\nNo updates available (latest: {version})")
