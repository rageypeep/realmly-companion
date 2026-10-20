using System.Net;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using RealmlyCompanion;

internal static class DistributionTests
{
    internal static void CheckStorePayload(string directory)
    {
        var assembly = Path.Combine(Path.GetFullPath(directory), "RealmlyCompanion.dll");
        using var stream = File.OpenRead(assembly);
        using var executable = new PEReader(stream);
        var metadata = executable.GetMetadataReader();
        if (metadata.ManifestResources.Any(handle => metadata.GetString(metadata.GetManifestResource(handle).Name).Contains("SelfUpdate"))
            || metadata.TypeDefinitions.Any(handle => metadata.GetString(metadata.GetTypeDefinition(handle).Name) == "SelfUpdater"))
            throw new Exception("Store payload still contains the portable replacement helper");
        if (Directory.EnumerateFiles(directory, "*.ps1", SearchOption.AllDirectories).Any())
            throw new Exception("Store payload contains an unexpected PowerShell helper");
        Console.WriteLine("PASS actual Store payload excludes SelfUpdater implementation and PowerShell resource/files");
    }

    internal static void Run()
    {
        using var handler = new FeedHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://realmly.me/") };
        using var form = new CompanionForm(new CompanionSettings(), api: new RealmlyApi(http));
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var check = (Task)typeof(CompanionForm).GetMethod("CheckForCompanionUpdateAsync", flags)!.Invoke(form, [true])!;
        check.GetAwaiter().GetResult();
        var update = (Button)typeof(CompanionForm).GetField("_companionUpdate", flags)!.GetValue(form)!;
        if (CompanionDistribution.UsesMicrosoftStore)
        {
            if (handler.Requests != 0 || update.Tag is not null)
                throw new Exception("Store build consulted the portable update feed");
            // Even a stale injected offer must not reach the replacement helper.
            update.Tag = new CompanionRelease("99.0.0", "https://realmly.me/downloads/not-used.zip", "fixture");
            ((Task)typeof(CompanionForm).GetMethod("UpdateCompanionAsync", flags)!.Invoke(form, null)!).GetAwaiter().GetResult();
            var status = (Label)typeof(CompanionForm).GetField("_status", flags)!.GetValue(form)!;
            if (handler.Requests != 0 || !status.Text.Contains("Microsoft Store"))
                throw new Exception("Store build attempted a portable update");
            using var options = (Form)typeof(CompanionForm).GetMethod("CreateOptionsDialog", flags)!.Invoke(form, null)!;
            var notification = options.Controls.Find("notifyUpdates", true).Single();
            if (notification.Text.Contains("app and")) throw new Exception("Store notification setting incorrectly claims to control app updates");
        }
        else if (handler.Requests != 1 || update.Tag is not CompanionRelease)
            throw new Exception("Portable build lost its update feed");
        Console.WriteLine("PASS distribution-specific update routing: " + CompanionDistribution.Name);
    }

    private sealed class FeedHandler : HttpMessageHandler
    {
        internal int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""
                {"version":"99.0.0","downloadUrl":"https://realmly.me/downloads/RealmlyCompanion-99.0.0-win-x64.zip","sha256":"fixture"}
                """) });
        }
    }
}
