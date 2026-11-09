using System.Text.Json;

namespace RealmlyCompanion;

internal sealed class AddonsPanel : UserControl
{
    private readonly Label _status = Copy("Choose Install to download and install an add-on. No account connection needed.");
    private readonly List<Image> _artwork = [];

    private readonly Func<string> _gameRoot;
    private readonly Func<string, ClientKind, WowRunState> _detectWow;
    private readonly Func<bool> _canInstall;
    private readonly OptionalAddons _manager;
    private readonly bool _ownsManager;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Label _target = Copy("");
    private readonly Button _check = new AddonButton() { Text = "Check for add-on updates" };
    private readonly List<(OptionalAddon Addon, Label Status, Button Install, Button Uninstall)> _installs = [];
    private Dictionary<string, OptionalAddonRelease> _latest = [];
    private bool _working;
    private bool _uninstalling;
    private bool _layoutQueued;
    private readonly Func<OptionalAddon, string, bool> _confirmUninstall;
    private string? _activeInstall;
    private readonly Dictionary<string, string> _installMessages = [];
    internal AddonsPanel(Func<string>? gameRoot = null,
        Func<string, ClientKind, WowRunState>? detectWow = null, OptionalAddons? manager = null, Func<bool>? canInstall = null, Func<OptionalAddon, string, bool>? confirmUninstall = null)
    {
        _gameRoot = gameRoot ?? (() => ""); _detectWow = detectWow ?? WowProcessDetector.Check;
        _manager = manager ?? new OptionalAddons(); _ownsManager = manager is null;
        _canInstall = canInstall ?? (() => true);
        _confirmUninstall = confirmUninstall ?? ((addon, target) => MessageBox.Show(this,
            "Uninstall " + addon.Name + " from this folder?\n\n" + target
            + "\n\nSaved settings and herb history will be kept."
            + (addon == OptionalAddons.Sparkles ? "\n\nBefore removing Sparkles, use /rls > Turn off & restore in-game, then exit Forever." : ""),
            "Uninstall " + addon.Name, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK);
        BackColor = Color.FromArgb(15, 11, 9); ForeColor = Color.FromArgb(245, 229, 184);
        Font = new Font("Segoe UI", 10F); AccessibleName = "Optional add-ons";
        var scroll = new RealmlyScrollPanel { Dock = DockStyle.Fill };
        var content = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1,
            Padding = new Padding(24, 18, 24, 18), BackColor = BackColor };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Add(Control control)
        {
            var row = content.RowCount++; content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right; content.Controls.Add(control, 0, row);
        }
        Add(Copy("ADD-ONS / A FEW TOOLS FOR THE ROAD", heading: true));
        var title = Copy("Make yourself at home."); title.Font = new Font("Georgia", 22F); Add(title);
        Add(Copy("Two optional add-ons for WoW Forever. Use either on its own, with or without the adventure recorder."));
        Add(_target); CompanionForm.StyleButton(_check);
        _check.Click += async (_, _) => await CheckUpdatesAsync(); Add(_check); _check.Anchor = AnchorStyles.Left;
        Add(Card("Chat Copy", "Select a few words worth keeping.",
            "Open chat in a selectable window, highlight any words and press Ctrl+C. Copy last message selects the latest readable message. A button follows each supported chat tab.",
            "/rlcc", "RealmlyCompanion.ChatCopy.png"));
        Add(Card("Sparkles", "A little light along the gathering trail.",
            "Highlight the nearby herb or ore node selected by the game. Adjust brightness and sparkle height, optionally rotate gathering trackers, or remember picked-herb locations. Features start off; enable the ones you want.",
            "/rls (also /rlg)", "RealmlyCompanion.Sparkles.png"));
        Add(Copy("INSTALL IN FOREVER", heading: true));
        Add(Copy("Select your World of Warcraft folder in the Realmly tab, then close Forever and click Install or Update here. The app downloads, verifies and installs the add-on into that folder's _forever_ / Interface / AddOns directory. Start Forever and enable it at character selection."));
        Add(Copy("These add-ons are for Forever, not Retail. Sparkles keeps its compatible folder and saved herb locations. Updates replace only the chosen add-on folder; SavedVariables and the main recorder are left alone."));
        Add(_status);
        content.SizeChanged += (_, _) =>
        {
            var width = Math.Max(1, content.ClientSize.Width - content.Padding.Horizontal - 24);
            foreach (var label in Labels(content)) label.MaximumSize = new Size(width, 0);
            SettleLayout();
        };
        scroll.SetContent(content); Controls.Add(scroll);
        VisibleChanged += async (_, _) =>
        {
            if (!Visible) return;
            RefreshLocal();
            SettleLayout();
            if (Directory.Exists(Path.Combine(_gameRoot(), "_forever_"))) await CheckUpdatesAsync();
        };
        RefreshLocal();
    }

    private void SettleLayout()
    {
        // Label wrapping can change preferred height inside the viewport's layout
        // guard. Re-measure after that pass, rather than leaving a stale scroll range.
        if (_layoutQueued || !IsHandleCreated || IsDisposed || !Visible) return;
        _layoutQueued = true;
        BeginInvoke(new Action(() =>
        {
            _layoutQueued = false;
            if (!IsDisposed)
                foreach (var scroll in Controls.OfType<RealmlyScrollPanel>()) scroll.PerformLayout();
        }));
    }

    private Control Card(string name, string tagline, string description, string command, string? artwork)
    {
        var card = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Padding = new Padding(16),
            BackColor = Color.FromArgb(29, 21, 14), Margin = new Padding(0, 10, 0, 12), AccessibleName = name };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Add(Control control)
        {
            var row = card.RowCount++; card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right; card.Controls.Add(control, 0, row);
        }
        var heading = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true };
        var assembly = typeof(AddonsPanel).Assembly;
        Image image;
        if (artwork is not null)
        {
            using var stream = assembly.GetManifestResourceStream(artwork) ?? throw new InvalidOperationException("Add-on artwork is missing.");
            using var source = Image.FromStream(stream); image = new Bitmap(source);
        }
        else
        {
            using var stream = assembly.GetManifestResourceStream("RealmlyCompanion.Icon.ico")!;
            using var icon = new Icon(stream); image = icon.ToBitmap();
        }
        _artwork.Add(image);
        heading.Controls.Add(new PictureBox { Image = image, SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(54, 54),
            Margin = new Padding(0, 0, 14, 6), AccessibleName = name + " icon" });
        var title = Copy(name, heading: true); title.Font = new Font("Georgia", 20F); title.Margin = new Padding(0, 9, 0, 0);
        heading.Controls.Add(title); Add(heading);
        Add(Copy("WOW FOREVER / OPTIONAL", heading: true));
        Add(Copy(tagline)); Add(Copy(description)); Add(Copy("Open in game: " + command));
        var addon = name == "Chat Copy" ? OptionalAddons.ChatCopy : OptionalAddons.Sparkles;
        if (addon == OptionalAddons.Sparkles)
            Add(Copy("Before uninstalling: use /rls > Turn off & restore in-game, then exit Forever."));
        var installed = Copy(""); Add(installed);
        var install = new AddonButton { Text = "Install " + name, AccessibleName = "Install or update " + name + " in Forever" };
        CompanionForm.StyleButton(install); install.Anchor = AnchorStyles.Left;
        var uninstall = new AddonButton { Text = "Uninstall", AccessibleName = "Uninstall " + name + " from Forever" };
        CompanionForm.StyleButton(uninstall); uninstall.Click += (_, _) => Uninstall(addon);
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true };
        actions.Controls.Add(install); actions.Controls.Add(uninstall); Add(actions);
        _installs.Add((addon, installed, install, uninstall));
        install.Click += async (_, _) => await InstallAsync(addon);
        return card;
    }

    internal event Action<OptionalAddon, string>? UpdateAvailable;
    internal void RefreshLocal()
    {
        if (IsDisposed) return;
        _check.Enabled = !_working; _check.Text = _working && _activeInstall is null ? "Checking..." : "Check for add-on updates";
        var root = _gameRoot();
        try { _target.Text = "Forever add-ons folder: " + Path.GetDirectoryName(OptionalAddons.Target(root, OptionalAddons.ChatCopy)); }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException)
        { _target.Text = error.Message; }
        foreach (var (addon, status, button, uninstall) in _installs)
        {
            try
            {
                var current = OptionalAddons.Installed(root, addon); _latest.TryGetValue(addon.Id, out var release);
                status.Text = "Installed: " + (current?.Display ?? "Not installed") + "  |  Latest: " + (release?.Version ?? "Not checked");
                if (_installMessages.TryGetValue(addon.Id, out var message)) status.Text += "\n" + message;
                var upToDate = current is not null && release is not null && current.Number >= Version.Parse(release.Version + ".0");
                button.Text = _working && !_uninstalling && _activeInstall == addon.Id ? "Installing..." : upToDate ? "Up to date" : current is null ? "Install " + addon.Name : "Update " + addon.Name;
                button.Enabled = !_working && !upToDate;
                uninstall.Enabled = !_working && Directory.Exists(OptionalAddons.Target(root, addon));
                uninstall.Text = _working && _uninstalling && _activeInstall == addon.Id ? "Uninstalling..." : "Uninstall";
            }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException)
            { status.Text = error.Message; button.Enabled = uninstall.Enabled = false; }
        }
    }
    internal async Task CheckUpdatesAsync()
    {
        if (_working || IsDisposed) return;
        _working = true; _installMessages.Clear(); RefreshLocal(); _status.Text = "Checking optional add-on versions...";
        try
        {
            _latest = await _manager.CheckAsync(_lifetime.Token);
            if (!IsDisposed)
            {
                _status.Text = "Version check complete. Install and update when Forever is closed.";
                foreach (var addon in OptionalAddons.All)
                {
                    try
                    {
                        var current = OptionalAddons.Installed(_gameRoot(), addon); var release = _latest[addon.Id];
                        if (current is not null && current.Number < Version.Parse(release.Version + ".0")) UpdateAvailable?.Invoke(addon, release.Version);
                    }
                    catch (Exception error) when (error is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException) { }
                }
            }
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or InvalidOperationException or JsonException)
        { if (!IsDisposed) _status.Text = "Could not check add-on updates: " + error.Message; }
        finally { _working = false; _activeInstall = null; if (!IsDisposed) RefreshLocal(); }
    }
    internal async Task InstallAsync(OptionalAddon addon)
    {
        if (_working || IsDisposed) return;
        var root = _gameRoot(); _working = true; _activeInstall = addon.Id; _installMessages[addon.Id] = "Preparing installation..."; RefreshLocal();
        try
        {
            OptionalAddons.Target(root, addon);
            WowProcessDetector.RequireStopped(_detectWow(root, ClientKind.Forever));
            if (!_canInstall()) throw new InvalidOperationException("Wait for the current Realmly operation to finish.");
            if (!_latest.TryGetValue(addon.Id, out var release))
            { _latest = await _manager.CheckAsync(_lifetime.Token); release = _latest[addon.Id]; }
            void BeforeReplace()
            {
                if (!_canInstall() || !string.Equals(root, _gameRoot(), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The selected setup changed or another operation started. Try again with your intended WoW folder selected.");
            }
            await _manager.InstallAsync(root, addon, release, () => _detectWow(root, ClientKind.Forever),
                text => { if (!IsDisposed) { _status.Text = text; _installMessages[addon.Id] = text; RefreshLocal(); } }, _lifetime.Token, BeforeReplace);
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or InvalidOperationException or JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        { if (!IsDisposed) { _status.Text = "Installation stopped: " + error.Message; _installMessages[addon.Id] = _status.Text; } }
        finally { _working = false; _activeInstall = null; if (!IsDisposed) RefreshLocal(); }
    }

    internal void Uninstall(OptionalAddon addon)
    {
        if (_working || IsDisposed) return;
        var root = _gameRoot(); _working = true; _uninstalling = true; _activeInstall = addon.Id; RefreshLocal();
        try
        {
            var target = OptionalAddons.Target(root, addon);
            WowProcessDetector.RequireStopped(_detectWow(root, ClientKind.Forever));
            if (!_canInstall()) throw new InvalidOperationException("Wait for the current Realmly operation to finish.");
            if (!_confirmUninstall(addon, target)) return;
            var message = OptionalAddons.Uninstall(root, addon, () => _detectWow(root, ClientKind.Forever), _lifetime.Token, () =>
            {
                if (!_canInstall() || !string.Equals(root, _gameRoot(), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The selected setup changed or another operation started. Uninstall cancelled.");
            });
            _status.Text = message; _installMessages[addon.Id] = message;
        }
        catch (Exception error) when (error is OperationCanceledException or InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        { if (!IsDisposed) { _status.Text = "Uninstall stopped: " + error.Message; _installMessages[addon.Id] = _status.Text; } }
        finally { _working = false; _uninstalling = false; _activeInstall = null; if (!IsDisposed) RefreshLocal(); }
    }

    private sealed class AddonButton : Button
    {
        protected override void OnPaint(PaintEventArgs args)
        {
            if (Enabled) { base.OnPaint(args); return; }
            args.Graphics.Clear(BackColor);
            using var border = new Pen(Color.FromArgb(100, 73, 33));
            args.Graphics.DrawRectangle(border, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
            TextRenderer.DrawText(args.Graphics, Text, Font, ClientRectangle, ReadableCheckBox.DisabledTextColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
    private static Label Copy(string text, bool heading = false) => new() { Text = text, AutoSize = true, UseMnemonic = false,
        MaximumSize = new Size(680, 0), Margin = new Padding(0, 0, 0, 9),
        ForeColor = heading ? Color.FromArgb(216, 168, 79) : Color.FromArgb(195, 176, 143),
        Font = new Font("Segoe UI", heading ? 9F : 10F, heading ? FontStyle.Bold : FontStyle.Regular) };
    private static IEnumerable<Label> Labels(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is Label label) yield return label;
            foreach (var nested in Labels(child)) yield return nested;
        }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { _lifetime.Cancel(); if (_ownsManager) _manager.Dispose(); }
        base.Dispose(disposing);
        if (disposing) foreach (var image in _artwork) image.Dispose();
    }
}
