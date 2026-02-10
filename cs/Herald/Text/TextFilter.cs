using System.Text.RegularExpressions;

namespace Herald.Text;

/// <summary>
/// Text filtering and normalization for TTS.
/// Port of Python text_filter.py — two-tier filtering + normalization.
/// </summary>
public static partial class TextFilter
{
    // Box-drawing and block characters (Tier 1: always filtered)
    private static readonly HashSet<char> BoxDrawChars = new(
        "─│┌┐└┘├┤┬┴┼═║╔╗╚╝╠╣╦╩╬━┃┏┓┗┛┣┫┳┻╋▀▄█▌▐░▒▓■□▪▫●○◆◇★☆"
    );

    /// <summary>
    /// Tier 1: Always filter lines that are unspeakable (box art, no letters, etc.)
    /// </summary>
    public static bool IsUnspeakable(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return true;

        // No alphabetic characters at all
        if (!line.Any(char.IsLetter)) return true;

        // >50% box-drawing characters
        int boxCount = line.Count(c => BoxDrawChars.Contains(c));
        if (boxCount > line.Length * 0.5) return true;

        return false;
    }

    /// <summary>
    /// Tier 2: Configurable code-like line detection.
    /// Returns true if the line looks like code, a URL, path, shell prompt, etc.
    /// </summary>
    public static bool IsCodeLike(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0) return false;

        // URLs
        if (UrlPattern().IsMatch(trimmed)) return true;

        // File paths
        if (FilePathPattern().IsMatch(trimmed)) return true;

        // Shell prompts
        if (ShellPromptPattern().IsMatch(trimmed)) return true;

        // Code syntax
        if (CodeSyntaxPattern().IsMatch(trimmed)) return true;

        // PowerShell cmdlets
        if (PowerShellPattern().IsMatch(trimmed)) return true;

        // CLI commands at start of line
        if (CliCommandPattern().IsMatch(trimmed)) return true;

        // Technical hashes (git SHA, UUID)
        if (HashPattern().IsMatch(trimmed)) return true;

        // Standalone email
        if (StandaloneEmailPattern().IsMatch(trimmed)) return true;

        return false;
    }

    /// <summary>
    /// Normalize text for natural speech output.
    /// </summary>
    public static string NormalizeForSpeech(string text, bool removeUrls = true)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var result = text;

        // Remove inline URLs/paths
        if (removeUrls)
        {
            result = InlineUrlPattern().Replace(result, " ");
            result = InlinePathPattern().Replace(result, " ");
        }

        // Strip ANSI escape codes
        result = AnsiEscapePattern().Replace(result, "");

        // Strip markdown
        result = MarkdownBoldPattern().Replace(result, "$1");       // **bold** or __bold__
        result = MarkdownCodePattern().Replace(result, "$1");        // `code`
        result = MarkdownStrikePattern().Replace(result, "$1");      // ~~strike~~
        result = MarkdownLinkPattern().Replace(result, "$1");        // [text](url)

        // Convert hashtags and mentions
        result = HashtagPattern().Replace(result, "$1");
        result = MentionPattern().Replace(result, "$1");

        // Convert snake_case to spaces (iterative for chains)
        result = SnakeCasePattern().Replace(result, m =>
            m.Value.Replace('_', ' '));

        // Convert camelCase/PascalCase to spaces
        result = CamelCasePattern().Replace(result, "$1 $2");
        result = AcronymCamelPattern().Replace(result, "$1 $2");

        // Simplify punctuation
        result = RepeatedDotsPattern().Replace(result, ".");
        result = RepeatedExclamPattern().Replace(result, "!");
        result = RepeatedQuestionPattern().Replace(result, "?");
        result = result.Replace("…", ".");

        // Collapse multiple spaces
        result = MultiSpacePattern().Replace(result, " ");

        return result.Trim();
    }

    /// <summary>
    /// Split text into lines, filtering unspeakable ones and optionally code-like ones.
    /// </summary>
    public static List<string> FilterAndSplit(string text, bool filterCode, bool normalizeText)
    {
        var lines = text.Split('\n', StringSplitOptions.None);
        var result = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (IsUnspeakable(line)) continue;
            if (filterCode && IsCodeLike(line)) continue;

            var processed = normalizeText ? NormalizeForSpeech(line) : line;
            if (!string.IsNullOrWhiteSpace(processed))
                result.Add(processed);
        }

        return result;
    }

    // --- Compiled regex patterns ---

    [GeneratedRegex(@"https?://|ftp://|www\.", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();

    [GeneratedRegex(@"^[A-Z]:\\|^/[a-z]|^\./|^\.\./", RegexOptions.IgnoreCase)]
    private static partial Regex FilePathPattern();

    [GeneratedRegex(@"^\$\s|^>\s|^>>\s|^PS\s+[A-Z]:\\|^\[.*@.*\]\$")]
    private static partial Regex ShellPromptPattern();

    [GeneratedRegex(@"^(import|from|def|class|function|var|let|const|return)\s|^@\w|=>|::")]
    private static partial Regex CodeSyntaxPattern();

    [GeneratedRegex(@"\b(Get|Set|New|Remove|Add|Clear|Enable|Disable|Start|Stop|Restart|Install|Uninstall|Update|Test|Find|Search|Select|Where|Sort|Group|Measure|Compare|Convert|Export|Import|Invoke|Register|Unregister)-\w+", RegexOptions.IgnoreCase)]
    private static partial Regex PowerShellPattern();

    [GeneratedRegex(@"^\s*(pip|npm|npx|yarn|git|docker|kubectl|curl|wget|ssh|scp|rsync|ls|dir|cd|mkdir|rmdir|rm|cp|mv|cat|echo|grep|find|awk|sed|chmod|chown|apt|yum|brew|dotnet|cargo|go)\s")]
    private static partial Regex CliCommandPattern();

    [GeneratedRegex(@"\b[0-9a-f]{40}\b|\b[0-9a-f]{7,8}\b|\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b", RegexOptions.IgnoreCase)]
    private static partial Regex HashPattern();

    [GeneratedRegex(@"^\S+@\S+\.\S+$")]
    private static partial Regex StandaloneEmailPattern();

    [GeneratedRegex(@"https?://\S+|ftp://\S+|www\.\S+")]
    private static partial Regex InlineUrlPattern();

    [GeneratedRegex(@"[A-Z]:\\[\w\\.-]+|/(?:usr|etc|var|home|opt|tmp)/[\w/.-]+|\./[\w/.-]+")]
    private static partial Regex InlinePathPattern();

    [GeneratedRegex(@"\x1b\[[0-9;]*m")]
    private static partial Regex AnsiEscapePattern();

    [GeneratedRegex(@"\*\*(.+?)\*\*|__(.+?)__")]
    private static partial Regex MarkdownBoldPattern();

    [GeneratedRegex(@"`(.+?)`")]
    private static partial Regex MarkdownCodePattern();

    [GeneratedRegex(@"~~(.+?)~~")]
    private static partial Regex MarkdownStrikePattern();

    [GeneratedRegex(@"\[(.+?)\]\(.+?\)")]
    private static partial Regex MarkdownLinkPattern();

    [GeneratedRegex(@"#(\w+)")]
    private static partial Regex HashtagPattern();

    [GeneratedRegex(@"@(\w+)")]
    private static partial Regex MentionPattern();

    [GeneratedRegex(@"\b(\w+(?:_\w+)+)\b")]
    private static partial Regex SnakeCasePattern();

    [GeneratedRegex(@"([a-z])([A-Z])")]
    private static partial Regex CamelCasePattern();

    [GeneratedRegex(@"([A-Z]+)([A-Z][a-z])")]
    private static partial Regex AcronymCamelPattern();

    [GeneratedRegex(@"\.{2,}")]
    private static partial Regex RepeatedDotsPattern();

    [GeneratedRegex(@"!{2,}")]
    private static partial Regex RepeatedExclamPattern();

    [GeneratedRegex(@"\?{2,}")]
    private static partial Regex RepeatedQuestionPattern();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultiSpacePattern();
}
