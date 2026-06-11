using Microsoft.Win32;

namespace HeliosToolkit.App.Services.Tweaks;

/// <summary>Parsing and read/write helpers over the registry hives used by tweaks.</summary>
public static class RegistryHelper
{
    public static RegistryKey OpenBaseKey(string hive) => hive.ToUpperInvariant() switch
    {
        "HKLM" or "HKEY_LOCAL_MACHINE" =>
            RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64),
        "HKCU" or "HKEY_CURRENT_USER" =>
            RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64),
        "HKU" or "HKEY_USERS" =>
            RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64),
        _ => throw new ArgumentException($"Unsupported hive '{hive}'"),
    };

    public static object? ReadValue(string hive, string subKey, string name)
    {
        using RegistryKey baseKey = OpenBaseKey(hive);
        using RegistryKey? key = baseKey.OpenSubKey(subKey);
        return key?.GetValue(name);
    }

    public static RegistryValueKind? ValueKind(string hive, string subKey, string name)
    {
        using RegistryKey baseKey = OpenBaseKey(hive);
        using RegistryKey? key = baseKey.OpenSubKey(subKey);
        if (key is null)
        {
            return null;
        }

        try
        {
            return key.GetValue(name) is null ? null : key.GetValueKind(name);
        }
        catch (global::System.IO.IOException)
        {
            return null;
        }
    }

    public static void WriteValue(string hive, string subKey, string name, object value, RegistryValueKind kind)
    {
        using RegistryKey baseKey = OpenBaseKey(hive);
        using RegistryKey key = baseKey.CreateSubKey(subKey, writable: true);
        key.SetValue(name, value, kind);
    }

    public static void DeleteValue(string hive, string subKey, string name)
    {
        using RegistryKey baseKey = OpenBaseKey(hive);
        using RegistryKey? key = baseKey.OpenSubKey(subKey, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }

    public static bool SubKeyExists(string hive, string subKey)
    {
        using RegistryKey baseKey = OpenBaseKey(hive);
        using RegistryKey? key = baseKey.OpenSubKey(subKey);
        return key is not null;
    }

    public static IEnumerable<string> SubKeyNames(string hive, string subKey)
    {
        using RegistryKey baseKey = OpenBaseKey(hive);
        using RegistryKey? key = baseKey.OpenSubKey(subKey);
        return key?.GetSubKeyNames() ?? Array.Empty<string>();
    }

    /// <summary>Serializes a value for the backup store ("dword:1", "sz:foo", "qword:5", "multi:a|b").</summary>
    public static string Serialize(object value, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.DWord => $"dword:{Convert.ToInt32(value)}",
        RegistryValueKind.QWord => $"qword:{Convert.ToInt64(value)}",
        RegistryValueKind.MultiString => "multi:" + string.Join('|', (string[])value),
        _ => $"sz:{value}",
    };

    public static (object Value, RegistryValueKind Kind) Deserialize(string serialized)
    {
        int sep = serialized.IndexOf(':');
        string tag = sep < 0 ? "sz" : serialized[..sep];
        string body = sep < 0 ? serialized : serialized[(sep + 1)..];
        return tag switch
        {
            "dword" => (int.Parse(body), RegistryValueKind.DWord),
            "qword" => (long.Parse(body), RegistryValueKind.QWord),
            "multi" => (body.Split('|'), RegistryValueKind.MultiString),
            _ => (body, RegistryValueKind.String),
        };
    }
}
