using System.Reflection;
using System.Text.Json;
using Serilog;

namespace Herald.Util;

/// <summary>
/// Checks GitHub releases API for newer versions.
/// </summary>
public static class UpdateChecker
{
    private const string RepoOwner = "ityeti";
    private const string RepoName = "herald";
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    static UpdateChecker()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("Herald-UpdateChecker/1.0");
    }

    /// <summary>
    /// Check if a newer release exists on GitHub.
    /// Returns (latestVersion, releaseUrl) if update available, or null.
    /// </summary>
    public static async Task<(string version, string url)?> CheckForUpdateAsync()
    {
        try
        {
            var apiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            var response = await Http.GetStringAsync(apiUrl);
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString();
            var htmlUrl = root.GetProperty("html_url").GetString();

            if (tagName == null || htmlUrl == null) return null;

            // Strip leading 'v' from tag
            var latestVersion = tagName.TrimStart('v');
            var currentVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion?.Split('+')[0]
                ?? "0.0.0";

            if (IsNewer(latestVersion, currentVersion))
            {
                Log.Information("Update available: {Current} → {Latest}", currentVersion, latestVersion);
                return (latestVersion, htmlUrl);
            }

            Log.Debug("Up to date (current={Current}, latest={Latest})", currentVersion, latestVersion);
            return null;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Update check failed");
            return null;
        }
    }

    /// <summary>Simple semver comparison: is 'latest' newer than 'current'?</summary>
    private static bool IsNewer(string latest, string current)
    {
        if (Version.TryParse(latest, out var latestVer) && Version.TryParse(current, out var currentVer))
            return latestVer > currentVer;
        return string.Compare(latest, current, StringComparison.Ordinal) > 0;
    }
}
