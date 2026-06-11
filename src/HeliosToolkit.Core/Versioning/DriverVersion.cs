namespace HeliosToolkit.Core.Versioning;

/// <summary>
/// Tolerant dotted driver version ("10.1.19444.8378", "6.0.9971.1", "576.80").
/// Missing parts compare as 0, so "10.1" == "10.1.0.0".
/// </summary>
public readonly struct DriverVersion : IComparable<DriverVersion>, IEquatable<DriverVersion>
{
    private readonly int[] _parts;

    public IReadOnlyList<int> Parts => _parts ?? Array.Empty<int>();

    private DriverVersion(int[] parts) => _parts = parts;

    /// <summary>
    /// Parses a dotted numeric version. Returns false for anything that is not purely
    /// dot-separated digits (e.g. manifest placeholders like "10.1.x").
    /// </summary>
    public static bool TryParse(string? text, out DriverVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string[] tokens = text.Trim().Split('.');
        var parts = new int[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            if (!int.TryParse(tokens[i], out parts[i]) || parts[i] < 0)
            {
                return false;
            }
        }

        version = new DriverVersion(parts);
        return true;
    }

    public int CompareTo(DriverVersion other)
    {
        IReadOnlyList<int> a = Parts, b = other.Parts;
        int len = Math.Max(a.Count, b.Count);
        for (int i = 0; i < len; i++)
        {
            int left = i < a.Count ? a[i] : 0;
            int right = i < b.Count ? b[i] : 0;
            if (left != right)
            {
                return left.CompareTo(right);
            }
        }

        return 0;
    }

    public bool Equals(DriverVersion other) => CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is DriverVersion other && Equals(other);

    public override int GetHashCode()
    {
        // Trailing zeros must not change the hash ("10.1" == "10.1.0").
        IReadOnlyList<int> parts = Parts;
        int significant = parts.Count;
        while (significant > 0 && parts[significant - 1] == 0)
        {
            significant--;
        }

        var hash = new HashCode();
        for (int i = 0; i < significant; i++)
        {
            hash.Add(parts[i]);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => string.Join('.', Parts);

    public static bool operator <(DriverVersion left, DriverVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(DriverVersion left, DriverVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(DriverVersion left, DriverVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(DriverVersion left, DriverVersion right) => left.CompareTo(right) >= 0;
    public static bool operator ==(DriverVersion left, DriverVersion right) => left.Equals(right);
    public static bool operator !=(DriverVersion left, DriverVersion right) => !left.Equals(right);
}
