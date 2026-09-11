namespace Rbq.QueryApi;

/// <summary>
/// Loads the repo-root .env into the process environment so the C# API reads the
/// same keys the Python ingestion pipeline already uses.
/// </summary>
public static class DotEnv
{
    public static void Load(string path)
    {
        if (!File.Exists(path)) return;

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;

            var separator = trimmed.IndexOf('=');
            if (separator <= 0) continue;

            var key = trimmed[..separator].Trim();
            var value = trimmed[(separator + 1)..].Trim().Trim('"', '\'');

            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    /// <summary>Reads a required variable, failing fast with a useful message.</summary>
    public static string Require(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException($"Missing environment variable '{key}' (set it in .env).");

    public static string Get(string key, string fallback) =>
        Environment.GetEnvironmentVariable(key) is { Length: > 0 } value ? value : fallback;
}
