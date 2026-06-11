namespace HeliosToolkit.Core.Update;

/// <summary>Compares an app version against a GitHub release tag (semver, "v" optional).</summary>
public static class ReleaseVersion
{
    /// <summary>Parses "v1.2.3", "1.2.3", "1.2.3-ci.4" into (major, minor, patch), ignoring pre-release.</summary>
    public static bool TryParse(string? tag, out (int Major, int Minor, int Patch) version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        string s = tag.Trim();
        if (s.StartsWith('v') || s.StartsWith('V'))
        {
            s = s[1..];
        }

        int dash = s.IndexOf('-');
        if (dash >= 0)
        {
            s = s[..dash];
        }

        string[] parts = s.Split('.');
        if (parts.Length is < 1 or > 3)
        {
            return false;
        }

        int major = 0, minor = 0, patch = 0;
        if (!int.TryParse(parts[0], out major) || major < 0)
        {
            return false;
        }

        if (parts.Length > 1 && (!int.TryParse(parts[1], out minor) || minor < 0))
        {
            return false;
        }

        if (parts.Length > 2 && (!int.TryParse(parts[2], out patch) || patch < 0))
        {
            return false;
        }

        version = (major, minor, patch);
        return true;
    }

    /// <summary>True when <paramref name="latestTag"/> is a newer version than <paramref name="currentVersion"/>.</summary>
    public static bool IsNewer(string? latestTag, string? currentVersion)
    {
        if (!TryParse(latestTag, out var latest) || !TryParse(currentVersion, out var current))
        {
            return false;
        }

        return Compare(latest, current) > 0;
    }

    private static int Compare((int Major, int Minor, int Patch) a, (int Major, int Minor, int Patch) b)
    {
        int byMajor = a.Major.CompareTo(b.Major);
        if (byMajor != 0)
        {
            return byMajor;
        }

        int byMinor = a.Minor.CompareTo(b.Minor);
        return byMinor != 0 ? byMinor : a.Patch.CompareTo(b.Patch);
    }
}
