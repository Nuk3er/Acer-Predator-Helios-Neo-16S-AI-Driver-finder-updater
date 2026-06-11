using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using HeliosToolkit.App.Services.System;

namespace HeliosToolkit.App.Services.Drivers;

public sealed record DownloadOutcome(string FilePath, string Sha256Hex, long Bytes);

/// <summary>Streams a file into Downloads\HeliosDrivers with progress and an incremental SHA-256.</summary>
public sealed class DownloadService(HttpClient http)
{
    public static string DriversFolder => Path.Combine(KnownFolders.Downloads, "HeliosDrivers");

    public async Task<DownloadOutcome> DownloadAsync(
        string url,
        string subfolder,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        using HttpResponseMessage response =
            await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        string fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"', ' ')
            ?? FileNameFromUrl(url)
            ?? "download.bin";

        string directory = Path.Combine(DriversFolder, Sanitize(subfolder));
        Directory.CreateDirectory(directory);
        string targetPath = Path.Combine(directory, Sanitize(fileName));

        long? totalBytes = response.Content.Headers.ContentLength;
        long written = 0;

        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using (Stream source = await response.Content.ReadAsStreamAsync(ct))
        await using (var target = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
        {
            byte[] buffer = new byte[1 << 16];
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), ct);
                sha.AppendData(buffer, 0, read);
                written += read;
                if (totalBytes is > 0)
                {
                    progress?.Report((double)written / totalBytes.Value);
                }
            }
        }

        progress?.Report(1.0);
        string hex = Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
        return new DownloadOutcome(targetPath, hex, written);
    }

    private static string? FileNameFromUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            string name = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return null;
    }

    private static string Sanitize(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name.Length == 0 ? "_" : name;
    }
}
