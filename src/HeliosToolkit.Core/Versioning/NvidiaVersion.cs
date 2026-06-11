namespace HeliosToolkit.Core.Versioning;

/// <summary>
/// Converts between the WMI driver version NVIDIA reports ("32.0.15.7680")
/// and the GeForce version users know ("576.80").
/// </summary>
public static class NvidiaVersion
{
    /// <summary>
    /// WMI <c>Win32_VideoController.DriverVersion</c> → GeForce version string.
    /// Algorithm (same as TinyNvidiaUpdateChecker): strip the dots, take the last
    /// five digits, and put a dot before the final two. "32.0.15.7680" → "576.80".
    /// </summary>
    public static bool TryFromWmiVersion(string? wmiVersion, out string geforceVersion)
    {
        geforceVersion = string.Empty;
        if (string.IsNullOrWhiteSpace(wmiVersion))
        {
            return false;
        }

        Span<char> digits = stackalloc char[wmiVersion.Length];
        int count = 0;
        foreach (char c in wmiVersion)
        {
            if (char.IsAsciiDigit(c))
            {
                digits[count++] = c;
            }
            else if (c != '.')
            {
                return false;
            }
        }

        if (count < 5)
        {
            return false;
        }

        ReadOnlySpan<char> last5 = digits.Slice(count - 5, 5);
        geforceVersion = $"{last5[..3]}.{last5[3..]}";
        return true;
    }

    /// <summary>Parses a GeForce version like "576.80" into comparable (major, minor).</summary>
    public static bool TryParseGeForce(string? text, out int major, out int minor)
    {
        major = minor = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string[] tokens = text.Trim().Split('.');
        return tokens.Length == 2
            && int.TryParse(tokens[0], out major) && major >= 0
            && int.TryParse(tokens[1], out minor) && minor >= 0;
    }

    /// <summary>negative = installed older than latest, 0 = same, positive = installed newer.</summary>
    public static int CompareGeForce(string installed, string latest)
    {
        if (!TryParseGeForce(installed, out int iMajor, out int iMinor))
        {
            throw new FormatException($"Not a GeForce version: '{installed}'");
        }

        if (!TryParseGeForce(latest, out int lMajor, out int lMinor))
        {
            throw new FormatException($"Not a GeForce version: '{latest}'");
        }

        int byMajor = iMajor.CompareTo(lMajor);
        return byMajor != 0 ? byMajor : iMinor.CompareTo(lMinor);
    }
}
