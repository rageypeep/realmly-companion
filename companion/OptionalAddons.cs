using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RealmlyCompanion;

internal sealed record OptionalAddon(string Id, string Name, string Folder, string PackagePrefix);
internal sealed record OptionalAddonRelease(string Id, string Version, string DownloadUrl, string Sha256);
internal sealed record OptionalAddonCatalog(OptionalAddonRelease[] Addons);

// Optional tools always target Forever, independently of the main recorder's client selection.
internal sealed class OptionalAddons : IDisposable
{
    internal static readonly OptionalAddon ChatCopy = new("chat-copy", "Chat Copy", "RealmlyChatCopy", "RealmlyChatCopy");
    internal static readonly OptionalAddon Sparkles = new("sparkles", "Sparkles", "RealmlyGathering", "RealmlySparkles");
    internal static readonly OptionalAddon[] All = [ChatCopy, Sparkles];
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    internal OptionalAddons(HttpClient? http = null)
    {
        _ownsHttp = http is null;
        _http = http ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(60) };
    }
    internal async Task<Dictionary<string, OptionalAddonRelease>> CheckAsync(CancellationToken ct)
    {
        var bytes = await DownloadAsync("https://realmly.me/downloads/RealmlyOptionalAddons.json?check=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 16_384, ct);
        var catalog = JsonSerializer.Deserialize<OptionalAddonCatalog>(bytes, Json);
        if (catalog?.Addons is not { Length: 2 }) throw new InvalidOperationException("The add-on release list is incomplete.");
        var results = new Dictionary<string, OptionalAddonRelease>();
        foreach (var release in catalog.Addons)
        {
            var addon = All.SingleOrDefault(a => a.Id == release?.Id) ?? throw new InvalidOperationException("Unknown optional add-on.");
            ValidateRelease(addon, release!);
            if (!results.TryAdd(addon.Id, release!)) throw new InvalidOperationException("Duplicate add-on release.");
        }
        return results;
    }
    internal static void ValidateRelease(OptionalAddon addon, OptionalAddonRelease release)
    {
        if (!All.Contains(addon) || release.Id != addon.Id || !Version.TryParse(release.Version, out _)
            || !Regex.IsMatch(release.Version ?? "", @"^\d+\.\d+\.\d+$")
            || !Regex.IsMatch(release.Sha256 ?? "", "^[a-fA-F0-9]{64}$")
            || release.DownloadUrl != "https://realmly.me/downloads/" + addon.PackagePrefix + "-" + release.Version + "-forever.zip")
            throw new InvalidOperationException("The optional add-on release is invalid.");
    }
    private async Task<byte[]> DownloadAsync(string url, int maximum, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(60));
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > maximum) throw new InvalidOperationException("The add-on download is too large.");
        await using var source = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var target = new MemoryStream(); var buffer = new byte[16384]; int read;
        while ((read = await source.ReadAsync(buffer, timeout.Token)) > 0)
        {
            if (target.Length + read > maximum) throw new InvalidOperationException("The add-on download is too large.");
            target.Write(buffer, 0, read);
        }
        return target.ToArray();
    }
    internal static string Target(string root, OptionalAddon addon)
    {
        if (!All.Contains(addon) || string.IsNullOrWhiteSpace(root) || !Directory.Exists(Path.Combine(root, "_forever_")))
            throw new InvalidOperationException("Choose a WoW folder containing _forever_ in the Realmly tab first.");
        var target = Path.GetFullPath(Path.Combine(WowFiles.AddOnsPath(root, ClientKind.Forever), addon.Folder));
        NoLinks(target); return target;
    }
    private static void NoLinks(string path)
    {
        for (var cursor = Path.GetFullPath(path); !string.IsNullOrEmpty(cursor); cursor = Path.GetDirectoryName(cursor))
        {
            try
            {
                if ((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("The add-on path contains a link or junction. Choose a direct WoW installation folder.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
    internal static AddonVersion? Installed(string root, OptionalAddon addon)
    {
        var toc = Path.Combine(Target(root, addon), addon.Folder + ".toc");
        if (!File.Exists(toc)) return null;
        if (new FileInfo(toc).Length > 65536) throw new InvalidOperationException("The installed add-on TOC is too large to read safely.");
        return AddonVersion.FromToc(File.ReadAllText(toc));
    }
    internal async Task InstallAsync(string root, OptionalAddon addon, OptionalAddonRelease release,
        Func<WowRunState> state, Action<string> progress, CancellationToken ct, Action? beforeReplace = null)
    {
        ValidateRelease(addon, release); Target(root, addon); WowProcessDetector.RequireStopped(state());
        progress("Downloading " + addon.Name + " " + release.Version + "...");
        var bytes = await DownloadAsync(release.DownloadUrl, 8 * 1024 * 1024, ct);
        InstallPackage(bytes, root, addon, release, state, ct, beforeReplace);
        progress(addon.Name + " " + release.Version + " installed. Start Forever and enable it at character selection.");
    }
    internal static void InstallPackage(byte[] bytes, string root, OptionalAddon addon, OptionalAddonRelease release,
        Func<WowRunState> state, CancellationToken ct, Action? beforeReplace = null, Action<string, string>? moveDirectory = null)
    {
        ValidateRelease(addon, release); var target = Target(root, addon);
        using var installationLock = new InstallationLock(target);
        WowProcessDetector.RequireStopped(state()); ct.ThrowIfCancellationRequested();
        if (bytes.Length > 8 * 1024 * 1024 || !Convert.ToHexString(SHA256.HashData(bytes)).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Add-on verification failed. Your current installation has not been changed.");
        var move = moveDirectory ?? Directory.Move;
        var current = Installed(root, addon);
        if (current is not null && current.Number > Version.Parse(release.Version + ".0"))
            throw new InvalidOperationException("Your installed add-on is newer than this release. It has been kept.");
        // Validate every entry before creating any directories or replacing files.
        using var input = new MemoryStream(bytes); using var zip = new ZipArchive(input, ZipArchiveMode.Read);
        if (zip.Entries.Count is 0 or > 256) throw new InvalidOperationException("Unexpected add-on archive size.");
        var entries = new List<(ZipArchiveEntry Entry, string Relative)>(); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expanded = 0;
        foreach (var entry in zip.Entries)
        {
            var path = entry.FullName.Replace('\\', '/');
            if (!path.StartsWith(addon.Folder + "/", StringComparison.Ordinal) || path.Contains(':')
                || path.Split('/').Any(p => p is "." or "..") || ((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000
                || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("The add-on archive contains an unsafe path or link.");
            var relative = path[(addon.Folder.Length + 1)..].TrimEnd('/');
            if (relative == "") continue;
            if (!names.Add(relative) || relative.Split('/').Any(p => p.Length == 0 || p.EndsWith(' ') || p.EndsWith('.') || Regex.IsMatch(p, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)", RegexOptions.IgnoreCase)))
                throw new InvalidOperationException("The add-on archive contains duplicate or ambiguous paths.");
            if (entry.Name.Length == 0) continue;
            if (!new[] { ".lua", ".toc", ".xml", ".png", ".tga", ".blp", ".txt", ".md", ".ogg", ".wav" }.Contains(Path.GetExtension(relative).ToLowerInvariant()))
                throw new InvalidOperationException("Unexpected file type in the add-on archive.");
            expanded += entry.Length;
            if (entry.Length > 8 * 1024 * 1024 || expanded > 24 * 1024 * 1024) throw new InvalidOperationException("The expanded add-on is too large.");
            entries.Add((entry, relative));
        }
        var toc = entries.SingleOrDefault(e => e.Relative == addon.Folder + ".toc").Entry
            ?? throw new InvalidOperationException("The add-on TOC is missing.");
        if (toc.Length > 65536) throw new InvalidOperationException("The add-on TOC is too large.");
        using (var reader = new StreamReader(toc.Open()))
        {
            var text = reader.ReadToEnd();
            if (AddonVersion.FromToc(text)?.Number != Version.Parse(release.Version + ".0") || !Regex.IsMatch(text, @"(?m)^## Interface:\s*16001\s*$"))
                throw new InvalidOperationException("The add-on version or Forever interface does not match its release.");
        }
        var parent = Path.GetDirectoryName(Path.GetDirectoryName(target))!; // Forever/Interface, outside loaded AddOns.
        var stage = Path.Combine(parent, "." + addon.Folder + "-stage-" + Guid.NewGuid().ToString("N"));
        var backup = Path.Combine(parent, "." + addon.Folder + "-backup-" + Guid.NewGuid().ToString("N"));
        var committed = false;
        try
        {
            Directory.CreateDirectory(stage); var prefix = Path.GetFullPath(stage) + Path.DirectorySeparatorChar;
            foreach (var (entry, relative) in entries)
            {
                ct.ThrowIfCancellationRequested(); var path = Path.GetFullPath(Path.Combine(stage, relative));
                if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe extraction path.");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using var source = entry.Open(); using var destination = new FileStream(path, FileMode.CreateNew);
                var buffer = new byte[16384]; long written = 0; int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ct.ThrowIfCancellationRequested(); written += read;
                    if (written > entry.Length) throw new InvalidOperationException("The expanded add-on size did not match the archive.");
                    destination.Write(buffer, 0, read);
                }
                if (written != entry.Length) throw new InvalidOperationException("The add-on archive is incomplete.");
            }
            ct.ThrowIfCancellationRequested(); beforeReplace?.Invoke(); ct.ThrowIfCancellationRequested();
            WowProcessDetector.RequireStopped(state()); NoLinks(target); NoLinks(stage);
            current = Installed(root, addon);
            if (current is not null && current.Number > Version.Parse(release.Version + ".0")) throw new InvalidOperationException("A newer local add-on was found; installation stopped.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (Directory.Exists(target)) move(target, backup);
            try { move(stage, target); committed = true; }
            catch { if (Directory.Exists(backup) && !Directory.Exists(target)) move(backup, target); throw; }
        }
        finally
        {
            RemoveOwned(stage, parent);
            // Keep the recovery copy if a third party added a link or a file is locked.
            if (committed) RemoveOwned(backup, parent);
        }
    }
    internal static string Uninstall(string root, OptionalAddon addon, Func<WowRunState> state, CancellationToken ct, Action? beforeRemove = null)
    {
        var target = Target(root, addon);
        using var installationLock = new InstallationLock(target);
        WowProcessDetector.RequireStopped(state()); ct.ThrowIfCancellationRequested();
        if (!Directory.Exists(target)) return addon.Name + " is not installed.";
        beforeRemove?.Invoke(); ct.ThrowIfCancellationRequested();
        WowProcessDetector.RequireStopped(state()); NoLinks(target);
        var parent = Path.GetDirectoryName(Path.GetDirectoryName(target))!;
        var removed = Path.Combine(parent, "." + addon.Folder + "-uninstalled-" + Guid.NewGuid().ToString("N"));
        // Remove from the game's loaded AddOns directory first, then clean only this copy.
        // WTF/SavedVariables are outside this path and are deliberately preserved.
        Directory.Move(target, removed);
        RemoveOwned(removed, parent);
        return addon.Name + " uninstalled. Saved settings are kept."
            + (addon == Sparkles ? " Saved herb history is kept." : "")
            + (Directory.Exists(removed) ? " Windows kept a recovery copy at: " + removed : "");
    }
    private static void RemoveOwned(string path, string parent)
    {
        if (!Directory.Exists(path)) return;
        if (Path.GetDirectoryName(Path.GetFullPath(path)) != Path.GetFullPath(parent)) return;
        try
        {
            NoLinks(path);
            var stack = new Stack<string>(); stack.Push(path); var inspected = 0;
            while (stack.TryPop(out var folder))
                foreach (var item in Directory.EnumerateFileSystemEntries(folder))
                {
                    if (++inspected > 4096) return;
                    var attributes = File.GetAttributes(item);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) return;
                    if ((attributes & FileAttributes.Directory) != 0) stack.Push(item);
                }
            Directory.Delete(path, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException) { }
    }
    private sealed class InstallationLock : IDisposable
    {
        private readonly Mutex _mutex;
        internal InstallationLock(string target)
        {
            var key = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(target.ToUpperInvariant())));
            _mutex = new Mutex(false, "Local\\Realmly.OptionalAddon." + key);
            bool acquired;
            try { acquired = _mutex.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) { _mutex.Dispose(); throw new InvalidOperationException("This add-on is already being installed by another Companion. Try again shortly."); }
        }
        public void Dispose() { _mutex.ReleaseMutex(); _mutex.Dispose(); }
    }
    public void Dispose() { if (_ownsHttp) _http.Dispose(); }
}
