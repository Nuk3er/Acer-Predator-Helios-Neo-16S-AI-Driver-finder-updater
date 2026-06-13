using System.IO;

namespace HeliosToolkit.App;

/// <summary>Well-known folders the toolkit writes to.</summary>
public static class AppPaths
{
    public static string Root =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "HeliosToolkit");

    public static string Logs => Path.Combine(Root, "logs");

    public static string Backup => Path.Combine(Root, "backup");

    public static string Cache => Path.Combine(Root, "cache");

    public static string Tools => Path.Combine(Root, "tools");

    public static string BenchCsv => Path.Combine(Root, "bench", "csv");

    public static string BenchRuns => Path.Combine(Root, "bench", "runs");

    public static string BoostSessionFile => Path.Combine(Root, "boost-session.json");

    public static string BoostConfigFile => Path.Combine(Root, "boost-config.json");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Backup);
        Directory.CreateDirectory(Cache);
        Directory.CreateDirectory(Tools);
        Directory.CreateDirectory(BenchCsv);
        Directory.CreateDirectory(BenchRuns);
    }
}
