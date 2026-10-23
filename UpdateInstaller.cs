using System.Net.Http;
using System.Security.Cryptography;

namespace FluentPrayerTimes;

/// <summary>The installer asset of a newer release, as reported by the GitHub API.</summary>
sealed record UpdateAsset(string Url, string Name, long Size, string? Sha256);

/// <summary>Downloads a release installer into a temporary folder and checks it before the user is offered to run it. Never launches anything by itself.</summary>
static class UpdateInstaller
{
    internal static string Dir => Path.Combine(Path.GetTempPath(), "FluentPrayerTimes-update");

    /// <summary>GitHub serves release files from github.com and redirects to its own asset hosts; anything else is refused.</summary>
    internal static bool IsTrustedSource(Uri? uri) =>
        uri != null && uri.Scheme == Uri.UriSchemeHttps && (uri.Host == "github.com" || uri.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase));

    internal static void Verify(string path, UpdateAsset asset)
    {
        var length = new FileInfo(path).Length;
        if (asset.Size > 0 ? length != asset.Size : length < 1_000_000) throw new InvalidDataException("Installer size does not match the release.");
        using var stream = File.OpenRead(path);
        if (stream.ReadByte() != 'M' || stream.ReadByte() != 'Z') throw new InvalidDataException("Downloaded file is not a Windows installer.");
        if (asset.Sha256 != null)
        {
            stream.Position = 0;
            var hash = Convert.ToHexString(SHA256.HashData(stream));
            if (!hash.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Installer checksum does not match the release.");
        }
    }

    internal static async Task<string> DownloadAsync(UpdateAsset asset, Action<double> progress, CancellationToken cancel)
    {
        if (asset.Name != Path.GetFileName(asset.Name) || !asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected installer name.");
        if (!Uri.TryCreate(asset.Url, UriKind.Absolute, out var source) || !IsTrustedSource(source)) throw new InvalidOperationException("Untrusted download source.");
        Directory.CreateDirectory(Dir);
        foreach (var old in Directory.GetFiles(Dir)) { try { File.Delete(old); } catch { } }
        var target = Path.Combine(Dir, asset.Name);
        var part = target + ".part";
        try
        {
            using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("FluentPrayerTimes/" + (typeof(UpdateInstaller).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"));
            using var response = await http.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, cancel);
            response.EnsureSuccessStatusCode();
            if (!IsTrustedSource(response.RequestMessage?.RequestUri)) throw new InvalidOperationException("Download was redirected to an untrusted host.");
            long total = response.Content.Headers.ContentLength ?? asset.Size;
            await using (var input = await response.Content.ReadAsStreamAsync(cancel))
            await using (var output = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                long done = 0; int read;
                while ((read = await input.ReadAsync(buffer, cancel)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancel);
                    done += read;
                    if (total > 0) progress(Math.Min(100, done * 100.0 / total));
                }
            }
            await Task.Run(() => Verify(part, asset), cancel);
            File.Move(part, target, true);
            return target;
        }
        catch
        {
            try { File.Delete(part); } catch { }
            throw;
        }
    }
}
