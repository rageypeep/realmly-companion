using System.IO.Compression;

namespace RealmlyCompanion;

internal enum ClientKind { Retail, Forever }

internal static class WowFiles
{
    public static string ClientFolder(ClientKind client) => client == ClientKind.Retail ? "_retail_" : "_forever_";

    public static string AddOnsPath(string gameRoot, ClientKind client) => Path.Combine(gameRoot, ClientFolder(client), "Interface", "AddOns");

    public static string? FindGameRoot()
    {
        foreach (var drive in DriveInfo.GetDrives().Where(drive => drive.IsReady))
        {
            var candidate = Path.Combine(drive.RootDirectory.FullName, "World of Warcraft");
            if (Directory.Exists(candidate)) return candidate;
        }
        return null;
    }

    public static IEnumerable<string> SavedVariablesCandidates(string gameRoot, ClientKind client)
    {
        var accounts = AccountsPath(gameRoot, client);
        if (!Directory.Exists(accounts)) return [];
        return Directory.EnumerateDirectories(accounts)
            .Select(account => Path.Combine(account, "SavedVariables", "Realmly.lua"))
            .Where(File.Exists)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    public static string? PreferredSavedVariablesPath(string gameRoot, ClientKind client)
    {
        var existing = SavedVariablesCandidates(gameRoot, client).FirstOrDefault();
        if (existing is not null) return existing;

        var accounts = AccountsPath(gameRoot, client);
        if (!Directory.Exists(accounts)) return null;
        var account = Directory.EnumerateDirectories(accounts)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        return account is null ? null : Path.Combine(account, "SavedVariables", "Realmly.lua");
    }

    public static string AccountsPath(string gameRoot, ClientKind client) => Path.Combine(gameRoot, ClientFolder(client), "WTF", "Account");

    // Retarget only standard SavedVariables paths inside this WoW installation.
    // Keep the account selection even before the new client has written its file.
    internal static string? ClientSavedVariablesPath(string gameRoot, ClientKind client, string? selectedPath)
    {
        if (string.IsNullOrWhiteSpace(gameRoot) || string.IsNullOrWhiteSpace(selectedPath)) return null;
        try
        {
            var parts = Path.GetRelativePath(Path.GetFullPath(gameRoot), Path.GetFullPath(selectedPath))
                .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 6
                || !new[] { "_retail_", "_classic_beta_", "_forever_" }.Contains(parts[0], StringComparer.OrdinalIgnoreCase)
                || !string.Equals(parts[1], "WTF", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(parts[2], "Account", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(parts[4], "SavedVariables", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(parts[5], "Realmly.lua", StringComparison.OrdinalIgnoreCase)) return null;
            return Path.Combine(AccountsPath(gameRoot, client), parts[3], "SavedVariables", "Realmly.lua");
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }

    public static string TocPath(string gameRoot, ClientKind client) => Path.Combine(AddOnsPath(gameRoot, client), "Realmly", "Realmly.toc");

    public static AddonVersion? InstalledVersion(string gameRoot, ClientKind client)
    {
        var path = TocPath(gameRoot, client);
        return File.Exists(path) ? AddonVersion.FromToc(File.ReadAllText(path)) : null;
    }

    private static async Task<byte[]> DownloadAddonAsync(ClientKind client, CancellationToken cancellationToken, int timeoutSeconds = 60)
    {
        var name = client == ClientKind.Retail ? "RealmlyRetail-addon.zip" : "Realmly-addon.zip";
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
        return await http.GetByteArrayAsync($"https://realmly.me/downloads/{name}?check={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}", cancellationToken);
    }

    public static AddonVersion PackageVersion(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var toc = archive.Entries.SingleOrDefault(entry => entry.FullName.Replace('\\', '/') == "Realmly/Realmly.toc")
            ?? throw new InvalidOperationException("The download does not contain Realmly.toc.");
        using var reader = new StreamReader(toc.Open());
        return AddonVersion.FromToc(reader.ReadToEnd())
            ?? throw new InvalidOperationException("The download has no readable addon version.");
    }

    public static async Task<AddonVersion> LatestVersionAsync(ClientKind client, CancellationToken cancellationToken) =>
        PackageVersion(await DownloadAddonAsync(client, cancellationToken, timeoutSeconds: 15));

    public static async Task InstallAsync(string gameRoot, ClientKind client, Action<string>? progress, CancellationToken cancellationToken, Action? beforeReplace = null)
    {
        progress?.Invoke("Downloading the latest Realmly Add-on...");
        var bytes = await DownloadAddonAsync(client, cancellationToken);
        await InstallPackageAsync(bytes, gameRoot, client, progress, cancellationToken, beforeReplace);
    }

    internal static async Task InstallPackageAsync(byte[] bytes, string gameRoot, ClientKind client, Action<string>? progress, CancellationToken cancellationToken, Action? beforeReplace = null)
    {
        var addOns = AddOnsPath(gameRoot, client);
        var destination = Path.Combine(addOns, "Realmly");
        if (!Path.GetFullPath(destination).StartsWith(Path.GetFullPath(addOns) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The selected AddOns folder is not safe to update.");

        PackageVersion(bytes); // Reject bad downloads before touching the installation.
        var temporary = Path.Combine(Path.GetTempPath(), $"realmly-{Guid.NewGuid():N}.zip");
        var stage = Path.Combine(Path.GetDirectoryName(addOns)!, $".Realmly-stage-{Guid.NewGuid():N}");
        var backup = Path.Combine(Path.GetDirectoryName(addOns)!, $".Realmly-backup-{Guid.NewGuid():N}");
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken);
            progress?.Invoke("Preparing your AddOns folder...");
            Directory.CreateDirectory(addOns);
            Directory.CreateDirectory(stage);

            progress?.Invoke("Installing Realmly files...");
            using var archive = ZipFile.OpenRead(temporary);
            var safeRoot = Path.GetFullPath(stage) + Path.DirectorySeparatorChar;
            string? archiveRoot = null;
            var filesInstalled = 0;
            foreach (var entry in archive.Entries)
            {
                var archivePath = entry.FullName.Replace('\\', '/');
                var separator = archivePath.IndexOf('/');
                if (separator <= 0) throw new InvalidOperationException("The add-on archive has an unexpected layout.");
                var entryRoot = archivePath[..separator];
                if (entryRoot is not ("Realmly" or "WoWForeverAtlas" or "WoWForeverAtlasRetail"))
                    throw new InvalidOperationException("The add-on archive has an unexpected layout.");
                if (archiveRoot is null) archiveRoot = entryRoot;
                if (!string.Equals(archiveRoot, entryRoot, StringComparison.Ordinal))
                    throw new InvalidOperationException("The add-on archive has more than one add-on folder.");

                var relativePath = archivePath[(separator + 1)..];
                var target = Path.GetFullPath(Path.Combine(stage, relativePath));
                if (!string.Equals(target, Path.GetFullPath(stage), StringComparison.OrdinalIgnoreCase)
                    && !target.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The add-on archive contains an unsafe path.");
                if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(target); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, overwrite: true);
                filesInstalled++;
            }
            if (!File.Exists(Path.Combine(stage, "Realmly.toc"))) throw new InvalidOperationException("The add-on archive was incomplete.");
            cancellationToken.ThrowIfCancellationRequested();
            // Extract and validate first. Keep the old install until the replacement
            // is ready, and restore it if the final move fails. WTF is never touched.
            beforeReplace?.Invoke();
            if (Directory.Exists(destination)) Directory.Move(destination, backup);
            try { Directory.Move(stage, destination); }
            catch
            {
                if (Directory.Exists(backup) && !Directory.Exists(destination)) Directory.Move(backup, destination);
                throw;
            }
            if (Directory.Exists(backup))
            {
                try { Directory.Delete(backup, recursive: true); }
                catch (IOException) { /* A recoverable old copy is preferable to failing a completed install. */ }
                catch (UnauthorizedAccessException) { }
            }
            progress?.Invoke($"Installed {filesInstalled} Realmly files.");
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            if (Directory.Exists(stage)) Directory.Delete(stage, recursive: true);
        }
    }
}
