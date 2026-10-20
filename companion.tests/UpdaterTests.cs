using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using RealmlyCompanion;

internal static class UpdaterTests
{
    private static string SourceDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }

    internal static async Task RunAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Realmly updater O'Brien & spaces " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var fixture = Path.Combine(SourceDirectory(), "UpdaterProbe", "bin", "Release", "net8.0");
        foreach (var file in Directory.GetFiles(fixture)) File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
        var target = Path.Combine(directory, "RealmlyCompanion.exe");
        File.Copy(Path.Combine(directory, "UpdaterProbe.exe"), target);
        var original = Hash(target);
        var zipped = Path.Combine(directory, "update.zip");
        using (var zip = ZipFile.Open(zipped, ZipArchiveMode.Create)) zip.CreateEntryFromFile(target, "RealmlyCompanion.exe");
        var release = new CompanionRelease("1.2.3", "https://realmly.me/downloads/RealmlyCompanion-1.2.3-win-x64.zip", null, Hash(zipped));
        foreach (var bad in new[] { release with { Sha256 = null }, release with { DownloadUrl = "https://example.com/update.zip" }, release with { DownloadUrl = "http://realmly.me/downloads/RealmlyCompanion-1.2.3-win-x64.zip" } })
        {
            try { SelfUpdater.ValidateRelease(bad); throw new Exception("Untrusted release accepted"); }
            catch (InvalidOperationException) { }
        }
        var rejected = Path.Combine(directory, "rejected");
        Directory.CreateDirectory(rejected);
        try
        {
            await SelfUpdater.VerifyAndExtractAsync(zipped, rejected, release with { Sha256 = new string('0', 64) }, default);
            throw new Exception("Bad hash accepted");
        }
        catch (InvalidOperationException) { }
        Require(!File.Exists(Path.Combine(rejected, "RealmlyCompanion.exe")) && Hash(target) == original, "Bad download changed installed app");
        try
        {
            await SelfUpdater.VerifyAndExtractAsync(zipped, rejected, release with { Version = "1.2.4", DownloadUrl = "https://realmly.me/downloads/RealmlyCompanion-1.2.4-win-x64.zip" }, default);
            throw new Exception("Wrong executable version accepted");
        }
        catch (InvalidOperationException) { }
        var stage = Path.Combine(directory, "staged");
        Directory.CreateDirectory(stage);
        var prepared = await SelfUpdater.VerifyAndExtractAsync(zipped, stage, release, default);
        // A harmless trailing byte makes the fixture update distinguishable.
        await using (var append = new FileStream(prepared.Executable, FileMode.Append)) append.WriteByte(0);
        prepared = prepared with { Sha256 = Hash(prepared.Executable) };
        using (var parent = Process.Start(new ProcessStartInfo(target, "wait") { UseShellExecute = false, CreateNoWindow = true })!)
        {
            await SelfUpdater.LaunchHelperAsync(prepared, target, parent.Id, parent.StartTime.ToUniversalTime().Ticks, default, quietErrors: true);
            Require(Hash(target) == original && !parent.HasExited, "App replaced before old process exited");
            parent.Kill();
            await parent.WaitForExitAsync();
        }
        var marker = Path.Combine(directory, "restarted.txt");
        await WaitForAsync(marker);
        Require(Hash(target) == prepared.Sha256, "Restarted app is not the staged update");
        // Force a launch failure: the helper must restore and restart the good EXE.
        File.Delete(marker);
        var failureStage = Path.Combine(directory, "failure");
        Directory.CreateDirectory(failureStage);
        var invalid = Path.Combine(failureStage, "RealmlyCompanion.exe");
        File.WriteAllText(invalid, "not an executable");
        using (var parent = Process.Start(new ProcessStartInfo(target, "wait") { UseShellExecute = false, CreateNoWindow = true })!)
        {
            await SelfUpdater.LaunchHelperAsync(new PreparedUpdate(failureStage, invalid, Hash(invalid)), target,
                parent.Id, parent.StartTime.ToUniversalTime().Ticks, default, quietErrors: true);
            parent.Kill();
            await parent.WaitForExitAsync();
        }
        await WaitForAsync(Path.Combine(failureStage, "update-error.txt"));
        await WaitForAsync(marker);
        Require(Hash(target) == prepared.Sha256, "Failed restart did not restore previous executable");
        Console.WriteLine("PASS self-updater: trusted origin, SHA256, EXE version, locked-process handover, replacement/restart, spaces/quotes and failure rollback");
    }

    private static async Task WaitForAsync(string path)
    {
        for (var i = 0; i < 150 && !File.Exists(path); i++) await Task.Delay(100);
        Require(File.Exists(path), "Timed out waiting for " + path);
    }
}
