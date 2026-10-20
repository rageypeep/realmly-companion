using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace RealmlyCompanion;

internal sealed record PreparedUpdate(string Directory, string Executable, string Sha256);

internal static class SelfUpdater
{
    internal static Uri ValidateRelease(CompanionRelease release)
    {
        if (!Version.TryParse(release.Version, out _) || !Regex.IsMatch(release.Sha256 ?? "", "^[a-fA-F0-9]{64}$"))
            throw new InvalidOperationException("The release has no valid version or verification hash. Please try checking again later.");
        if (!Uri.TryCreate(release.DownloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || uri.Host != "realmly.me" || !uri.IsDefaultPort || uri.UserInfo != ""
            || uri.AbsolutePath != $"/downloads/RealmlyCompanion-{release.Version}-win-x64.zip" || uri.Fragment != "" || uri.Query != "")
            throw new InvalidOperationException("The update download address is not trusted.");
        return uri;
    }

    internal static async Task<PreparedUpdate> PrepareAsync(CompanionRelease release, Action<string> progress, CancellationToken cancellationToken)
    {
        var uri = ValidateRelease(release);
        var stage = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Realmly", "Updates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        var zip = Path.Combine(stage, "release.zip");
        try
        {
            progress("Downloading Companion " + release.Version + "...");
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            using var response = await http.GetAsync(uri + "?update=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.RequestMessage?.RequestUri is not { Scheme: "https", Host: "realmly.me" }) throw new InvalidOperationException("Update redirected outside Realmly.");
            var total = response.Content.Headers.ContentLength;
            const long maximum = 120L * 1024 * 1024;
            if (total > maximum) throw new InvalidOperationException("The update package is unexpectedly large.");
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var destination = File.Create(zip))
            {
                var buffer = new byte[81920];
                long received = 0;
                var previousPercent = -1;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    received += read;
                    if (received > maximum) throw new InvalidOperationException("The update package is unexpectedly large.");
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    var percent = total > 0 ? (int)(100 * received / total) : (int)(received / (1024 * 1024));
                    if (percent != previousPercent)
                    {
                        progress(total > 0 ? $"Downloading Companion {release.Version}: {percent}%" : $"Downloading Companion: {percent} MB");
                        previousPercent = percent;
                    }
                }
            }
            progress("Verifying the update package...");
            return await VerifyAndExtractAsync(zip, stage, release, cancellationToken);
        }
        catch
        {
            Directory.Delete(stage, recursive: true); // This method owns this exact GUID staging directory.
            throw;
        }
    }

    internal static async Task<PreparedUpdate> VerifyAndExtractAsync(string zip, string stage, CompanionRelease release, CancellationToken cancellationToken)
    {
        ValidateRelease(release);
        await using (var input = File.OpenRead(zip))
        {
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken));
            if (!hash.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Update verification failed. Your current Companion has not been changed.");
        }
        using var archive = ZipFile.OpenRead(zip);
        var entry = archive.Entries.SingleOrDefault(e => e.FullName == "RealmlyCompanion.exe")
            ?? throw new InvalidOperationException("The update has no Companion executable.");
        if (entry.Length is <= 0 or > 300L * 1024 * 1024) throw new InvalidOperationException("The update executable has an invalid size.");
        var executable = Path.Combine(stage, "RealmlyCompanion.exe");
        entry.ExtractToFile(executable);
        var actual = FileVersionInfo.GetVersionInfo(executable).FileVersion;
        if (!Version.TryParse(actual, out var version) || !Version.TryParse(release.Version, out var expected)
            || version.Major != expected.Major || version.Minor != expected.Minor || version.Build != Math.Max(0, expected.Build)
            || version.Revision != Math.Max(0, expected.Revision))
            throw new InvalidOperationException("The executable version does not match the advertised release.");
        await using var extracted = File.OpenRead(executable);
        return new PreparedUpdate(stage, executable, Convert.ToHexString(await SHA256.HashDataAsync(extracted, cancellationToken)));
    }

    internal static async Task LaunchHelperAsync(PreparedUpdate update, string target, int parentId, long parentStarted, CancellationToken cancellationToken, bool quietErrors = false)
    {
        target = Path.GetFullPath(target);
        var folder = Path.GetDirectoryName(target)!;
        // Check write access while the original app is still running, before asking it to exit.
        var probe = Path.Combine(folder, ".realmly-write-" + Guid.NewGuid().ToString("N"));
        using (File.Create(probe)) { }
        File.Delete(probe);
        var script = Path.Combine(update.Directory, "SelfUpdate.ps1");
        using (var source = typeof(SelfUpdater).Assembly.GetManifestResourceStream("RealmlyCompanion.SelfUpdate.ps1")
            ?? throw new InvalidOperationException("The update helper is missing."))
        await using (var destination = File.Create(script)) await source.CopyToAsync(destination, cancellationToken);
        var ready = Path.Combine(update.Directory, "ready");
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script,
            "-ParentProcessId", parentId.ToString(), "-ParentStarted", parentStarted.ToString(),
            "-SourcePath", update.Executable, "-TargetPath", target, "-ExpectedHash", update.Sha256, "-ReadyPath", ready }) start.ArgumentList.Add(argument);
        if (quietErrors) start.ArgumentList.Add("-NoErrorDialog");
        using var helper = Process.Start(start) ?? throw new InvalidOperationException("The update helper could not start.");
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (File.Exists(ready)) return;
            if (helper.HasExited) throw new InvalidOperationException("The update helper could not prepare the restart. Your current Companion is unchanged.");
            await Task.Delay(100, cancellationToken);
        }
        if (!helper.HasExited) helper.Kill();
        throw new InvalidOperationException("The update helper did not become ready. Your current Companion is unchanged.");
    }
}
