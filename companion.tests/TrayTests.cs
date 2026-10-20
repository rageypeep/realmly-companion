using System.IO.Compression;
using System.Reflection;
using RealmlyCompanion;

internal static class TrayTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static object? Call(CompanionForm form, string name, params object[] args) => typeof(CompanionForm).GetMethod(name, Private)!.Invoke(form, args);
    private static void ShowOffscreen(Form form)
    {
        form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-30000, -30000); form.Show();
    }

    public static void Run(string output)
    {
        var root = Path.Combine(Path.GetTempPath(), "realmly-tray-test-" + Guid.NewGuid().ToString("N"));
        Assert(WowProcessDetector.Evaluate(root, ClientKind.Retail, []) == WowRunState.Stopped, "Empty process list");
        var retail = Path.Combine(root, "_retail_", "Wow.exe");
        var forever = Path.Combine(root, "_classic_beta_", "Wow.exe");
        Assert(WowProcessDetector.Evaluate(root, ClientKind.Retail, [retail]) == WowRunState.Running, "Retail detection");
        Assert(WowProcessDetector.Evaluate(root, ClientKind.Forever, [retail]) == WowRunState.Stopped, "Other client blocked");
        Assert(WowProcessDetector.Evaluate(root, ClientKind.Forever, [forever]) == WowRunState.Running, "Forever detection");
        Assert(WowProcessDetector.Evaluate(root, ClientKind.Retail, [Path.Combine(root, "_retail_-copy", "Wow.exe")]) == WowRunState.Stopped, "Adjacent install path prefix");
        Assert(WowProcessDetector.Evaluate(root, ClientKind.Retail, [null]) == WowRunState.Unknown, "Unreadable running client assumed closed");
        Assert(WowProcessDetector.Evaluate(root, ClientKind.Retail, [null, retail]) == WowRunState.Running, "Known selected game should take precedence");
        foreach (var state in new[] { WowRunState.Running, WowRunState.Unknown })
        {
            try { WowProcessDetector.RequireStopped(state); throw new Exception("Unsafe install permitted"); }
            catch (InvalidOperationException) { }
        }
        WowProcessDetector.RequireStopped(WowRunState.Stopped);
        Directory.CreateDirectory(Path.GetDirectoryName(WowFiles.TocPath(root, ClientKind.Retail))!);
        File.WriteAllText(WowFiles.TocPath(root, ClientKind.Retail), "## Version: 0.8.0");
        using var package = new MemoryStream();
        using (var zip = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
        using (var writer = new StreamWriter(zip.CreateEntry("Realmly/Realmly.toc").Open())) writer.Write("## Version: 0.8.3");
        try
        {
            WowFiles.InstallPackageAsync(package.ToArray(), root, ClientKind.Retail, null, default,
                () => WowProcessDetector.RequireStopped(WowRunState.Running)).GetAwaiter().GetResult();
            throw new Exception("Game started during download but files were replaced");
        }
        catch (InvalidOperationException) { }
        Assert(WowFiles.InstalledVersion(root, ClientKind.Retail)!.Display == "0.8.0", "Late process guard damaged old addon");

        var preferences = new CompanionSettings { GameRoot = root, CloseToTray = true, NotifyUpdates = true };
        using var form = new CompanionForm(preferences, (_, _) => WowRunState.Running);
        ShowOffscreen(form);
        using (var running = new Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(running, new Rectangle(Point.Empty, running.Size));
            running.Save(Path.Combine(output, "companion-wow-running.png"));
        }
        var status = (Label)typeof(CompanionForm).GetField("_gameStatus", Private)!.GetValue(form)!;
        Assert(status.Text.Contains("WoW running") && status.Text.Contains("/reload"), "Game status / saved-record guidance missing");
        var blockedInstall = (Task)Call(form, "InstallAsync")!;
        Assert(blockedInstall.IsCompleted, "Install reached a dialog or network while WoW running");
        using (var preview = (Form)Call(form, "CreateUploadDialog", UploadPreview.Parse(UploadTests.Source, []))!)
            Assert(preview.Controls.Cast<Control>().SelectMany(Descendants).OfType<TextBox>().Any(text => text.Text.Contains("Only data already saved to disk")), "Running-game preview warning missing");

        var tray = (CompanionTray)typeof(CompanionForm).GetField("_tray", Private)!.GetValue(form)!;
        Assert(tray.Enabled && tray.NotifyUpdate("addon:1", "fixture", true), "Opt-in tray update notice");
        Assert(!tray.NotifyUpdate("addon:1", "fixture", true), "Duplicate update notification");
        Assert(!tray.NotifyUpdate("addon:2", "fixture", false) && tray.NotifyUpdate("addon:2", "fixture", true), "Disabled notices consumed future update");
        form.Close();
        Assert(!form.IsDisposed && !form.Visible && !form.ShowInTaskbar, "Close did not keep Companion in tray");
        Call(form, "RestoreFromTray");
        Assert(form.Visible && form.WindowState != FormWindowState.Minimized, "Tray restore failed");
        form.ShowInTaskbar = false;
        form.WindowState = FormWindowState.Minimized;
        Assert(!form.Visible && !form.IsDisposed, "Minimize did not hide to tray");
        Call(form, "SetTrayPreferences", false, false);
        Assert(form.Visible && !tray.Enabled, "Disabling tray stranded a hidden app");
        form.ShowInTaskbar = false;

        using (var options = (Form)Call(form, "CreateOptionsDialog")!)
        {
            ShowOffscreen(options);
            var checks = options.Controls.Cast<Control>().SelectMany(Descendants).OfType<CheckBox>().ToArray();
            Assert(checks.Length == 3 && !checks.Single(check => check.Name == "notifyUpdates").Enabled && !checks.Single(check => check.Name == "autoUpload").Checked, "Options defaults/dependency");
            var disabledNotice = checks.Single(check => check.Name == "notifyUpdates");
            Assert(disabledNotice is ReadableCheckBox && disabledNotice.Text.Contains("Requires tray mode"), "Disabled notice missing readable styling or dependency hint");
            using (var disabledImage = new Bitmap(options.Width, options.Height))
            {
                options.DrawToBitmap(disabledImage, new Rectangle(Point.Empty, disabledImage.Size));
                disabledImage.Save(Path.Combine(output, "companion-options-disabled.png"));
                using var labelImage = new Bitmap(disabledNotice.Width, disabledNotice.Height);
                disabledNotice.DrawToBitmap(labelImage, new Rectangle(Point.Empty, labelImage.Size));
                var goldPixels = 0;
                for (var y = 0; y < labelImage.Height; y++)
                for (var x = 35; x < labelImage.Width; x++)
                    if (labelImage.GetPixel(x, y).ToArgb() == ReadableCheckBox.DisabledTextColor.ToArgb()) goldPixels++;
                Assert(goldPixels > 20, "Disabled label did not render muted gold text");
            }
            checks.Single(check => check.Name == "closeToTray").Checked = true;
            Assert(preferences.CloseToTray && checks.Single(check => check.Name == "notifyUpdates").Enabled, "Tray option not applied");
            Assert(!disabledNotice.Text.Contains("Requires tray mode"), "Dependency hint stayed after enabling tray mode");
            checks.Single(check => check.Name == "notifyUpdates").Checked = true;
            Assert(preferences.NotifyUpdates, "Notifications option not applied");
            using var image = new Bitmap(options.Width, options.Height);
            options.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
            image.Save(Path.Combine(output, "companion-options.png"));
            foreach (var check in checks) Assert(check.Right <= check.Parent!.Width && check.Height >= TextRenderer.MeasureText(check.Text, check.Font).Height, "Clipped option");
            options.Close();
        }
        typeof(CompanionForm).GetField("_busy", Private)!.SetValue(form, true);
        Call(form, "RequestExit");
        Assert(!form.IsDisposed, "Tray Exit interrupted an active operation");
        typeof(CompanionForm).GetField("_busy", Private)!.SetValue(form, false);
        Call(form, "RequestExit");
        Assert(form.IsDisposed && !tray.Enabled, "Tray Exit failed to close/dispose icon");

        using (var shutdown = new CompanionForm(new CompanionSettings { CloseToTray = true }))
        {
            var closing = new FormClosingEventArgs(CloseReason.WindowsShutDown, false);
            typeof(Form).GetMethod("OnFormClosing", Private)!.Invoke(shutdown, [closing]);
            Assert(!closing.Cancel, "Tray preference blocked Windows shutdown");
        }

        using var updating = new CompanionForm(new CompanionSettings { CloseToTray = true });
        ShowOffscreen(updating);
        typeof(CompanionForm).GetField("_restarting", Private)!.SetValue(updating, true);
        updating.Close();
        Assert(updating.IsDisposed, "Self-update restart was swallowed by close-to-tray");
        using var ordinary = new CompanionForm(new CompanionSettings());
        ShowOffscreen(ordinary); ordinary.Close();
        Assert(ordinary.IsDisposed, "Tray must be opt-in, not the default");
        Console.WriteLine("PASS selected-client process matching, inaccessible-process safety, late installer guard, tray opt-in/restore/Exit, notification dedupe and self-update shutdown");
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        yield return control;
        foreach (Control child in control.Controls)
        foreach (var nested in Descendants(child)) yield return nested;
    }
}
