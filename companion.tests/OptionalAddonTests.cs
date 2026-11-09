using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using RealmlyCompanion;

internal static class OptionalAddonTests
{
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    private static byte[] Package(OptionalAddon addon, string version, string? extra = null, bool link = false, int api = 16001)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            using (var writer = new StreamWriter(zip.CreateEntry(addon.Folder + "/" + addon.Folder + ".toc").Open()))
                writer.Write("## Interface: " + api + "\n## Version: " + version + "\nCore.lua\n");
            using (var writer = new StreamWriter(zip.CreateEntry(addon.Folder + "/Core.lua").Open())) writer.Write("-- " + version);
            if (extra is not null)
            {
                var entry = zip.CreateEntry(extra); if (link) entry.ExternalAttributes = unchecked((int)0xa1ff0000);
                using var writer = new StreamWriter(entry.Open()); writer.Write("unexpected");
            }
        }
        return stream.ToArray();
    }
    private static OptionalAddonRelease Release(OptionalAddon addon, byte[] bytes, string version) =>
        new(addon.Id, version, "https://realmly.me/downloads/" + addon.PackagePrefix + "-" + version + "-forever.zip", Convert.ToHexString(SHA256.HashData(bytes)));
    private static void Rejected(Action action, string message)
    {
        try { action(); } catch (Exception error) when (error is InvalidOperationException or IOException or OperationCanceledException) { return; }
        throw new Exception("Expected rejection: " + message);
    }
    private static void Pump(Task task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(5); }
        Assert(task.IsCompleted, "Optional add-on operation timed out"); task.GetAwaiter().GetResult();
    }
    private static IEnumerable<Control> Children(Control control)
    {
        foreach (Control child in control.Controls) { yield return child; foreach (var nested in Children(child)) yield return nested; }
    }
    internal static void Run(string output)
    {
        var root = Path.Combine(Path.GetFullPath(output), "optional-install-" + Guid.NewGuid().ToString("N"));
        var forever = Path.Combine(root, "_forever_"); Directory.CreateDirectory(forever);
        var retail = Path.Combine(root, "_retail_", "Interface", "AddOns"); Directory.CreateDirectory(retail);
        File.WriteAllText(Path.Combine(retail, "keep.txt"), "retail");
        var saved = Path.Combine(forever, "WTF", "Account", "test", "SavedVariables"); Directory.CreateDirectory(saved);
        File.WriteAllText(Path.Combine(saved, "RealmlyGathering.lua"), "saved herb history");
        var recorder = Path.Combine(forever, "Interface", "AddOns", "Realmly"); Directory.CreateDirectory(recorder);
        File.WriteAllText(Path.Combine(recorder, "keep.lua"), "main recorder");
        var packages = new Dictionary<string, byte[]>(); var releases = new Dictionary<string, OptionalAddonRelease>();
        foreach (var addon in OptionalAddons.All)
        {
            var version = addon == OptionalAddons.ChatCopy ? "0.1.3" : "0.2.1";
            var bytes = Package(addon, version); packages[addon.Id] = bytes; releases[addon.Id] = Release(addon, bytes, version);
            OptionalAddons.InstallPackage(bytes, root, addon, releases[addon.Id], () => WowRunState.Stopped, default);
            Assert(OptionalAddons.Installed(root, addon)?.Display == version, "Correct optional folder/version");
        }
        var chat = OptionalAddons.ChatCopy; var target = OptionalAddons.Target(root, chat);
        var newer = Package(chat, "0.1.4"); var offer = Release(chat, newer, "0.1.4");
        foreach (var path in new[] { "RealmlyChatCopy/../escape.lua", "Realmly/Realmly.toc", "RealmlyChatCopy/stream:ads.lua", "RealmlyChatCopy/CON.lua", "RealmlyChatCopy/core.lua" })
        {
            var bytes = Package(chat, "0.1.4", path);
            Rejected(() => OptionalAddons.InstallPackage(bytes, root, chat, Release(chat, bytes, "0.1.4"), () => WowRunState.Stopped, default), path);
        }
        var symlink = Package(chat, "0.1.4", "RealmlyChatCopy/link.lua", link: true);
        Rejected(() => OptionalAddons.InstallPackage(symlink, root, chat, Release(chat, symlink, "0.1.4"), () => WowRunState.Stopped, default), "ZIP symlink");
        var wrongClient = Package(chat, "0.1.4", api: 120105);
        Rejected(() => OptionalAddons.InstallPackage(wrongClient, root, chat, Release(chat, wrongClient, "0.1.4"), () => WowRunState.Stopped, default), "Retail package");
        Rejected(() => OptionalAddons.InstallPackage(newer, root, chat, offer with { Sha256 = new string('0', 64) }, () => WowRunState.Stopped, default), "Hash mismatch");
        Rejected(() => OptionalAddons.InstallPackage(newer, root, chat, offer with { Version = "0.1.9", DownloadUrl = offer.DownloadUrl.Replace("0.1.4", "0.1.9") }, () => WowRunState.Stopped, default), "TOC mismatch");
        foreach (var state in new[] { WowRunState.Running, WowRunState.Unknown })
            Rejected(() => OptionalAddons.InstallPackage(newer, root, chat, offer, () => state, default), "Game not stopped");
        var runState = WowRunState.Stopped;
        Rejected(() => OptionalAddons.InstallPackage(newer, root, chat, offer, () => runState, default, () => runState = WowRunState.Running), "Game restarts before replacement");
        using var cancel = new CancellationTokenSource();
        Rejected(() => OptionalAddons.InstallPackage(newer, root, chat, offer, () => WowRunState.Stopped, cancel.Token, cancel.Cancel), "Cancel before commit");
        Rejected(() => OptionalAddons.InstallPackage(newer, root, chat, offer, () => WowRunState.Stopped, default,
            moveDirectory: (from, to) => { if (from.Contains("-stage-")) throw new IOException("Simulated final move failure"); Directory.Move(from, to); }), "Rollback on failed commit");
        Assert(File.ReadAllText(Path.Combine(target, "Core.lua")) == "-- 0.1.3", "Every rejection preserves the old add-on");
        OptionalAddons.InstallPackage(newer, root, chat, offer, () => WowRunState.Stopped, default);
        Assert(OptionalAddons.Installed(root, chat)?.Display == "0.1.4", "Upgrade commits");
        Rejected(() => OptionalAddons.InstallPackage(packages[chat.Id], root, chat, releases[chat.Id], () => WowRunState.Stopped, default), "Never silently downgrade");
        Assert(File.ReadAllText(Path.Combine(saved, "RealmlyGathering.lua")) == "saved herb history"
            && File.ReadAllText(Path.Combine(retail, "keep.txt")) == "retail" && File.ReadAllText(Path.Combine(recorder, "keep.lua")) == "main recorder", "SavedVariables, Retail and recorder isolation");
        Assert(!Directory.EnumerateDirectories(Path.Combine(forever, "Interface"), ".*").Any(), "No temporary installation state");

        foreach (var state in new[] { WowRunState.Running, WowRunState.Unknown })
            Rejected(() => OptionalAddons.Uninstall(root, chat, () => state, default), "Uninstall while game state is unsafe");
        Rejected(() => OptionalAddons.Uninstall(root, chat, () => WowRunState.Stopped, cancel.Token), "Cancelled uninstall");
        Rejected(() => OptionalAddons.Uninstall(root, chat, () => WowRunState.Stopped, default, () => throw new InvalidOperationException("Setup changed")), "Changed uninstall target");
        runState = WowRunState.Stopped;
        Rejected(() => OptionalAddons.Uninstall(root, chat, () => runState, default, () => runState = WowRunState.Running), "Game opens before uninstall");
        Assert(Directory.Exists(target), "Rejected uninstall preserves the add-on");
        OptionalAddons.Uninstall(root, chat, () => WowRunState.Stopped, default);
        Assert(!Directory.Exists(target) && Directory.Exists(OptionalAddons.Target(root, OptionalAddons.Sparkles)), "Remove only the selected add-on");
        OptionalAddons.Uninstall(root, OptionalAddons.Sparkles, () => WowRunState.Stopped, default);
        Assert(File.ReadAllText(Path.Combine(saved, "RealmlyGathering.lua")) == "saved herb history"
            && File.ReadAllText(Path.Combine(retail, "keep.txt")) == "retail" && File.ReadAllText(Path.Combine(recorder, "keep.lua")) == "main recorder", "Uninstall preserves history, Retail and main recorder");
        Assert(!Directory.EnumerateDirectories(Path.Combine(forever, "Interface"), ".*").Any(), "Successful uninstall leaves no removal folder");

        var linkRoot = root + "-linked";
        try
        {
            Directory.CreateSymbolicLink(linkRoot, root);
            Rejected(() => OptionalAddons.Target(linkRoot, chat), "Junction/symlink installation root");
        }
        catch (Exception error) when (error is UnauthorizedAccessException or IOException)
        { Console.WriteLine("SKIP OS symlink fixture: " + error.GetType().Name); }
        finally
        {
            if (Directory.Exists(linkRoot) && Path.GetDirectoryName(linkRoot) == Path.GetDirectoryName(root)
                && (File.GetAttributes(linkRoot) & FileAttributes.ReparsePoint) != 0) Directory.Delete(linkRoot);
        }

        // Run the real UI/network adapter against a no-auth fixture catalog and ZIPs.
        var uiRoot = Path.Combine(root, "ui"); Directory.CreateDirectory(Path.Combine(uiRoot, "_forever_"));
        using var handler = new Handler(packages, releases); using var http = new HttpClient(handler);
        using var manager = new OptionalAddons(http); var selectedRoot = uiRoot; runState = WowRunState.Stopped;
        using var host = new Form { ClientSize = new Size(900, 830), StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000) };
        var approved = false; var confirmed = 0; string? confirmedTarget = null;
        using var panel = new AddonsPanel(gameRoot: () => selectedRoot, detectWow: (_, client) => { Assert(client == ClientKind.Forever, "Always check Forever"); return runState; }, manager: manager, confirmUninstall: (addon, path) => { confirmed++; confirmedTarget = path; return approved; });
        panel.Dock = DockStyle.Fill; host.Controls.Add(panel); host.Show(); Application.DoEvents(); Pump(panel.CheckUpdatesAsync());
        Assert(handler.Packages == 0 && !Directory.Exists(Path.Combine(uiRoot, "_forever_", "Interface")), "Automatic checks never install");
        runState = WowRunState.Running; Pump(panel.InstallAsync(chat)); Assert(handler.Packages == 0, "Running game blocks download");
        runState = WowRunState.Stopped; Pump(panel.InstallAsync(chat));
        Assert(OptionalAddons.Installed(uiRoot, chat)?.Display == "0.1.3" && Children(panel).OfType<Button>().Any(b => b.Text == "Up to date"), "One-click verified install and status");
        handler.BeforePackage = () => selectedRoot = root;
        Pump(panel.InstallAsync(OptionalAddons.Sparkles)); Assert(!Directory.Exists(Path.Combine(uiRoot, "_forever_", "Interface", "AddOns", "RealmlyGathering")), "Changed setup blocks stale install");
        selectedRoot = uiRoot; handler.BeforePackage = null;
        Pump(panel.InstallAsync(OptionalAddons.Sparkles));
        Assert(OptionalAddons.Installed(uiRoot, OptionalAddons.Sparkles)?.Display == "0.2.1", "Sparkles uses the compatible folder");
        panel.Uninstall(chat);
        Assert(confirmed == 1 && confirmedTarget == OptionalAddons.Target(uiRoot, chat) && OptionalAddons.Installed(uiRoot, chat) is not null, "Uninstall confirmation cancellation preserves files");
        approved = true; runState = WowRunState.Running; panel.Uninstall(chat);
        Assert(confirmed == 1 && OptionalAddons.Installed(uiRoot, chat) is not null, "Running game stops uninstall before confirmation");
        runState = WowRunState.Stopped; panel.Uninstall(chat);
        Assert(OptionalAddons.Installed(uiRoot, chat) is null && OptionalAddons.Installed(uiRoot, OptionalAddons.Sparkles) is not null, "Confirmed UI uninstall removes only Chat Copy");
        Assert(Children(panel).OfType<Button>().Any(b => b.Text == "Install Chat Copy" && b.Enabled), "Allow reinstall after uninstall");
        using (var image = new Bitmap(host.Width, host.Height)) { host.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(output, "optional-installed.png")); }
        host.Close();
        Console.WriteLine("PASS optional installers: verified catalog/hash/TOC, Forever-only paths, settings isolation, unsafe archives, game checks, cancellation, rollback, upgrades, no downgrades, automatic checks, one-click UI and confirmed uninstall");
    }
    internal static void RealPackages(string output, string downloads)
    {
        var root = Path.Combine(Path.GetFullPath(output), "real-packages-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "_forever_"));
        foreach (var addon in OptionalAddons.All)
        {
            var version = addon == OptionalAddons.ChatCopy ? "0.1.4" : "0.2.2";
            var bytes = File.ReadAllBytes(Path.Combine(downloads, addon.PackagePrefix + "-" + version + "-forever.zip"));
            OptionalAddons.InstallPackage(bytes, root, addon, Release(addon, bytes, version), () => WowRunState.Stopped, default);
            Assert(OptionalAddons.Installed(root, addon)?.Display == version, "Actual release ZIP failed to install");
        }
        Console.WriteLine("PASS actual published Chat Copy/Sparkles ZIPs install into an isolated Forever fixture");
    }
    private sealed class Handler(Dictionary<string, byte[]> packages, Dictionary<string, OptionalAddonRelease> releases) : HttpMessageHandler
    {
        internal int Packages; internal Action? BeforePackage;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert(request.Headers.Authorization is null, "Optional add-ons need no account token");
            if (request.RequestUri!.AbsolutePath.EndsWith(".json"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { addons = releases.Values })) });
            var release = releases.Values.Single(r => r.DownloadUrl == request.RequestUri.AbsoluteUri);
            Packages++; BeforePackage?.Invoke();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(packages[release.Id]) });
        }
    }
}
