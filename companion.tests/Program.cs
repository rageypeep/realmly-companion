using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Runtime.InteropServices;
using RealmlyCompanion;

internal static class Program
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr window, out Rectangle rectangle);
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static byte[] Package(string version, string? extra = null)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("Realmly\\Realmly.toc").Open())) writer.Write("## Version: " + version);
            using (var writer = new StreamWriter(zip.CreateEntry(extra ?? "Realmly/Realmly.lua").Open())) writer.Write("-- test addon");
        }
        return stream.ToArray();
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (Control child in control.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    [STAThread]
    private static void Main(string[] args)
    {
        var payloadArgument = Array.IndexOf(args, "--store-payload");
        if (payloadArgument >= 0)
        {
            if (payloadArgument + 1 >= args.Length) throw new ArgumentException("Pass --store-payload <payload-directory>.");
            DistributionTests.CheckStorePayload(args[payloadArgument + 1]);
            return;
        }
        if (args.Contains("--automatic-only"))
        {
            ApplicationConfiguration.Initialize();
            AutomaticUploadTests.Run(args[0]);
            return;
        }
        if (args.Contains("--screenshots-only"))
        {
            if (args.Contains("--dpi-unaware")) Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
            ApplicationConfiguration.Initialize();
            using var dpiProbe = new Control(); dpiProbe.CreateControl();
            Assert(!args.Contains("--dpi-unaware") || dpiProbe.DeviceDpi == 96, "DPI-unaware fixture did not run at 96 DPI");
            Console.WriteLine("Dropdown fixture DPI: " + dpiProbe.DeviceDpi);
            Directory.CreateDirectory(Path.GetFullPath(args[0]));
            ScreenshotTests.Run(args[0]);
            ScreenshotThemeTests.Run(args[0]);
            return;
        }
        UploadTests.Run();
        UpdaterTests.RunAsync().GetAwaiter().GetResult();
        if (args.Contains("--release"))
        {
            var releaseArgument = Array.IndexOf(args, "--release");
            if (releaseArgument + 1 >= args.Length || args[releaseArgument + 1].StartsWith("--"))
                throw new ArgumentException("Pass --release <path-to-release-manifest.json>. The referenced ZIP must be alongside the manifest.");
            var manifest = Path.GetFullPath(args[releaseArgument + 1]);
            var release = JsonSerializer.Deserialize<CompanionRelease>(File.ReadAllText(manifest), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            var verification = Path.Combine(Path.GetTempPath(), "realmly-release-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(verification);
            var archive = Path.Combine(Path.GetDirectoryName(manifest)!, Path.GetFileName(new Uri(release.DownloadUrl).AbsolutePath));
            var verified = SelfUpdater.VerifyAndExtractAsync(archive, verification, release, default).GetAwaiter().GetResult();
            Assert(File.Exists(verified.Executable), "Published package validation failed");
            File.Delete(verified.Executable);
            Directory.Delete(verification);
            Console.WriteLine("PASS published release ZIP, manifest SHA256 and executable version: " + release.Version);
        }
        Assert(AddonVersion.FromToc("## Version: 0.8.3-retail-test")?.Number == new Version(0, 8, 3, 0), "Retail suffix parsing");
        Assert(AddonVersion.FromToc("## Version: 0.8")?.Number == new Version(0, 8, 0, 0), "Short version normalization");
        Assert(AddonVersion.FromToc("## Version: not-a-version") is null, "Malformed version");
        Assert(AddonVersion.FromToc("## Version: 9999999999999999999.1") is null, "Overflow must not crash");
        Assert(WowFiles.PackageVersion(Package("0.8.3-retail-test")).Display == "0.8.3-retail-test", "ZIP version / Windows separators");
        using (var settings = JsonDocument.Parse(JsonSerializer.Serialize(new CompanionSettings { ProtectedAccessToken = "encrypted-value" })))
        {
            Assert(!settings.RootElement.TryGetProperty("AccessToken", out _), "Decrypted token serialized to settings");
            Assert(settings.RootElement.GetProperty("ProtectedAccessToken").GetString() == "encrypted-value", "Encrypted token not persisted");
        }
        var root = Path.Combine(Path.GetTempPath(), "realmly-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Assert(WowFiles.ClientFolder(ClientKind.Forever) == "_forever_", "Forever folder must not target Classic beta");
        Assert(WowFiles.ClientFolder(ClientKind.Retail) == "_retail_", "Retail folder unchanged");
        Assert(WowFiles.AddOnsPath(root, ClientKind.Forever) == Path.Combine(root, "_forever_", "Interface", "AddOns"), "Forever addon destination");
        Assert(WowFiles.AccountsPath(root, ClientKind.Forever) == Path.Combine(root, "_forever_", "WTF", "Account"), "Forever records destination");
        foreach (var client in new[] { ClientKind.Retail, ClientKind.Forever })
        {
            Assert(WowFiles.InstalledVersion(root, client) is null, "Absent addon");
            var directory = Path.GetDirectoryName(WowFiles.TocPath(root, client))!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(WowFiles.TocPath(root, client), "## Version: 0.8.2");
            File.WriteAllText(Path.Combine(directory, "old.lua"), "old installation");
            var saved = Path.Combine(WowFiles.AccountsPath(root, client), "TEST", "SavedVariables", "Realmly.lua");
            Directory.CreateDirectory(Path.GetDirectoryName(saved)!);
            File.WriteAllText(saved, "saved variables untouched");
            Assert(WowFiles.SavedVariablesCandidates(root, client).Contains(saved), "Selected client's records are discoverable");
            Assert(WowFiles.InstalledVersion(root, client)?.Number < new Version(0, 8, 3, 0), "Outdated addon");
            try
            {
                WowFiles.InstallPackageAsync(Package("0.8.3", "Realmly/../../../escaped.lua"), root, client, null, default).GetAwaiter().GetResult();
                throw new Exception("Unsafe package accepted");
            }
            catch (InvalidOperationException) { }
            Assert(File.Exists(Path.Combine(directory, "old.lua")), "Failed update damaged existing installation");
            WowFiles.InstallPackageAsync(Package("0.8.3"), root, client, null, default).GetAwaiter().GetResult();
            Assert(WowFiles.InstalledVersion(root, client)?.Number == new Version(0, 8, 3, 0), "Post-install version");
            Assert(!File.Exists(Path.Combine(directory, "old.lua")), "Old addon not replaced");
            Assert(File.ReadAllText(saved) == "saved variables untouched", "SavedVariables changed");
        }
        Assert(!Directory.Exists(Path.Combine(root, "_classic_beta_")), "Install must never create a Classic beta folder");
        Console.WriteLine("PASS version parsing, independent clients, staged installation, unsafe ZIP rejection and SavedVariables preservation");
        if (args.Contains("--live"))
        {
            foreach (var client in new[] { ClientKind.Retail, ClientKind.Forever })
                Console.WriteLine($"PASS live package version: {client} {WowFiles.LatestVersionAsync(client, default).GetAwaiter().GetResult().Display}");
        }

        ApplicationConfiguration.Initialize();
        using (var iconResource = typeof(CompanionForm).Assembly.GetManifestResourceStream("RealmlyCompanion.Icon.ico")!)
        using (var reader = new BinaryReader(iconResource))
        {
            Assert(reader.ReadUInt16() == 0 && reader.ReadUInt16() == 1 && reader.ReadUInt16() == 7, "Invalid multi-resolution icon");
            var entries = new List<(int Size, uint Length, uint Offset)>();
            foreach (var expected in new[] { 16, 24, 32, 48, 64, 128, 256 })
            {
                var width = reader.ReadByte(); var height = reader.ReadByte();
                Assert((width == 0 ? 256 : width) == expected && width == height, "Incorrect icon frame size");
                reader.ReadBytes(6);
                entries.Add((expected, reader.ReadUInt32(), reader.ReadUInt32()));
            }
            foreach (var entry in entries)
            {
                iconResource.Position = entry.Offset;
                using var imageStream = new MemoryStream(reader.ReadBytes((int)entry.Length));
                using var image = new Bitmap(imageStream);
                Assert(image.Width == entry.Size && image.GetPixel(0, 0).A == 0, "Icon size/transparent edge invalid");
                Assert(image.GetPixel(entry.Size / 2, entry.Size / 2).A > 200, "Icon center is empty");
            }
        }
        Console.WriteLine("PASS icon: seven Windows sizes, transparent edges and valid PNG frames");
        var hitSize = new Size(900, 880);
        foreach (var hit in new[] { (new Point(0, 0), 13), (new Point(899, 0), 14), (new Point(0, 879), 16),
            (new Point(899, 879), 17), (new Point(450, 0), 12), (new Point(450, 879), 15),
            (new Point(0, 400), 10), (new Point(899, 400), 11), (new Point(450, 400), 1) })
        {
            Assert(RealmlyWindow.ResizeHitTest(hit.Item1, hitSize, 9, false) == hit.Item2, "Wrong native resize edge");
            Assert(RealmlyWindow.ResizeHitTest(hit.Item1, hitSize, 9, true) == 1, "Maximized window has resize grips");
        }
        using var form = new CompanionForm(new CompanionSettings { GameRoot = root });
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-30000, -30000);
        form.Show();
        Assert(Descendants(form).OfType<RealmlyScrollPanel>().Single().Offset == 0, "Initial focus unexpectedly scrolled away from page heading");
        Assert(form.FormBorderStyle == FormBorderStyle.None, "Native title bar still enabled");
        using (var menu = form.BuildWindowMenu())
        {
            Assert(menu.Items.OfType<ToolStripMenuItem>().Any(i => i.Text == "Close" && i.ShortcutKeyDisplayString == "Alt+F4"), "Window keyboard menu missing Close");
            Assert(menu.Items.OfType<ToolStripMenuItem>().Any(i => i.Text == "Size" && i.Enabled), "Window menu missing resize");
        }
        foreach (var name in new[] { "Minimize", "Maximize", "Close" })
            Assert(Descendants(form).OfType<RealmlyCompanion.CaptionButton>().Any(b => b.AccessibleName == name), "Missing accessible window control: " + name);
        Console.WriteLine($"UI render DPI: {form.DeviceDpi}; font: {form.Font.Size}; scale: {form.AutoScaleDimensions}");
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var procedure = typeof(RealmlyWindow).GetMethod("WndProc", flags)!;
        var renderingResult = DwmGetWindowAttribute(form.Handle, 1, out var nativeRendering, sizeof(int));
        if (renderingResult == 0) Assert(nativeRendering == 0, "DWM is still rendering the native resize frame");
        else Console.WriteLine("NOTE: Desktop composition is unavailable in this test session; native paint messages are checked separately.");
        Assert((bool)typeof(Control).GetMethod("GetStyle", flags)!.Invoke(form, [ControlStyles.ResizeRedraw])!, "Full resize redraw is disabled");
        object[] nonclientPaint = [Message.Create(form.Handle, 0x0085, (IntPtr)1, IntPtr.Zero)];
        procedure.Invoke(form, nonclientPaint);
        Assert(((Message)nonclientPaint[0]).Result == IntPtr.Zero, "Native border paint was not suppressed");
        var fullInvalidations = 0;
        form.Invalidated += (_, e) => { if (e.InvalidRect.Contains(form.ClientRectangle)) fullInvalidations++; };
        for (var step = 0; step < 16; step++)
        {
            form.ClientSize = new Size(800 + step * 7, 760 + step * 5);
            form.Update();
        }
        for (var step = 15; step >= 0; step--)
        {
            form.ClientSize = new Size(800 + step * 7, 760 + step * 5);
            form.Update();
        }
        Assert(fullInvalidations >= 30, "Incremental growing/shrinking did not invalidate the complete client area");
        Console.WriteLine("PASS frame repaint: native DWM/NC paint disabled and full client invalidated during incremental resizing");
        var screenPoint = form.PointToScreen(new Point(0, 0));
        var packedPoint = unchecked((int)((screenPoint.Y << 16) | (screenPoint.X & 0xffff)));
        object[] hitMessage = [Message.Create(form.Handle, 0x0084, IntPtr.Zero, (IntPtr)packedPoint)];
        procedure.Invoke(form, hitMessage);
        Assert(((Message)hitMessage[0]).Result == (IntPtr)13, "Native negative-monitor resize hit testing");
        var minmax = Marshal.AllocHGlobal(40);
        try
        {
            Marshal.Copy(new int[10], 0, minmax, 10);
            object[] sizingMessage = [Message.Create(form.Handle, 0x0024, IntPtr.Zero, minmax)];
            procedure.Invoke(form, sizingMessage);
            var screen = Screen.FromHandle(form.Handle);
            Assert(Marshal.ReadInt32(minmax, 8) == screen.WorkingArea.Width && Marshal.ReadInt32(minmax, 12) == screen.WorkingArea.Height,
                "Maximize does not respect monitor work area");
        }
        finally { Marshal.FreeHGlobal(minmax); }
        var latest = (Dictionary<ClientKind, AddonVersion>)typeof(CompanionForm).GetField("_latestAddons", flags)!.GetValue(form)!;
        latest[ClientKind.Retail] = new AddonVersion("0.8.3-retail-test", new Version(0, 8, 3, 0));
        typeof(CompanionForm).GetMethod("RefreshAddonStatus", flags)!.Invoke(form, null);
        var savedBox = (TextBox)typeof(CompanionForm).GetField("_savedVariables", flags)!.GetValue(form)!;
        var fixture = Path.Combine(root, "preview.lua");
        File.WriteAllText(fixture, UploadTests.Source);
        savedBox.Text = fixture;
        var historyField = (Label)typeof(CompanionForm).GetField("_historyStatus", flags)!.GetValue(form)!;
        historyField.Text = "Last upload: 19/10/2026 15:30";
        Assert(Descendants(form).Any(c => c.Text.Contains("You're up to date")), "Up-to-date UI");
        File.WriteAllText(WowFiles.TocPath(root, ClientKind.Retail), "## Version: 0.8.2-retail-test");
        typeof(CompanionForm).GetMethod("RefreshAddonStatus", flags)!.Invoke(form, null);
        Assert(Descendants(form).OfType<Button>().Any(c => c.Text == "Update to 0.8.3-retail-test"), "Update action");
        var addonStatus = (Label)typeof(CompanionForm).GetField("_addonStatus", flags)!.GetValue(form)!;
        File.WriteAllText(WowFiles.TocPath(root, ClientKind.Retail), "## Version: 9.0.0");
        typeof(CompanionForm).GetMethod("RefreshAddonStatus", flags)!.Invoke(form, null);
        Assert(addonStatus.Text.Contains("newer than"), "Newer local build misidentified");
        File.WriteAllText(WowFiles.TocPath(root, ClientKind.Retail), "## Version: invalid");
        typeof(CompanionForm).GetMethod("RefreshAddonStatus", flags)!.Invoke(form, null);
        Assert(addonStatus.Text.Contains("unreadable"), "Unreadable TOC UI");
        File.Delete(WowFiles.TocPath(root, ClientKind.Retail));
        typeof(CompanionForm).GetMethod("RefreshAddonStatus", flags)!.Invoke(form, null);
        Assert(addonStatus.Text.Contains("not installed"), "Missing addon UI");
        var clientPicker = (ComboBox)typeof(CompanionForm).GetField("_client", flags)!.GetValue(form)!;
        clientPicker.SelectedIndex = 1;
        Assert(addonStatus.Text.Contains("Latest: not checked") && !addonStatus.Text.Contains("retail"), "Client version state leaked");
        clientPicker.SelectedIndex = 0;
        File.WriteAllText(WowFiles.TocPath(root, ClientKind.Retail), "## Version: 0.8.2-retail-test");
        typeof(CompanionForm).GetMethod("RefreshAddonStatus", flags)!.Invoke(form, null);
        savedBox.Text = fixture;
        var scanDeadline = DateTime.UtcNow.AddSeconds(5);
        while ((bool)typeof(CompanionForm).GetField("_scanning", flags)!.GetValue(form)! && DateTime.UtcNow < scanDeadline) { Application.DoEvents(); Thread.Sleep(10); }
        var scan = (Task)typeof(CompanionForm).GetMethod("RefreshPreviewAsync", flags)!.Invoke(form, [true])!;
        scanDeadline = DateTime.UtcNow.AddSeconds(15);
        while (!scan.IsCompleted && DateTime.UtcNow < scanDeadline) { Application.DoEvents(); Thread.Sleep(10); }
        Assert(scan.IsCompleted, "Preview scan did not finish");
        scan.GetAwaiter().GetResult();
        Assert(((Label)typeof(CompanionForm).GetField("_recordStatus", flags)!.GetValue(form)!).Text.Contains("3 new on this PC"), "Live local preview did not refresh");
        form.CreateControl();
        var output = args.Length > 0 ? args[0] : root;
        Directory.CreateDirectory(output);
        foreach (var width in new[] { 900, 780 })
        {
            form.ClientSize = new Size(width, 880);
            form.PerformLayout();
            GetClientRect(form.Handle, out var nativeClient);
            Assert(nativeClient.Size == form.ClientSize && form.ClientSize == form.Size, "Native/managed client sizes disagree, leaving an unpainted frame strip");
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            Assert(bitmap.GetPixel(0, 0).ToArgb() == Color.FromArgb(125, 89, 39).ToArgb()
                && bitmap.GetPixel(bitmap.Width - 1, bitmap.Height - 1).ToArgb() == Color.FromArgb(125, 89, 39).ToArgb(),
                "The border does not cover the actual outer window corners");
            bitmap.Save(Path.Combine(output, $"companion-{width}.png"));
            foreach (var button in Descendants(form).OfType<Button>().Where(c => c.Visible))
            {
                var required = TextRenderer.MeasureText(button.Text, button.Font);
                Assert(button.Width >= required.Width + button.Padding.Horizontal && button.Height >= required.Height + 6, "Clipped button: " + button.Text);
                Assert(button.Right <= button.Parent!.ClientSize.Width, "Button outside parent: " + button.Text);
            }
            var scroll = Descendants(form).OfType<RealmlyScrollPanel>().Single();
            Assert(scroll.ContentWidth <= scroll.ViewportWidth, "Horizontal overflow");
            Assert(scroll.ContentWidth == scroll.ViewportWidth, "Content does not fill viewport width");
            scroll.ScrollTo(0);
            scroll.ScrollByWheel(-120);
            Assert(scroll.Offset > 0, "Mouse wheel does not scroll");
            scroll.ScrollTo(int.MaxValue);
            Assert(scroll.Offset == scroll.MaximumOffset, "Scroll position not clamped");
            var bar = Descendants(scroll).OfType<RealmlyScrollBar>().Single();
            Assert(bar.Thumb.Bottom <= bar.Height && bar.AccessibleRole == AccessibleRole.ScrollBar, "Scrollbar geometry/accessibility");
            typeof(RealmlyScrollBar).GetMethod("OnKeyDown", flags)!.Invoke(bar, [new KeyEventArgs(Keys.Home)]);
            Assert(scroll.Offset == 0, "Scrollbar Home key");
            typeof(RealmlyScrollBar).GetMethod("OnKeyDown", flags)!.Invoke(bar, [new KeyEventArgs(Keys.End)]);
            Assert(scroll.Offset == scroll.MaximumOffset, "Scrollbar End key");
            scroll.ScrollTo(scroll.MaximumOffset);
            form.PerformLayout();
            using var scrolled = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(scrolled, new Rectangle(Point.Empty, scrolled.Size));
            scrolled.Save(Path.Combine(output, $"companion-upload-{width}.png"));
            scroll.ScrollTo(0);
        }
        Console.WriteLine("PASS UI states and button bounds; rendered desktop and narrow layouts to " + output);
        TrayTests.Run(output);
        DistributionTests.Run();
        ThemeTests.Run(form, output);
        SetupDiagnosticsTests.Run(output);
        AutomaticUploadTests.Run(output);
        ScreenshotTests.Run(output);
        ScreenshotThemeTests.Run(output);
        using (var dialog = (Form)typeof(CompanionForm).GetMethod("CreateUploadDialog", flags)!.Invoke(form, [UploadPreview.Parse(UploadTests.Source, [])])!)
        {
            dialog.StartPosition = FormStartPosition.Manual; dialog.Location = new Point(-30000, -30000); dialog.Show();
            using var bitmap = new Bitmap(dialog.Width, dialog.Height);
            dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(Path.Combine(output, "companion-upload-preview.png"));
            Assert(Descendants(dialog).OfType<TextBox>().Single().ReadOnly, "Preview can be edited after review");
            dialog.Close();
        }
        object[] closeKey = [Message.Create(form.Handle, 0x0104, IntPtr.Zero, IntPtr.Zero), Keys.Alt | Keys.F4];
        typeof(RealmlyWindow).GetMethod("ProcessCmdKey", flags)!.Invoke(form, closeKey);
        Assert(form.IsDisposed, "Alt+F4 did not close the custom window");
        Console.WriteLine("PASS custom chrome: resize hit targets, accessible caption controls, viewport width, wheel, keyboard scrolling and clamped scrollbar");
    }
}
