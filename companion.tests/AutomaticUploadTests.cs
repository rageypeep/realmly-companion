using System.Net;
using System.Reflection;
using System.Text.Json;
using RealmlyCompanion;

internal static class AutomaticUploadTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static T Field<T>(CompanionForm form, string name) => (T)typeof(CompanionForm).GetField(name, Private)!.GetValue(form)!;
    private static object? Call(CompanionForm form, string name, params object[] args) => typeof(CompanionForm).GetMethod(name, Private)!.Invoke(form, args);
    private static void Pump(Task task, [System.Runtime.CompilerServices.CallerArgumentExpression("task")] string? stage = null)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(10); }
        Assert(task.IsCompleted, "Automatic upload task did not finish: " + stage + " / " + task.Status);
        task.GetAwaiter().GetResult();
    }

    private static void TestOptionsConsent(string path, string root)
    {
        var settings = new CompanionSettings { GameRoot = root, SavedVariablesPath = path, AccessToken = "fixture-options-token" };
        var installation = settings.EnsureInstallation();
        var accept = true;
        var confirmations = 0;
        CheckBox? checkbox = null;
        using var owner = new CompanionForm(settings, confirmAutomaticUploads: parent =>
        {
            confirmations++;
            Assert(!installation.AutoUpload, "Enabled uploads before consent");
            // Reproduce focus/activation changes during a nested modal message loop.
            using var popup = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000) };
            using var timer = new System.Windows.Forms.Timer { Interval = 30 };
            timer.Tick += (_, _) => { timer.Stop(); checkbox!.Checked = false; popup.DialogResult = accept ? DialogResult.OK : DialogResult.Cancel; popup.Close(); };
            popup.Shown += (_, _) => timer.Start();
            return popup.ShowDialog(parent) == DialogResult.OK;
        });
        using var options = (Form)Call(owner, "CreateOptionsDialog")!;
        options.ShowInTaskbar = false; options.StartPosition = FormStartPosition.Manual; options.Location = new Point(-30000, -30000); options.Show();
        checkbox = options.Controls.Find("autoUpload", true).OfType<CheckBox>().Single();
        var feedback = options.Controls.Find("autoUploadFeedback", true).Single();
        void Click() => typeof(CheckBox).GetMethod("OnClick", Private)!.Invoke(checkbox, [EventArgs.Empty]);
        Click();
        Assert(checkbox.Checked && installation.AutoUpload && feedback.Text.Contains("are on"), "Confirmed checkbox did not enable uploads");
        Assert(JsonSerializer.Deserialize<CompanionSettings>(JsonSerializer.Serialize(settings))!.EnsureInstallation().AutoUpload, "Confirmed opt-in not retained in saved setup");
        Click(); Assert(!checkbox.Checked && !installation.AutoUpload && confirmations == 1, "Disable should not request consent");
        accept = false; Click(); Assert(!checkbox.Checked && !installation.AutoUpload && confirmations == 2, "Cancel enabled uploads");
        settings.AccessToken = null;
        Click(); Assert(!checkbox.Checked && !installation.AutoUpload && confirmations == 2 && feedback.Text.Contains("Connect"), "Missing account not explained in options");
        settings.AccessToken = "fixture-options-token";
        Field<TextBox>(owner, "_savedVariables").Text = Path.Combine(root, "missing.lua");
        Click(); Assert(!checkbox.Checked && !installation.AutoUpload && confirmations == 2 && feedback.Text.Contains("existing"), "Missing Lua not explained in options");
        options.Close();
        Console.WriteLine("PASS options consent: nested modal OK, Cancel, disable, persisted setup state and visible prerequisite errors");
    }

    public static void Run(string output)
    {
        var now = DateTimeOffset.UtcNow;
        var policy = new AutoUploadPolicy();
        Assert(!policy.Observe("scope", "file1", now) && !policy.Observe("scope", "file1", now.AddSeconds(9)), "Unstable file permitted");
        Assert(policy.Observe("scope", "file1", now.AddSeconds(10)), "Stable file not ready");
        policy.Fail(new HttpRequestException("private data"), now.AddSeconds(10));
        Assert(!policy.Observe("scope", "file2", now.AddSeconds(20)) && !policy.Observe("scope", "file2", now.AddSeconds(39)), "Changed file bypassed network backoff");
        Assert(policy.Observe("scope", "file2", now.AddSeconds(40)), "Backoff did not expire");
        policy.Fail(new RealmlyApiException(401, "private token"), now.AddSeconds(40));
        Assert(!policy.Observe("scope", "file3", now.AddHours(1)) && policy.State == AutoUploadState.SignInRequired, "Expired authentication retried repeatedly");
        Assert(!policy.Observe("new-account-token", "file3", now.AddHours(1)) && policy.Observe("new-account-token", "file3", now.AddHours(1).AddSeconds(11)), "New credentials did not reset watcher");
        policy.Complete();
        Assert(!policy.Observe("new-account-token", "file3", now.AddHours(2)), "Unchanged snapshot repeated");

        var root = Path.Combine(Path.GetTempPath(), "realmly-auto-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "Realmly.lua");
        File.WriteAllText(path, UploadTests.Source);
        TestOptionsConsent(path, root);
        var settings = new CompanionSettings { GameRoot = root, SavedVariablesPath = path };
        settings.AccessToken = "fixture-auto-token";
        var installation = settings.EnsureInstallation();
        Assert(!installation.AutoUpload, "Automatic uploads defaulted on");
        var handler = new UploadHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://realmly.me/") };
        using var form = new CompanionForm(settings, (_, _) => WowRunState.Running, api: new RealmlyApi(http));
        form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-30000, -30000); _ = form.Handle;
        Task Tick(int seconds) => (Task)Call(form, "ProcessAutomaticUploadAsync", now.AddSeconds(seconds))!;
        Pump(Tick(0)); Assert(handler.Requests == 0, "Disabled automatic uploads sent data");
        Assert((bool)Call(form, "SetAutoUploadPreference", true)!, "Opt-in failed with a valid file/account");
        Assert(!settings.SaveAs(installation, "Other setup").AutoUpload, "Save as inherited automatic-upload consent");
        Pump(Tick(0)); Assert(handler.Requests == 0, "No debounce on enable");
        Pump(Tick(11)); Assert(handler.Requests == 1 && handler.Sources.Single() == UploadTests.Source, "Wrong snapshot or missing automatic upload");
        var journal = Field<UploadJournal>(form, "_journal");
        Assert(journal.History.Count == 1 && journal.History[0].Automatic && journal.History[0].Succeeded, "Automatic upload history not marked");
        Pump(Tick(30)); Assert(handler.Requests == 1, "Duplicate automatic upload");

        File.WriteAllText(path, UploadTests.Source.Replace("id=\"two\"", "id=\"three\""));
        handler.Status = HttpStatusCode.ServiceUnavailable;
        Pump(Tick(40)); Pump(Tick(51));
        Assert(handler.Requests == 2 && !journal.History[0].Succeeded, "Failed auto upload marked successful");
        Pump(Tick(60)); Assert(handler.Requests == 2, "Failed request retried too soon");
        handler.Status = HttpStatusCode.Created;
        Pump(Tick(85)); Assert(handler.Requests == 3 && journal.History[0].Succeeded, "Automatic retry did not recover");

        File.WriteAllText(path, UploadTests.Source.Replace("id=\"two\"", "id=\"four\""));
        handler.Status = HttpStatusCode.Unauthorized;
        Pump(Tick(100)); Pump(Tick(111)); Pump(Tick(500));
        Assert(handler.Requests == 4 && Field<AutoUploadPolicy>(form, "_autoPolicy").State == AutoUploadState.SignInRequired, "401 did not pause automatic uploads");
        Call(form, "SetAutoUploadPreference", false); Call(form, "SetAutoUploadPreference", true);
        handler.Status = HttpStatusCode.Created;
        Pump(Tick(510)); Pump(Tick(521)); Assert(handler.Requests == 5, "Explicit re-enable did not reset pause");

        File.WriteAllText(path, "RealmlyDB = {");
        Pump(Tick(530)); Pump(Tick(541)); Pump(Tick(600));
        Assert(handler.Requests == 5 && Field<AutoUploadPolicy>(form, "_autoPolicy").State == AutoUploadState.FileNeedsAttention, "Malformed Lua was uploaded or repeatedly attempted");
        File.WriteAllText(path, UploadTests.Source.Replace("id=\"two\"", "id=\"five\""));
        Pump(Tick(610)); Pump(Tick(621)); Assert(handler.Requests == 6, "New saved file did not resume after malformed data");

        File.WriteAllText(path, UploadTests.Source.Replace("id=\"two\"", "id=\"six\""));
        handler.Block = true;
        var beforeCancellation = journal.History.Count;
        Pump(Tick(630));
        var pending = Tick(641);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (handler.Requests < 7 && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(10); }
        Assert(handler.Requests == 7, "Blocked upload did not start");
        Pump(Tick(642)); Assert(handler.Requests == 7, "Overlapping automatic requests");
        Call(form, "SetAutoUploadPreference", false);
        Pump(pending); Assert(!installation.AutoUpload && journal.History.Count == beforeCancellation, "Disable did not cancel in-flight operation");
        handler.Block = false;
        Call(form, "SetAutoUploadPreference", true);
        Field<TextBox>(form, "_savedVariables").Text = Path.Combine(root, "other.lua");
        Assert(!installation.AutoUpload, "Changing selected file retained upload consent");
        Pump(Tick(700)); Assert(handler.Requests == 7, "Changed file was sent without consent");
        Field<TextBox>(form, "_savedVariables").Text = path;
        Call(form, "SetAutoUploadPreference", true);
        var restored = JsonSerializer.Deserialize<CompanionSettings>(JsonSerializer.Serialize(settings))!;
        Assert(restored.EnsureInstallation().AutoUpload, "Opt-in was not persisted per setup");
        var other = settings.Installations.Single(item => item != installation);
        Call(form, "ActivateInstallation", other);
        Pump(Tick(710)); Assert(handler.Requests == 7 && !other.AutoUpload && installation.AutoUpload, "Inactive or unapproved setup uploaded");
        Call(form, "ActivateInstallation", installation);
        Call(form, "AcceptConnectedToken", "different-fixture-token");
        Assert(settings.Installations.All(item => !item.AutoUpload), "Reconnecting retained previous upload consent");
        Pump(Tick(720)); Assert(handler.Requests == 7, "Reconnected account uploaded without renewed consent");
        form.Show();
        using (var image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(output, "companion-auto-upload.png")); }
        form.Close();
        using (var closing = new CompanionForm(new CompanionSettings { GameRoot = root, SavedVariablesPath = path }))
        {
            _ = closing.Handle;
            var token = Field<CancellationTokenSource>(closing, "_lifetime").Token;
            var reading = (Task)Call(closing, "ReadPreviewAsync", token)!;
            closing.Dispose();
            try { Pump(reading); } catch (OperationCanceledException) { }
            Assert(token.IsCancellationRequested, "Disposing the window did not cancel background reads");
        }
        Console.WriteLine("PASS opt-in/default-off, stable-file debounce, exact snapshots, history, duplicate suppression, retries, auth/invalid-file pauses, cancellation, single-flight and path-change consent");
    }

    private sealed class UploadHandler : HttpMessageHandler
    {
        public int Requests;
        public bool Block;
        public HttpStatusCode Status = HttpStatusCode.Created;
        public List<string> Sources = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert(request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/api/collector-uploads", "Automatic upload used a post-publishing or unexpected endpoint");
            Assert(request.Headers.Authorization?.Parameter == "fixture-auto-token", "Automatic upload lost authentication");
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Sources.Add(json.RootElement.GetProperty("savedVariables").GetString()!); Requests++;
            if (Block) await Task.Delay(Timeout.Infinite, cancellationToken);
            return new(Status) { Content = new StringContent(Status == HttpStatusCode.Created ? "{\"ok\":true,\"batch\":true,\"importedCount\":2,\"locationCount\":3}" : "{\"error\":\"fixture failure\"}") };
        }
    }
}
