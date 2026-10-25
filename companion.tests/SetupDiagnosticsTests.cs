using System.Reflection;
using System.Text.Json;
using RealmlyCompanion;

internal static class SetupDiagnosticsTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static T Field<T>(CompanionForm form, string name) => (T)typeof(CompanionForm).GetField(name, Private)!.GetValue(form)!;
    private static object? Call(CompanionForm form, string name, params object[] args) => typeof(CompanionForm).GetMethod(name, Private)!.Invoke(form, args);
    private static void ShowOffscreen(Form form) { form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-30000, -30000); form.Show(); }
    private static IEnumerable<Control> Descendants(Control control)
    { yield return control; foreach (Control child in control.Controls) foreach (var nested in Descendants(child)) yield return nested; }

    public static void Run(string output)
    {
        const string secret = "PRIVATE_MARKER_12345";
        var root = Path.Combine(Path.GetTempPath(), "realmly-setups-" + Guid.NewGuid().ToString("N"), secret);
        var retailLua = Path.Combine(root, "_retail_", "WTF", "Account", "SAME_ACCOUNT", "SavedVariables", "Realmly.lua");
        var foreverLua = Path.Combine(root, "_forever_", "WTF", "Account", "SAME_ACCOUNT", "SavedVariables", "Realmly.lua");
        var betaLua = Path.Combine(root, "_classic_beta_", "WTF", "Account", "SAME_ACCOUNT", "SavedVariables", "Realmly.lua");
        Directory.CreateDirectory(Path.GetDirectoryName(retailLua)!);
        Directory.CreateDirectory(Path.GetDirectoryName(betaLua)!);
        File.WriteAllText(retailLua, "-- old Retail file still exists");
        File.WriteAllText(betaLua, "-- old Classic beta file still exists");
        Assert(!File.Exists(foreverLua), "Forever target fixture must not exist yet");
        Assert(WowFiles.ClientSavedVariablesPath(root, ClientKind.Forever, retailLua) == foreverLua, "Retail to Forever must preserve account");
        Assert(WowFiles.ClientSavedVariablesPath(root, ClientKind.Forever, betaLua) == foreverLua, "Old Classic beta path must be repaired");
        Assert(WowFiles.ClientSavedVariablesPath(root, ClientKind.Retail, foreverLua) == retailLua, "Forever to Retail path");
        Assert(WowFiles.ClientSavedVariablesPath(root, ClientKind.Forever, Path.Combine(root, "backups", "Realmly.lua")) is null, "Custom backup path must not be rewritten");
        Assert(WowFiles.ClientSavedVariablesPath(root, ClientKind.Forever, Path.Combine(root, "..", "other", "_retail_", "WTF", "Account", "OTHER", "SavedVariables", "Realmly.lua")) is null, "Other installation must not be rewritten");
        var pathSettings = new CompanionSettings { GameRoot = root, SavedVariablesPath = retailLua };
        using (var switchingForm = new CompanionForm(pathSettings))
        {
            Field<ComboBox>(switchingForm, "_client").SelectedIndex = 1;
            Assert(Field<TextBox>(switchingForm, "_savedVariables").Text == foreverLua, "Client picker must switch displayed Lua path before target file exists");
            Field<ComboBox>(switchingForm, "_client").SelectedIndex = 0;
            Assert(Field<TextBox>(switchingForm, "_savedVariables").Text == retailLua, "Switching back must restore Retail file");
            var stale = pathSettings.ForClient(ClientKind.Forever, root);
            stale.SavedVariablesPath = betaLua; stale.AutoUpload = true;
            Field<ComboBox>(switchingForm, "_client").SelectedIndex = 1;
            Assert(Field<TextBox>(switchingForm, "_savedVariables").Text == foreverLua && !stale.AutoUpload, "Stale setup must be corrected without carrying auto-upload consent");
            Assert(stale.SavedVariablesPath == foreverLua, "Corrected path must be remembered for the selected setup");
        }
        var legacy = new CompanionSettings { GameRoot = root, SavedVariablesPath = Path.Combine(root, "personal", "Realmly.lua"), Client = ClientKind.Retail, ProtectedAccessToken = secret };
        var retail = legacy.EnsureInstallation();
        Assert(retail.GameRoot == root && retail.SavedVariablesPath == legacy.SavedVariablesPath && legacy.Installations.Count == 1, "Legacy migration lost paths");
        var forever = legacy.ForClient(ClientKind.Forever, root);
        forever.GameRoot = Path.Combine(root, "forever"); forever.SavedVariablesPath = Path.Combine(root, "forever-account", "Realmly.lua");
        legacy.Remember(forever);
        Assert(legacy.ForClient(ClientKind.Retail, null) == retail, "Client selection lost remembered setup");
        var second = legacy.SaveAs(retail, "Second retail");
        second.GameRoot = Path.Combine(root, "other-retail"); second.SavedVariablesPath = Path.Combine(root, "other-account", "Realmly.lua");
        legacy.Remember(second);
        Assert(legacy.ForClient(ClientKind.Retail, null) == second && retail.GameRoot == root, "Multiple installations share paths");
        var restored = JsonSerializer.Deserialize<CompanionSettings>(JsonSerializer.Serialize(legacy))!;
        Assert(restored.EnsureInstallation().Id == second.Id && restored.Installations.Count == 3, "Active setup persistence");
        Assert(restored.ForClient(ClientKind.Forever, null).SavedVariablesPath == forever.SavedVariablesPath, "Independent Forever paths after restart");
        restored.Rename(restored.EnsureInstallation(), "Renamed");
        try { restored.SaveAs(restored.EnsureInstallation(), "Retail"); throw new Exception("Duplicate setup name permitted"); } catch (InvalidOperationException) { }
        var count = restored.Installations.Count;
        restored.Remember(restored.Forget(restored.EnsureInstallation()));
        Assert(restored.Installations.Count == count - 1 && restored.Installations.All(item => item.Id != second.Id), "Forget setup did not remove shortcut");

        var log = new DiagnosticLog();
        log.Record(DiagnosticOperation.Upload, new RealmlyApiException(401, "Bearer " + secret));
        log.Record(DiagnosticOperation.Preview, new IOException(@"C:\Users\" + secret + "\\Realmly.lua"));
        log.Record(DiagnosticOperation.CompanionCheck, new HttpRequestException("https://example.test/?token=" + secret));
        log.Record(DiagnosticOperation.General, new Exception(secret));
        var report = DiagnosticReport.Build(new(ClientKind.Retail, new("0.8.3-" + secret, new Version(0, 8, 3, 0)), null,
            WowRunState.Unknown, true, true, true, 3, true, true, 12, 2, true), log);
        Assert(!report.Contains(secret) && !report.Contains(@"C:\") && !report.Contains("Bearer") && !report.Contains("example.test"), "Sensitive data in diagnostic report");
        Assert(report.Contains("Sign-in or permission required") && report.Contains("Network connection failed") && report.Contains("0.8.3.0"), "Diagnostics lost useful categories/versions");
        var previous = log.Events.Count;
        log.Record(DiagnosticOperation.General, new Exception(secret));
        Assert(log.Events.Count == previous, "Repeated diagnostics spam");
        for (var i = 0; i < 20; i++) log.Record(i % 2 == 0 ? DiagnosticOperation.Upload : DiagnosticOperation.Preview, new IOException(secret));
        Assert(log.Events.Count == 8, "Unbounded diagnostic history");

        string? copied = null;
        using var form = new CompanionForm(legacy, (_, _) => WowRunState.Stopped, text => copied = text);
        ShowOffscreen(form);
        var clients = Field<ComboBox>(form, "_client");
        var roots = Field<TextBox>(form, "_gameRoot");
        var files = Field<TextBox>(form, "_savedVariables");
        var scroll = Field<RealmlyScrollPanel>(form, "_mainScroll");
        scroll.ScrollTo(scroll.MaximumOffset);
        clients.SelectedIndex = 1;
        Assert(scroll.Offset == 0, "Changing client must return to the top");
        Assert(roots.Text == forever.GameRoot && files.Text == forever.SavedVariablesPath, "Retail to Forever UI path restore");
        clients.SelectedIndex = 0;
        Assert(roots.Text == second.GameRoot && files.Text == second.SavedVariablesPath, "Forever to Retail UI path restore");
        Field<ComboBox>(form, "_installations").SelectedItem = retail;
        Assert(roots.Text == retail.GameRoot && files.Text == retail.SavedVariablesPath, "Switching between two Retail installations");
        clients.Focus(); scroll.ScrollTo(scroll.MaximumOffset);
        var finishCheck = new TaskCompletionSource();
        var busyCheck = (Task)Call(form, "BusyAsync", "Checking fixture",
            (Func<CancellationToken, Task>)(_ => finishCheck.Task), DiagnosticOperation.AddonCheck)!;
        Application.DoEvents();
        Assert(scroll.Offset == 0 && !scroll.FollowFocus, "Disabling focused setup controls must not scroll down");
        scroll.ScrollTo(scroll.MaximumOffset);
        finishCheck.SetResult();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!busyCheck.IsCompleted && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(5); }
        busyCheck.GetAwaiter().GetResult();
        Assert(scroll.Offset == 0 && scroll.FollowFocus, "Finishing an addon check must preserve the top and restore normal focus scrolling");
        scroll.ScrollTo(scroll.MaximumOffset);
        var failedCheck = (Task)Call(form, "BusyAsync", "Offline fixture",
            (Func<CancellationToken, Task>)(_ => Task.FromException(new IOException("Offline fixture"))), DiagnosticOperation.AddonCheck)!;
        failedCheck.GetAwaiter().GetResult();
        Assert(scroll.Offset == 0 && scroll.FollowFocus, "Failed addon check must return to the top too");
        var previewCheck = (Task)Call(form, "RefreshPreviewAsync", true)!;
        deadline = DateTime.UtcNow.AddSeconds(5);
        while (!previewCheck.IsCompleted && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(5); }
        previewCheck.GetAwaiter().GetResult();
        var previewStatus = Field<Label>(form, "_recordStatus");
        Assert(previewStatus.Text.Contains("No saved records yet") && !previewStatus.Text.Contains(root), "Missing-file message should not repeat the long path");
        Assert(previewStatus.MaximumSize.Width == previewStatus.Parent!.ClientSize.Width - previewStatus.Parent.Padding.Horizontal - previewStatus.Margin.Horizontal, "Preview status must use full panel width");
        Field<DiagnosticLog>(form, "_diagnostics").Record(DiagnosticOperation.Upload, new InvalidOperationException(secret));
        using (var diagnostics = (Form)Call(form, "CreateDiagnosticsDialog")!)
        {
            ShowOffscreen(diagnostics);
            Assert(copied is null, "Diagnostics copied without explicit action");
            Descendants(diagnostics).OfType<Button>().Single(button => button.Name == "copyDiagnostics").PerformClick();
            Assert(copied is not null && !copied.Contains(secret) && !copied.Contains(root), "Clipboard contains private data");
            using var bitmap = new Bitmap(diagnostics.Width, diagnostics.Height);
            diagnostics.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(output, "companion-diagnostics.png"));
            diagnostics.Close();
        }
        using (var setups = (Form)Call(form, "CreateSetupsDialog")!)
        {
            ShowOffscreen(setups);
            using var bitmap = new Bitmap(setups.Width, setups.Height);
            setups.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(output, "companion-setups.png"));
            foreach (var button in Descendants(setups).OfType<Button>().Where(button => button.Visible)) Assert(button.Right <= button.Parent!.ClientSize.Width, "Setup dialog button overflow");
            setups.Close();
        }
        form.Close();
        Console.WriteLine("PASS legacy setup migration, multiple installs/client switching/persistence, safe categorized diagnostics and explicit clipboard action (stubbed)");
    }
}
