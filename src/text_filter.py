"""
Text Filtering for Herald TTS

Filters out lines that cannot be meaningfully spoken by TTS engines.
Two-tier filtering:
1. Always filtered (unspeakable): box-drawing characters, lines with no letters
2. Configurable (code/tech): URLs, file paths, code syntax, shell commands
"""

import re
from loguru import logger

# Box-drawing and similar decorative characters
BOX_DRAWING_CHARS = set("─│┌┐└┘├┤┬┴┼═║╔╗╚╝╠╣╦╩╬━┃┏┓┗┛┣┫┳┻╋▀▄█▌▐░▒▓■□▪▫●○◆◇★☆")

# Patterns for code/URL detection
URL_PATTERN = re.compile(
    r"^[\s]*"  # Optional leading whitespace
    r"(https?://|ftp://|www\.)"  # URL prefix
    r"[^\s]+"  # Rest of URL
    r"[\s]*$",  # Optional trailing whitespace
    re.IGNORECASE,
)

# File path patterns (Windows and Unix)
FILE_PATH_PATTERN = re.compile(
    r"^[\s]*"  # Optional leading whitespace
    r"("
    r"[A-Za-z]:\\[^\s]*|"  # Windows: C:\path\to\file
    r"/[a-zA-Z][^\s]*|"  # Unix: /path/to/file (not just /)
    r"\.[/\\][^\s]*|"  # Relative: ./path or .\path
    r"\.\.[/\\][^\s]*"  # Parent: ../path or ..\path
    r")"
    r"[\s]*$",  # Optional trailing whitespace
    re.IGNORECASE,
)

# Shell prompt patterns
SHELL_PROMPT_PATTERN = re.compile(
    r"^[\s]*"  # Optional leading whitespace
    r"("
    r"[$>][\s]+|"  # $ or > followed by space (shell prompts)
    r">>[\s]+|"  # >> (PowerShell append)
    r"PS[^\s]*>|"  # PS C:\> style PowerShell prompt
    r"\[[^\]]+\][$#]\s"  # [user@host]$ style prompt
    r")"
)

# Code syntax patterns
CODE_PATTERNS = [
    # Python
    r"^[\s]*(import|from)\s+\w+",  # import statements
    r"^[\s]*def\s+\w+\s*\(",  # function definitions
    r"^[\s]*class\s+\w+",  # class definitions
    r"^[\s]*@\w+",  # decorators
    # JavaScript/TypeScript
    r"^[\s]*(const|let|var)\s+\w+\s*=",  # variable declarations
    r"^[\s]*function\s+\w+\s*\(",  # function declarations
    r"^[\s]*export\s+(default\s+)?(class|function|const)",  # exports
    # General code syntax
    r"^[\s]*[{}[\]]+[\s]*$",  # Lines with only braces/brackets
    r"^\s*[=\-]{3,}\s*$",  # Separator lines (===, ---)
    r"^[\s]*//|^[\s]*#|^[\s]*/\*|^[\s]*\*",  # Comments
    r"->\s*\w+",  # Arrow operators (Rust, PHP)
    r"=>\s*[{\(]",  # Fat arrow (JS)
    r"::\w+",  # Scope resolution (C++, Rust)
]

# PowerShell cmdlets
POWERSHELL_CMDLETS = [
    "Get-",
    "Set-",
    "New-",
    "Remove-",
    "Add-",
    "Clear-",
    "Copy-",
    "Move-",
    "Rename-",
    "Out-",
    "Write-",
    "Read-",
    "Start-",
    "Stop-",
    "Test-",
    "Invoke-",
    "Select-",
    "Where-",
    "ForEach-",
    "Format-",
    "Export-",
    "Import-",
    "ConvertTo-",
    "ConvertFrom-",
]

# Common CLI commands
CLI_COMMANDS = [
    "pip ",
    "pip3 ",
    "npm ",
    "npx ",
    "yarn ",
    "pnpm ",
    "git ",
    "docker ",
    "kubectl ",
    "terraform ",
    "curl ",
    "wget ",
    "ssh ",
    "scp ",
    "rsync ",
    "cd ",
    "ls ",
    "dir ",
    "mkdir ",
    "rm ",
    "cp ",
    "mv ",
    "python ",
    "python3 ",
    "node ",
    "ruby ",
    "go ",
    "cargo ",
    "rustc ",
    "javac ",
    "gcc ",
    "g++ ",
    "apt ",
    "apt-get ",
    "brew ",
    "choco ",
    "winget ",
]

# Compile code patterns
CODE_REGEX = [re.compile(p, re.IGNORECASE) for p in CODE_PATTERNS]

# Additional technical patterns for filtering
GIT_HASH_PATTERN = re.compile(r"^[a-f0-9]{40}$", re.IGNORECASE)  # Full git hash
SHORT_HASH_PATTERN = re.compile(r"^[a-f0-9]{7,8}$", re.IGNORECASE)  # Short commit hash
UUID_PATTERN = re.compile(r"^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$", re.IGNORECASE)
HEX_DUMP_PATTERN = re.compile(r"(0x[a-f0-9]+\s*){3,}", re.IGNORECASE)  # Multiple 0x values
LOG_TIMESTAMP_PATTERN = re.compile(r"^\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}")

# Patterns for text normalization
ANSI_ESCAPE_PATTERN = re.compile(r"\x1b\[[0-9;]*m")
MARKDOWN_BOLD_PATTERN = re.compile(r"\*\*([^*]+)\*\*")  # **bold**
MARKDOWN_UNDERLINE_BOLD_PATTERN = re.compile(r"__([^_]+)__")  # __bold__
MARKDOWN_CODE_PATTERN = re.compile(r"`([^`]+)`")  # `code`
MARKDOWN_STRIKE_PATTERN = re.compile(r"~~([^~]+)~~")  # ~~strike~~
MARKDOWN_LINK_PATTERN = re.compile(r"\[([^\]]+)\]\([^)]+\)")  # [text](url) -> text
REPEATED_DOTS_PATTERN = re.compile(r"\.{2,}")
REPEATED_EXCLAIM_PATTERN = re.compile(r"!{2,}")
REPEATED_QUESTION_PATTERN = re.compile(r"\?{2,}")
UNICODE_ELLIPSIS_PATTERN = re.compile(r"…")  # Unicode ellipsis -> .
MULTIPLE_SPACES_PATTERN = re.compile(r" {2,}")  # Multiple spaces -> single
HASHTAG_PATTERN = re.compile(r"#(\w+)")  # #tag -> tag
MENTION_PATTERN = re.compile(r"@(\w+)")  # @user -> user

# Email pattern for filtering standalone email lines
EMAIL_PATTERN = re.compile(r"^[\s]*[\w.-]+@[\w.-]+\.\w+[\s]*$", re.IGNORECASE)


def normalize_for_speech(text: str) -> str:
    """Transform text to read more naturally for TTS.

    Applies the following transformations:
    1. Strip ANSI escape codes (terminal colors)
    2. Strip markdown formatting (**bold**, __bold__, `code`, ~~strike~~)
    3. Convert snake_case to spaces
    4. Convert camelCase/PascalCase to spaces
    5. Simplify repeated punctuation

    Args:
        text: Text to normalize

    Returns:
        Normalized text suitable for TTS
    """
    if not text:
        return text

    result = text

    # 1. Strip ANSI escape codes
    result = ANSI_ESCAPE_PATTERN.sub("", result)

    # 2. Strip markdown formatting (keep the content)
    result = MARKDOWN_BOLD_PATTERN.sub(r"\1", result)
    result = MARKDOWN_UNDERLINE_BOLD_PATTERN.sub(r"\1", result)
    result = MARKDOWN_CODE_PATTERN.sub(r"\1", result)
    result = MARKDOWN_STRIKE_PATTERN.sub(r"\1", result)
    result = MARKDOWN_LINK_PATTERN.sub(r"\1", result)  # [text](url) -> text

    # 3. Normalize hashtags and mentions
    result = HASHTAG_PATTERN.sub(r"\1", result)  # #tag -> tag
    result = MENTION_PATTERN.sub(r"\1", result)  # @user -> user

    # 4. Convert snake_case to spaces
    # Match underscores between word characters and replace with space
    result = re.sub(r"(\w)_(\w)", r"\1 \2", result)
    # Handle multiple consecutive underscores (run multiple times for chains)
    while "_" in result and re.search(r"(\w)_(\w)", result):
        result = re.sub(r"(\w)_(\w)", r"\1 \2", result)

    # 5. Convert camelCase/PascalCase to spaces
    # Insert space before uppercase letters that follow lowercase letters
    result = re.sub(r"([a-z])([A-Z])", r"\1 \2", result)
    # Insert space before uppercase letters followed by lowercase (for acronyms like XMLParser -> XML Parser)
    result = re.sub(r"([A-Z]+)([A-Z][a-z])", r"\1 \2", result)

    # 6. Simplify repeated punctuation
    result = REPEATED_DOTS_PATTERN.sub(".", result)
    result = REPEATED_EXCLAIM_PATTERN.sub("!", result)
    result = REPEATED_QUESTION_PATTERN.sub("?", result)

    # 7. Replace unicode ellipsis
    result = UNICODE_ELLIPSIS_PATTERN.sub(".", result)

    # 8. Collapse multiple spaces (do this last)
    result = MULTIPLE_SPACES_PATTERN.sub(" ", result)

    return result


def is_unspeakable(line: str) -> bool:
    """Check if a line cannot be meaningfully spoken (always filtered).

    Returns True for:
    - Lines that are mostly/entirely box-drawing characters
    - Lines with no alphabetic characters

    Args:
        line: A single line of text

    Returns:
        True if the line should always be filtered out
    """
    if not line or not line.strip():
        return True

    stripped = line.strip()

    # Check for box-drawing lines (>50% box chars)
    box_char_count = sum(1 for c in stripped if c in BOX_DRAWING_CHARS)
    if box_char_count > 0 and box_char_count >= len(stripped) * 0.5:
        return True

    # Check for lines with no alphabetic characters
    has_letters = any(c.isalpha() for c in stripped)
    return not has_letters


def is_code_like(line: str) -> bool:
    """Check if a line looks like code, URLs, or paths (configurable filter).

    Returns True for:
    - URLs (http://, https://, www.)
    - File paths (C:\\..., /path/..., ./...)
    - Shell prompts and commands
    - Code syntax patterns

    Args:
        line: A single line of text

    Returns:
        True if the line looks like code/URLs/paths
    """
    if not line or not line.strip():
        return False

    stripped = line.strip()

    # Check for URLs
    if URL_PATTERN.match(stripped):
        return True

    # Check for file paths (but not if it looks like a sentence)
    if FILE_PATH_PATTERN.match(stripped):
        # Don't filter if it has multiple words (probably a sentence mentioning a path)
        words = stripped.split()
        if len(words) <= 2:
            return True

    # Check for shell prompts
    if SHELL_PROMPT_PATTERN.match(stripped):
        return True

    # Check for PowerShell cmdlets
    for cmdlet in POWERSHELL_CMDLETS:
        if cmdlet in stripped:
            return True

    # Check for CLI commands at start of line
    lower_stripped = stripped.lower()
    for cmd in CLI_COMMANDS:
        if lower_stripped.startswith(cmd):
            return True

    # Check for code syntax patterns
    for regex in CODE_REGEX:
        if regex.search(stripped):
            return True

    # Check for git hashes (full 40-char or short 7-8 char)
    if GIT_HASH_PATTERN.match(stripped):
        return True
    if SHORT_HASH_PATTERN.match(stripped):
        return True

    # Check for UUIDs
    if UUID_PATTERN.match(stripped):
        return True

    # Check for hex dumps (multiple 0x values on a line)
    if HEX_DUMP_PATTERN.search(stripped):
        return True

    # Check for log timestamps at start of line
    if LOG_TIMESTAMP_PATTERN.match(stripped):
        return True

    # Check for standalone email addresses
    return bool(EMAIL_PATTERN.match(stripped))


def filter_lines(lines: list[str], filter_code: bool = True) -> list[str]:
    """Filter out unspeakable and optionally code-like lines.

    Args:
        lines: List of text lines to filter
        filter_code: If True, also filter URLs, paths, and code syntax

    Returns:
        Filtered list of lines that can be spoken
    """
    if not lines:
        return []

    result = []
    skipped_unspeakable = 0
    skipped_code = 0

    for line in lines:
        if is_unspeakable(line):
            skipped_unspeakable += 1
            continue

        if filter_code and is_code_like(line):
            skipped_code += 1
            continue

        result.append(line)

    # Log filtering results at debug level
    total_skipped = skipped_unspeakable + skipped_code
    if total_skipped > 0:
        logger.debug(f"Filtered {total_skipped} lines: {skipped_unspeakable} unspeakable, {skipped_code} code-like")

    return result


# Self-test
if __name__ == "__main__":
    import sys

    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    test_lines = [
        # Should always be filtered (unspeakable)
        "--------------------------------------------",  # Box-drawing chars (using dashes for testing)
        "============================================",
        "***********",
        "[15/52]",
        "---",
        "",
        # Should be filtered when filter_code=True
        "https://github.com/ityeti/herald",
        "C:\\dev\\herald\\src\\main.py",
        "/usr/local/bin/python",
        "$ pip install herald",
        "> Get-Process",
        "import asyncio",
        "def main():",
        "git clone https://github.com/ityeti/herald",
        # Should NOT be filtered
        "Hello, this is a test.",
        "The quick brown fox jumps over the lazy dog.",
        "Press Alt+S to speak selected text.",
        "This line mentions C:\\Users but is a sentence.",
    ]

    print("Testing text_filter.py")
    print("=" * 50)

    print("\n--- Individual line tests ---")
    for line in test_lines:
        unspeakable = is_unspeakable(line)
        code_like = is_code_like(line)
        preview = line[:40] + "..." if len(line) > 40 else line
        print(f"  '{preview}' -> unspeakable={unspeakable}, code={code_like}")

    print("\n--- Filter with code=True ---")
    filtered = filter_lines(test_lines, filter_code=True)
    print(f"  Input: {len(test_lines)} lines")
    print(f"  Output: {len(filtered)} lines")
    for line in filtered:
        print(f"    - {line}")

    print("\n--- Filter with code=False ---")
    filtered = filter_lines(test_lines, filter_code=False)
    print(f"  Input: {len(test_lines)} lines")
    print(f"  Output: {len(filtered)} lines")
    for line in filtered:
        print(f"    - {line}")

    print("\n--- Text normalization tests ---")
    normalize_tests = [
        ("snake_case_name", "snake case to spaces"),
        ("camelCaseName", "camelCase to spaces"),
        ("**bold text**", "markdown bold stripped"),
        ("`code here`", "markdown code stripped"),
        ("Hello...", "repeated dots simplified"),
        ("on_filter_code_change", "multi-underscore"),
        ("XMLParser", "acronym handling"),
        ("\x1b[31mRed Text\x1b[0m", "ANSI codes stripped"),
    ]
    for text, description in normalize_tests:
        normalized = normalize_for_speech(text)
        print(f"  {description}: '{text}' -> '{normalized}'")
