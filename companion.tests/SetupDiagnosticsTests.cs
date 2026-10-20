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
        clients.SelectedIndex = 1;
        Assert(roots.Text == forever.GameRoot && files.Text == forever.SavedVariablesPath, "Retail to Forever UI path restore");
        clients.SelectedIndex = 0;
        Assert(roots.Text == second.GameRoot && files.Text == second.SavedVariablesPath, "Forever to Retail UI path restore");
        Field<ComboBox>(form, "_installations").SelectedItem = retail;
        Assert(roots.Text == retail.GameRoot && files.Text == retail.SavedVariablesPath, "Switching between two Retail installations");
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
