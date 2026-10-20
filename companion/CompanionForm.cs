using System.Diagnostics;

namespace RealmlyCompanion;

internal sealed class CompanionForm : RealmlyWindow
{
    private readonly CompanionSettings _settings;
    private readonly RealmlyApi _api;
    private readonly ComboBox _client = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _gameRoot = new();
    private readonly TextBox _savedVariables = new();
    private readonly Label _account = new();
    private readonly Label _status = new();
    private readonly Label _addonStatus = new();
    private readonly Button _checkAddon = new() { Text = "Check for updates" };
    private readonly Dictionary<ClientKind, AddonVersion> _latestAddons = new();
    private bool _busy;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Button _connect = new() { Text = "Connect Realmly account" };
    private readonly Button _install = new() { Text = "Install or update Add-on" };
    private readonly Button _upload = new() { Text = "Upload Realmly.lua" };
    private readonly Button _companionUpdate = new() { Text = "Update Companion", Visible = false };
    private readonly Button _checkCompanion = new() { Text = "Check Companion updates" };
    private readonly Label _recordStatus = new() { AutoSize = true, MaximumSize = new Size(650, 0), UseMnemonic = false };
    private readonly Label _historyStatus = new() { AutoSize = true, MaximumSize = new Size(650, 0), UseMnemonic = false };
    private readonly Button _scan = new() { Text = "Refresh preview" };
    private readonly Button _historyButton = new() { Text = "Upload history" };
    private UploadJournal _journal = new();
    private readonly bool _persistJournal;
    private readonly System.Windows.Forms.Timer _previewTimer = new() { Interval = 10000 };
    private string? _fileStamp;
    private bool _scanning;
    private readonly ToolTip _uploadHints = new();
    private readonly Label _gameStatus = new() { AutoSize = true, MaximumSize = new Size(650, 0), UseMnemonic = false };
    private readonly Func<string, ClientKind, WowRunState> _detectWow;
    private readonly CompanionTray _tray;
    private readonly Button _options = new() { Text = "App options" };
    private readonly System.Windows.Forms.Timer _updateTimer = new() { Interval = 6 * 60 * 60 * 1000 };
    private bool _exitRequested, _restarting;
    private FormWindowState _restoreState = FormWindowState.Normal;
    private readonly ComboBox _installations = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _manageSetups = new() { Text = "Setups..." };
    private WowInstallation _currentInstallation;
    private bool _switchingSetup;
    private readonly DiagnosticLog _diagnostics = new();
    private readonly Action<string> _copyToClipboard;
    private readonly Func<IWin32Window, bool> _confirmAutomaticUploads;
    private int? _previewRecords, _previewCharacters;
    private readonly AutoUploadPolicy _autoPolicy = new();
    private readonly Label _autoStatus = new() { AutoSize = true, MaximumSize = new Size(650, 0), UseMnemonic = false };
    private CancellationTokenSource? _autoCancellation;
    private bool _autoPass;
    private volatile bool _closing;
    private string _selectedSavedPath = "";

    public CompanionForm(CompanionSettings? settings = null, Func<string, ClientKind, WowRunState>? detectWow = null, Action<string>? copyToClipboard = null, RealmlyApi? api = null, Func<IWin32Window, bool>? confirmAutomaticUploads = null)
    {
        _settings = settings ?? CompanionSettings.Load();
        _api = api ?? new RealmlyApi();
        _currentInstallation = _settings.EnsureInstallation();
        _copyToClipboard = copyToClipboard ?? Clipboard.SetText;
        _confirmAutomaticUploads = confirmAutomaticUploads ?? ConfirmAutomaticUploads;
        _detectWow = detectWow ?? (settings is null ? WowProcessDetector.Check : (_, _) => WowRunState.Stopped);
        _persistJournal = settings is null;
        if (_persistJournal)
        {
            try { _journal = UploadJournal.Load(UploadJournal.DefaultPath); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
            {
                _diagnostics.Record(DiagnosticOperation.History, error);
                // Leave a damaged history untouched until the user explicitly completes a new upload.
                _historyStatus.Text = "Previous local history could not be read. New-record counts may include older uploads.";
            }
        }
        Text = "Realmly Companion";
        using (var stream = typeof(CompanionForm).Assembly.GetManifestResourceStream("RealmlyCompanion.Icon.ico")
            ?? throw new InvalidOperationException("The Companion icon is missing."))
        using (var icon = new Icon(stream)) Icon = (Icon)icon.Clone();
        _tray = new CompanionTray(Icon, RestoreFromTray, async () => await CheckAllUpdatesAsync(), RequestExit, native: settings is null);
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(780, 700);
        ClientSize = new Size(900, 830);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(15, 11, 9);
        ForeColor = Color.FromArgb(245, 229, 184);
        Font = new Font("Segoe UI", 10F);
        _client.Items.AddRange(["WoW Retail tester", "WoW Forever"]);
        _client.SelectedIndex = _settings.Client == ClientKind.Forever ? 1 : 0;
        _gameRoot.Text = _settings.GameRoot ?? (settings is null ? WowFiles.FindGameRoot() : null) ?? "";
        _savedVariables.Text = _settings.SavedVariablesPath ?? "";
        _selectedSavedPath = _savedVariables.Text;
        RebuildInstallationPicker();
        foreach (var label in new[] { _account, _status, _addonStatus })
        {
            label.AutoSize = true;
            label.MaximumSize = new Size(760, 0);
            label.Margin = new Padding(0, 4, 0, 10);
        }
        _status.ForeColor = Color.FromArgb(225, 209, 175);
        foreach (var button in new[] { _connect, _install, _upload, _checkAddon, _companionUpdate, _checkCompanion, _scan, _historyButton, _options }) StyleButton(button);
        StyleButton(_manageSetups, compact: true);
        _upload.Text = "Review & upload";
        _recordStatus.ForeColor = Color.FromArgb(225, 209, 175);
        _historyStatus.ForeColor = Color.FromArgb(195, 176, 143);
        _autoStatus.ForeColor = Color.FromArgb(195, 176, 143);
        _upload.BackColor = Color.FromArgb(99, 65, 24);
        _upload.Font = new Font(Font, FontStyle.Bold);

        var scroll = new RealmlyScrollPanel();
        var content = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(24, 18, 24, 18), ColumnCount = 1 };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Add(Control control)
        {
            var row = content.RowCount++;
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            content.Controls.Add(control, 0, row);
        }
        Add(Heading("REALMLY COMPANION  /  " + typeof(CompanionForm).Assembly.GetName().Version?.ToString(3)));
        Add(Title("Bring your adventures home."));
        Add(Copy("Keep the collector current. Send only the field notes you choose."));
        var setup = Section("01  GAME & ADDON", "", _gameStatus);
        AddRow(setup, 2, "Saved setup", _installations);
        setup.Controls.Add(_manageSetups, 2, 2);
        AddRow(setup, 3, "Game client", _client);
        AddRow(setup, 4, "WoW folder", _gameRoot, BrowseFolder);
        FullRow(setup, 5, _addonStatus);
        FullRow(setup, 6, Actions(_install, _checkAddon));
        Add(setup);
        var account = Section("02  REALMLY ACCOUNT", "Secure browser sign-in. Your token is encrypted on this PC.");
        FullRow(account, 2, _account);
        FullRow(account, 3, Actions(_connect));
        Add(account);
        var upload = Section("03  SEND YOUR FIELD NOTES", "After playing, log out or /reload before uploading.");
        AddRow(upload, 2, "Realmly.lua", _savedVariables, BrowseLua);
        FullRow(upload, 4, _recordStatus);
        FullRow(upload, 5, _autoStatus);
        FullRow(upload, 6, Actions(_upload, _scan, _historyButton));
        FullRow(upload, 7, _historyStatus);
        _uploadHints.SetToolTip(_savedVariables, "Choose WTF / Account / ... / SavedVariables / Realmly.lua, not the addon source.");
        _uploadHints.SetToolTip(_recordStatus, "New counts track uploads from this PC only. Website and other-PC uploads are not included. Review & upload shows all characters and dates; Realmly handles duplicates.");
        Add(upload);
        Add(_status);
        Add(_companionUpdate);
        Add(Actions(_checkCompanion, _options));
        scroll.SetContent(content);
        SetWindowContent(scroll);
        Shown += (_, _) => scroll.ScrollTo(0);

        _client.SelectedIndexChanged += async (_, _) =>
        {
            if (_switchingSetup) return;
            CaptureInstallation();
            ActivateInstallation(_settings.ForClient(SelectedClient, _currentInstallation.GameRoot));
            SaveSettings();
            if (settings is null && Visible && !_busy) await CheckAddonAsync();
        };
        _installations.SelectedIndexChanged += async (_, _) =>
        {
            if (_switchingSetup || _installations.SelectedItem is not WowInstallation installation || installation == _currentInstallation) return;
            CaptureInstallation();
            ActivateInstallation(installation);
            SaveSettings();
            if (settings is null && Visible && !_busy) await CheckAddonAsync();
        };
        _manageSetups.Click += (_, _) => { if (!_busy) { using var dialog = CreateSetupsDialog(); dialog.ShowDialog(this); } };
        _gameRoot.TextChanged += (_, _) => { if (!_switchingSetup) { DisableAutoForPathChange(); RefreshAddonStatus(); RefreshGameState(); } };
        _gameRoot.Leave += (_, _) =>
        {
            RefreshSavedVariables(reset: !string.Equals(_settings.GameRoot, _gameRoot.Text, StringComparison.OrdinalIgnoreCase));
            SaveSettings();
        };
        _savedVariables.Leave += (_, _) => SaveSettings();
        _connect.Click += async (_, _) => await ConnectAsync();
        _install.Click += async (_, _) => await InstallAsync();
        _checkAddon.Click += async (_, _) => await CheckAddonAsync();
        _upload.Click += async (_, _) => await UploadAsync();
        _scan.Click += async (_, _) => await RefreshPreviewAsync(force: true);
        _historyButton.Click += (_, _) => ShowUploadHistory();
        _savedVariables.TextChanged += (_, _) => { _selectedSavedPath = _savedVariables.Text; if (!_switchingSetup) DisableAutoForPathChange(); _fileStamp = null; _previewRecords = _previewCharacters = null; _recordStatus.Text = "Refresh preview to inspect this file."; };
        Activated += async (_, _) => { if (Visible) { RefreshGameState(); await RefreshPreviewAsync(); } };
        _previewTimer.Tick += async (_, _) => { RefreshGameState(); await RefreshPreviewAsync(); if (_persistJournal) await ProcessAutomaticUploadAsync(DateTimeOffset.UtcNow); };
        Shown += async (_, _) => { _previewTimer.Start(); await RefreshPreviewAsync(force: true); };
        _companionUpdate.Click += async (_, _) => await UpdateCompanionAsync();
        _options.Click += (_, _) => { using var dialog = CreateOptionsDialog(); dialog.ShowDialog(this); };
        _updateTimer.Tick += async (_, _) => { if (_persistJournal) await CheckAllUpdatesAsync(); };
        _checkCompanion.Click += async (_, _) =>
        {
            if (!_busy) await BusyAsync("Checking for Companion updates...", _ => CheckForCompanionUpdateAsync(manual: true), DiagnosticOperation.CompanionCheck);
        };
        if (settings is null) FormClosing += (_, _) => { if (!_busy) SaveSettings(); };
        FormClosing += (_, eventArgs) =>
        {
            if (eventArgs.CloseReason == CloseReason.UserClosing && !_exitRequested && !_restarting && _settings.CloseToTray)
            { eventArgs.Cancel = true; HideToTray(); }
            else { _closing = true; _autoCancellation?.Cancel(); _lifetime.Cancel(); }
        };
        FormClosed += (_, _) => { _previewTimer.Stop(); _previewTimer.Dispose(); _updateTimer.Stop(); _updateTimer.Dispose(); _tray.Dispose(); _uploadHints.Dispose(); _autoCancellation?.Cancel(); _lifetime.Cancel(); };
        Resize += (_, _) =>
        {
            var width = Math.Max(320, ClientSize.Width - 120);
            foreach (var label in new[] { _account, _status, _addonStatus }) label.MaximumSize = new Size(width, 0);
            if (Visible && _settings.CloseToTray && WindowState == FormWindowState.Minimized) HideToTray();
            else if (WindowState != FormWindowState.Minimized) _restoreState = WindowState;
        };
        RefreshAccount();
        RefreshSavedVariables();
        RefreshAddonStatus();
        RefreshGameState();
        ConfigureTray();
        RefreshHistoryStatus();
        RefreshAutoStatus();
        SetStatus("Ready. Check your addon above, or connect your account to upload.");
        if (settings is null) Shown += async (_, _) =>
        {
            await CheckForCompanionUpdateAsync();
            await CheckAddonAsync();
        };
    }

    private static TableLayoutPanel Section(string title, string description, Label? detail = null)
    {
        var table = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3, Padding = new Padding(12), Margin = new Padding(0, 12, 0, 0),
            BackColor = Color.FromArgb(29, 22, 16) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        FullRow(table, 0, Heading(title));
        FullRow(table, 1, detail ?? Copy(description));
        return table;
    }

    private static void EnsureRows(TableLayoutPanel table, int row)
    {
        while (table.RowStyles.Count <= row) table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    }

    private static void FullRow(TableLayoutPanel table, int row, Control control)
    {
        EnsureRows(table, row);
        control.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        table.Controls.Add(control, 0, row);
        table.SetColumnSpan(control, 3);
    }

    private static FlowLayoutPanel Actions(params Button[] buttons)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true, Margin = Padding.Empty };
        panel.Controls.AddRange(buttons);
        return panel;
    }

    private void RefreshAddonStatus()
    {
        _addonStatus.ForeColor = Color.FromArgb(239, 200, 119);
        try
        {
            if (!Directory.Exists(_gameRoot.Text))
            {
                _addonStatus.Text = "Choose your WoW folder to see the installed addon version.";
                _install.Text = "Install addon";
                return;
            }
            var installed = WowFiles.InstalledVersion(_gameRoot.Text, SelectedClient);
            var exists = File.Exists(WowFiles.TocPath(_gameRoot.Text, SelectedClient));
            _latestAddons.TryGetValue(SelectedClient, out var latest);
            var local = installed is not null ? "Installed: " + installed.Display : exists ? "Installed version: unreadable" : "Addon not installed";
            var remote = latest is null ? "Latest: not checked" : "Latest: " + latest.Display;
            var state = latest is null ? "Check online for updates."
                : installed is null ? "Ready to install."
                : installed.Number < latest.Number ? "Update available."
                : installed.Number == latest.Number ? "You're up to date."
                : "Your installed version is newer than the published release.";
            _addonStatus.Text = local + "  |  " + remote + "\n" + state;
            _addonStatus.ForeColor = installed is not null && latest is not null && installed.Number >= latest.Number
                ? Color.FromArgb(160, 211, 157) : Color.FromArgb(239, 200, 119);
            _install.Text = !exists ? "Install addon" : latest is not null && installed is not null && installed.Number < latest.Number
                ? "Update to " + latest.Display : "Reinstall latest addon";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _diagnostics.Record(DiagnosticOperation.AddonCheck, error);
            _addonStatus.Text = "Cannot read the installed addon: " + error.Message;
            _install.Text = "Install or repair addon";
        }
    }

    private async Task CheckAddonAsync()
    {
        if (_busy) return;
        if (!Directory.Exists(_gameRoot.Text)) { SetStatus("Choose your World of Warcraft folder before checking for addon updates."); return; }
        await BusyAsync("Checking the latest addon for " + _client.Text + "...", async cancellationToken =>
        {
            var client = SelectedClient;
            try
            {
                _latestAddons[client] = await WowFiles.LatestVersionAsync(client, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (_closing || IsDisposed || Disposing) return;
                RefreshAddonStatus();
                NotifyAddonUpdate(client, _latestAddons[client]);
                SetStatus("Addon check complete - " + DateTime.Now.ToString("t") + ". No files were changed.");
            }
            catch
            {
                if (!IsDisposed)
                {
                    RefreshAddonStatus();
                    _addonStatus.Text += "\nOnline check failed. Retry when connected; your installed addon is unchanged.";
                }
                throw;
            }
        }, DiagnosticOperation.AddonCheck);
    }

    private ClientKind SelectedClient => _client.SelectedIndex == 1 ? ClientKind.Forever : ClientKind.Retail;

    private void AddRow(TableLayoutPanel table, int row, string label, Control value, EventHandler? browse = null)
    {
        EnsureRows(table, row);
        table.Controls.Add(new Label { Text = label, AutoSize = true, MinimumSize = new Size(110, 24), Anchor = AnchorStyles.Left, ForeColor = Color.FromArgb(216, 168, 79), UseMnemonic = false }, 0, row);
        value.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        value.Margin = new Padding(0, 6, 10, 6);
        if (value is TextBox box) box.BorderStyle = BorderStyle.FixedSingle;
        table.Controls.Add(value, 1, row);
        if (browse is not null)
        {
            var button = new Button { Text = "Browse", Name = "browse" };
            StyleButton(button, compact: true);
            button.Click += browse;
            table.Controls.Add(button, 2, row);
        }
    }

    private static void StyleButton(Button button, bool compact = false)
    {
        button.AutoSize = true;
        button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(170, 119, 41);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(67, 45, 22);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(87, 58, 25);
        button.BackColor = Color.FromArgb(37, 24, 15);
        button.ForeColor = Color.FromArgb(245, 229, 184);
        button.MinimumSize = new Size(0, compact ? 30 : 38);
        button.Padding = compact ? new Padding(10, 3, 10, 3) : new Padding(14, 6, 14, 6);
        button.Margin = new Padding(0, compact ? 2 : 5, 12, compact ? 2 : 5);
        button.UseVisualStyleBackColor = false;
        button.UseMnemonic = false;
        void FitText()
        {
            var text = TextRenderer.MeasureText(button.Text, button.Font);
            button.MinimumSize = new Size(text.Width + button.Padding.Horizontal + 16,
                Math.Max(compact ? 32 : 38, text.Height + button.Padding.Vertical + 10));
        }
        button.FontChanged += (_, _) => FitText();
        button.TextChanged += (_, _) => FitText();
        FitText();
    }

    private static Label Heading(string text) => new() { Text = text, AutoSize = true, UseMnemonic = false, Margin = new Padding(0, 0, 0, 8), ForeColor = Color.FromArgb(216, 168, 79), Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
    private static Label Title(string text) => new() { Text = text, AutoSize = true, UseMnemonic = false, Margin = new Padding(0, 0, 0, 11), Font = new Font("Georgia", 22F) };
    private static Label Copy(string text) => new() { Text = text, AutoSize = true, MaximumSize = new Size(650, 0), ForeColor = Color.FromArgb(195, 176, 143) };

    private async void BrowseFolder(object? sender, EventArgs eventArgs)
    {
        using var dialog = new FolderBrowserDialog { Description = "Choose the World of Warcraft folder (the folder containing _retail_ or _classic_beta_)" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _gameRoot.Text = dialog.SelectedPath;
        RefreshSavedVariables(reset: true);
        SaveSettings();
        await CheckAddonAsync();
    }

    private void BrowseLua(object? sender, EventArgs eventArgs)
    {
        var initialDirectory = Path.GetDirectoryName(_savedVariables.Text);
        if (string.IsNullOrWhiteSpace(initialDirectory) || !Directory.Exists(initialDirectory))
            initialDirectory = WowFiles.AccountsPath(_gameRoot.Text, SelectedClient);
        using var dialog = new OpenFileDialog
        {
            Filter = "Realmly SavedVariables (*.lua)|*.lua|All files (*.*)|*.*",
            CheckFileExists = true,
            FileName = "Realmly.lua",
            InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : "",
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) { _savedVariables.Text = dialog.FileName; SaveSettings(); }
    }

    private void RefreshSavedVariables(bool reset = false)
    {
        if (!reset && File.Exists(_savedVariables.Text)) return;
        if (!Directory.Exists(_gameRoot.Text)) { if (reset) _savedVariables.Clear(); return; }
        var candidate = WowFiles.PreferredSavedVariablesPath(_gameRoot.Text, SelectedClient);
        if (candidate is not null) _savedVariables.Text = candidate;
        else if (reset) _savedVariables.Clear();
    }

    private void RefreshAccount()
    {
        var connected = !string.IsNullOrWhiteSpace(_settings.AccessToken);
        _account.Text = connected ? "Connected - ready to upload." : "Not connected yet.";
        _connect.Text = connected ? "Reconnect Realmly account" : "Connect Realmly account";
        _upload.Enabled = connected && !_busy;
    }

    private async Task ConnectAsync()
    {
        await BusyAsync("Opening your secure browser approval…", async cancellationToken =>
        {
            var pairing = await _api.StartPairingAsync(cancellationToken);
            Process.Start(new ProcessStartInfo(pairing.VerificationUri) { UseShellExecute = true });
            var deadline = DateTimeOffset.UtcNow.AddSeconds(pairing.ExpiresIn);
            while (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                var status = await _api.PollPairingAsync(pairing, cancellationToken);
                if (!status.Approved || string.IsNullOrWhiteSpace(status.AccessToken)) continue;
                AcceptConnectedToken(status.AccessToken);
                SetStatus("Connected. Upload manually, or enable automatic uploads for this setup in App options.");
                return;
            }
            throw new InvalidOperationException("The approval timed out. Please try connecting again.");
        }, DiagnosticOperation.Pairing);
    }

    private async Task InstallAsync()
    {
        if (_busy) return;
        if (!Directory.Exists(_gameRoot.Text)) { SetStatus("Choose your World of Warcraft folder first."); return; }
        try { WowProcessDetector.RequireStopped(RefreshGameState()); }
        catch (InvalidOperationException error) { _diagnostics.Record(DiagnosticOperation.AddonInstall, error); SetStatus(error.Message); return; }
        var addOns = WowFiles.AddOnsPath(_gameRoot.Text, SelectedClient);
        var target = Path.Combine(addOns, "Realmly");
        var installed = WowFiles.InstalledVersion(_gameRoot.Text, SelectedClient);
        _latestAddons.TryGetValue(SelectedClient, out var latest);
        var downgrade = installed is not null && latest is not null && installed.Number > latest.Number
            ? "\n\nYour installed version is newer. This will install the older published release." : "";
        var answer = MessageBox.Show(this, $"Close WoW before continuing. Realmly will replace only this folder:\n\n{target}\n\nYour SavedVariables files are not touched.{downgrade}", "Install or update Realmly", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
        if (answer != DialogResult.OK) return;
        await BusyAsync("Preparing the Realmly Add-on…", async cancellationToken =>
        {
            await WowFiles.InstallAsync(_gameRoot.Text, SelectedClient, SetStatus, cancellationToken,
                () => WowProcessDetector.RequireStopped(RefreshGameState()));
            SaveSettings();
            RefreshSavedVariables();
            _latestAddons[SelectedClient] = WowFiles.InstalledVersion(_gameRoot.Text, SelectedClient)
                ?? throw new InvalidOperationException("Installed addon version could not be verified.");
            RefreshAddonStatus();
            SetStatus("Realmly is installed. Start WoW, sign into a character, then log out or type /reload to create Realmly.lua at the shown path.");
        }, DiagnosticOperation.AddonInstall);
    }

    private async Task UploadAsync()
    {
        var token = _settings.AccessToken;
        if (string.IsNullOrWhiteSpace(token)) { RefreshAccount(); return; }
        if (!File.Exists(_savedVariables.Text)) { SetStatus("Realmly.lua has not been written yet. Start WoW with Realmly enabled, then log out or type /reload and try again."); return; }
        await BusyAsync("Reading your selected field notes...", async cancellationToken =>
        {
            var preview = await ReadPreviewAsync(cancellationToken);
            _recordStatus.Text = PreviewText(preview);
            if (!ConfirmUpload(preview)) { SetStatus("Upload cancelled. Nothing was sent."); return; }
            SetStatus("Uploading the reviewed snapshot...");
            UploadResult result;
            try
            {
                // Send exactly what was reviewed, even if WoW saves the file while the dialog is open.
                result = await _api.UploadSourceAsync(preview.Source, token, cancellationToken);
                if (!result.Ok) throw new InvalidOperationException("Realmly did not confirm this upload. Please retry.");
            }
            catch
            {
                _journal.Record(preview, SelectedClient, null);
                PersistJournal();
                throw;
            }
            _journal.Record(preview, SelectedClient, result);
            ResetAutoWatch();
            var saved = PersistJournal();
            _recordStatus.Text = PreviewText(UploadPreview.Parse(preview.Source, _journal.UploadedRecords));
            var imported = result.Batch ? result.ImportedCount : (result.Duplicate ? 0 : 1);
            SetStatus((imported > 0 ? $"Realmly accepted your upload ({result.LocationCount} discoveries reported)." : "Those field notes were already safely on record.")
                + (saved ? "" : " Local history could not be saved; counts may be repeated next time."));
        }, DiagnosticOperation.Upload);
    }

    private async Task<UploadPreview> ReadPreviewAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_closing || IsDisposed || Disposing) throw new OperationCanceledException(cancellationToken);
        var path = _selectedSavedPath;
        var installationId = _currentInstallation.Id;
        var known = _journal.UploadedRecords.ToArray();
        if (new FileInfo(path).Length > 2 * 1024 * 1024) throw new InvalidOperationException("Realmly.lua is larger than the 2 MB upload limit.");
        var source = await File.ReadAllTextAsync(path, cancellationToken);
        var preview = await Task.Run(() => UploadPreview.Parse(source, known), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (_closing || IsDisposed || Disposing) throw new OperationCanceledException(cancellationToken);
        if (!IsDisposed && path == _selectedSavedPath && installationId == _currentInstallation.Id)
        { _previewRecords = preview.Records; _previewCharacters = preview.Characters.Length; }
        return preview;
    }

    private static string PreviewText(UploadPreview preview) =>
        (preview.NewRecords == 0 ? "Up to date on this PC" : $"{preview.NewRecords} new on this PC")
        + $"  /  {preview.Records} records  /  {preview.Characters.Length} characters";

    private async Task RefreshPreviewAsync(bool force = false)
    {
        if (_busy || _scanning || _closing || IsDisposed || Disposing) return;
        _scanning = true;
        var path = _selectedSavedPath;
        try
        {
            if (string.IsNullOrWhiteSpace(path)) { _recordStatus.Text = "Choose your SavedVariables file to preview its records."; return; }
            var info = new FileInfo(path);
            var stamp = info.FullName + (info.Exists ? $"|{info.Length}|{info.LastWriteTimeUtc.Ticks}" : "|missing");
            if (!force && stamp == _fileStamp) return;
            var preview = await ReadPreviewAsync(_lifetime.Token);
            await UpdatePreviewOnUiAsync(() =>
            {
                if (!_busy && path == _selectedSavedPath) { _recordStatus.Text = PreviewText(preview); _fileStamp = stamp; }
            });
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException or OperationCanceledException)
        {
            _diagnostics.Record(DiagnosticOperation.Preview, error);
            await UpdatePreviewOnUiAsync(() =>
            {
                if (!_busy && path == _selectedSavedPath) _recordStatus.Text = "Preview unavailable: " + (error is OperationCanceledException ? "cancelled." : error.Message);
            });
        }
        finally { _scanning = false; }
    }

    private Task UpdatePreviewOnUiAsync(Action update)
    {
        if (_closing || IsDisposed || Disposing || !IsHandleCreated) return Task.CompletedTask;
        if (!InvokeRequired) { update(); return Task.CompletedTask; }
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellation = _lifetime.Token.Register(() => completion.TrySetResult());
        try
        {
            BeginInvoke(new Action(() =>
            {
                try { if (!_closing && !IsDisposed && !Disposing) update(); completion.TrySetResult(); }
                catch (Exception error) { completion.TrySetException(error); }
                finally { cancellation.Dispose(); }
            }));
        }
        catch (InvalidOperationException) { cancellation.Dispose(); completion.TrySetResult(); }
        return completion.Task;
    }

    private bool ConfirmUpload(UploadPreview preview)
    {
        using var dialog = CreateUploadDialog(preview);
        return dialog.ShowDialog(this) == DialogResult.OK;
    }

    private Form CreateUploadDialog(UploadPreview preview)
    {
        var state = RefreshGameState();
        var warning = state == WowRunState.Stopped ? "" : "WoW may still be running. Only data already saved to disk is included. Type /reload or log out, then cancel and refresh if needed.\n\n";
        var dialog = CreateTextDialog("Review your upload", warning + preview.Summary + "\n\nAll " + preview.Records + " records in this snapshot will be sent. Realmly handles duplicates.\nLog out or /reload first to include your latest adventures.", out var buttons);
        var send = new Button { Text = "Upload this snapshot", DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        StyleButton(send); StyleButton(cancel);
        buttons.Controls.AddRange([send, cancel]);
        dialog.AcceptButton = send; dialog.CancelButton = cancel;
        return dialog;
    }

    private void ShowUploadHistory()
    {
        var text = _journal.History.Count == 0 ? "No uploads recorded on this PC yet." : string.Join("\n\n", _journal.History.Select(attempt =>
            $"{attempt.At.ToLocalTime():g} - {attempt.Client} - {(attempt.Automatic ? "Automatic" : "Manual")}\n{attempt.Characters} character(s), {attempt.Records} records submitted\n{attempt.Outcome}"));
        using var dialog = CreateTextDialog("Upload history - this PC", text, out var buttons);
        var close = new Button { Text = "Close", DialogResult = DialogResult.Cancel };
        StyleButton(close); buttons.Controls.Add(close); dialog.CancelButton = close;
        dialog.ShowDialog(this);
    }

    private Form CreateTextDialog(string title, string text, out FlowLayoutPanel buttons)
    {
        var dialog = new RealmlyWindow { Text = title, Icon = Icon, Font = Font, ForeColor = ForeColor,
            BackColor = BackColor, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(680, 500), MinimumSize = new Size(540, 360), ShowInTaskbar = false };
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(18) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(Heading(title.ToUpperInvariant()), 0, 0);
        var body = new RealmlyTextBox { Text = text.Replace("\r\n", "\n").Replace("\n", Environment.NewLine), Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None, BackColor = BackColor, ForeColor = ForeColor, AccessibleName = title };
        panel.Controls.Add(body.WithScrollBar(), 0, 1);
        buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        panel.Controls.Add(buttons, 0, 2); dialog.SetWindowContent(panel);
        dialog.Shown += (_, _) => { body.Select(0, 0); if (dialog.AcceptButton is Control primary) primary.Focus(); };
        return dialog;
    }

    private bool PersistJournal()
    {
        RefreshHistoryStatus();
        if (!_persistJournal) return true;
        try { _journal.Save(UploadJournal.DefaultPath); return true; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { _diagnostics.Record(DiagnosticOperation.History, error); return false; }
    }

    private void RefreshHistoryStatus()
    {
        if (_journal.History.FirstOrDefault() is { } last)
        {
            _historyStatus.Text = last.Succeeded ? $"Last upload: {last.At.ToLocalTime():g}" : $"Last attempt failed: {last.At.ToLocalTime():g}. See upload history.";
            if (!last.Succeeded && _journal.History.FirstOrDefault(attempt => attempt.Succeeded) is { } success)
                _historyStatus.Text += $"\nLast successful upload: {success.At.ToLocalTime():g}.";
        }
        else if (_historyStatus.Text.Length == 0) _historyStatus.Text = "No uploads on this PC yet.";
    }

    private async Task UpdateCompanionAsync()
    {
        if (_busy || _companionUpdate.Tag is not CompanionRelease offered) return;
        if (MessageBox.Show(this, $"Download Companion {offered.Version}, replace this app and restart it?\n\nYour settings, account connection and addon data will be kept.",
            "Update Realmly Companion", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
        await BusyAsync("Preparing the Companion update...", async cancellationToken =>
        {
            var release = await _api.GetLatestReleaseAsync(cancellationToken);
            var current = typeof(CompanionForm).Assembly.GetName().Version!;
            if (!Version.TryParse(release.Version, out var latest) || latest <= current)
            {
                SetStatus("This Companion is already current. No files were changed.");
                _companionUpdate.Visible = false;
                return;
            }
            var update = await SelfUpdater.PrepareAsync(release, SetStatus, cancellationToken);
            var target = Environment.ProcessPath ?? throw new InvalidOperationException("The running Companion path could not be found.");
            SaveSettings();
            SetStatus("Update verified. Restarting Realmly Companion...");
            using var process = Process.GetCurrentProcess();
            await SelfUpdater.LaunchHelperAsync(update, target, process.Id, process.StartTime.ToUniversalTime().Ticks, cancellationToken);
            _restarting = true; // Updating must exit, not disappear into the tray and block the helper.
            Close();
        }, DiagnosticOperation.CompanionUpdate);
    }

    private async Task CheckForCompanionUpdateAsync(bool manual = false)
    {
        try
        {
            var release = await _api.GetLatestReleaseAsync(_lifetime.Token);
            if (_closing || IsDisposed || Disposing || _lifetime.IsCancellationRequested) return;
            var current = typeof(CompanionForm).Assembly.GetName().Version;
            if (current is null || !Version.TryParse(release.Version, out var latest)) throw new InvalidOperationException("The Companion release version is unreadable.");
            if (latest.CompareTo(current) <= 0)
            {
                _companionUpdate.Visible = false;
                if (manual) SetStatus("Your Companion is up to date (" + current.ToString(3) + ").");
                return;
            }
            _companionUpdate.Tag = release;
            _companionUpdate.Text = $"Update Companion to {latest} and restart";
            _companionUpdate.Visible = true;
            SetStatus($"Companion {latest} is available. Use Update Companion to download it and restart here.");
            _tray.NotifyUpdate("companion:" + latest, $"Companion {latest} is ready. Open Realmly to review and install it.", _settings.NotifyUpdates);
        }
        catch (Exception error)
        {
            _diagnostics.Record(DiagnosticOperation.CompanionCheck, error);
            if (manual && !IsDisposed) SetStatus("Could not check Companion updates: " + error.Message);
            // Updates are a convenience. The Companion must remain usable offline.
        }
    }

    private WowRunState RefreshGameState()
    {
        WowRunState state;
        try { state = _detectWow(_gameRoot.Text, SelectedClient); }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        { state = WowRunState.Unknown; }
        _gameStatus.Text = string.IsNullOrWhiteSpace(_gameRoot.Text) ? "Choose your WoW folder to check game status." : state switch
        {
            WowRunState.Running => "WoW running: /reload before uploading; exit before addon updates.",
            WowRunState.Unknown => "WoW status unavailable. Close all WoW clients before addon updates.",
            _ => "WoW closed. Ready for addon updates."
        };
        _gameStatus.ForeColor = state == WowRunState.Stopped ? Color.FromArgb(160, 211, 157) : Color.FromArgb(239, 200, 119);
        return state;
    }

    private void NotifyAddonUpdate(ClientKind client, AddonVersion latest)
    {
        var installed = WowFiles.InstalledVersion(_gameRoot.Text, client);
        if (installed is not null && installed.Number < latest.Number)
            _tray.NotifyUpdate($"addon:{client}:{latest.Display}", $"Realmly addon {latest.Display} is ready for {client}. Close WoW, then open Companion to update.", _settings.NotifyUpdates);
    }

    private async Task CheckAllUpdatesAsync()
    {
        if (_busy || IsDisposed) return;
        if (Directory.Exists(_gameRoot.Text)) await CheckAddonAsync();
        if (!_busy && !IsDisposed) await BusyAsync("Checking Companion updates...", _ => CheckForCompanionUpdateAsync(manual: true), DiagnosticOperation.CompanionCheck);
    }

    private void ConfigureTray()
    {
        _tray.Enabled = _settings.CloseToTray;
        if (_persistJournal && _settings.CloseToTray && _settings.NotifyUpdates) _updateTimer.Start();
        else _updateTimer.Stop();
    }

    private void SetTrayPreferences(bool closeToTray, bool notifyUpdates)
    {
        _settings.CloseToTray = closeToTray;
        _settings.NotifyUpdates = notifyUpdates;
        ConfigureTray();
        if (!closeToTray && !Visible) RestoreFromTray();
        if (_persistJournal) SaveSettings();
        if (notifyUpdates && closeToTray)
        {
            if (_companionUpdate.Tag is CompanionRelease release)
                _tray.NotifyUpdate("companion:" + release.Version, $"Companion {release.Version} is ready. Open Realmly to review and install it.", true);
            if (_latestAddons.TryGetValue(SelectedClient, out var latest))
            {
                try { NotifyAddonUpdate(SelectedClient, latest); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            }
        }
    }

    private void CaptureInstallation()
    {
        _currentInstallation.GameRoot = _gameRoot.Text;
        _currentInstallation.SavedVariablesPath = _savedVariables.Text;
        _settings.Remember(_currentInstallation);
    }

    private void RebuildInstallationPicker()
    {
        var switching = _switchingSetup;
        _switchingSetup = true;
        try
        {
            _installations.Items.Clear();
            _installations.Items.AddRange(_settings.Installations.Cast<object>().ToArray());
            _installations.SelectedItem = _currentInstallation;
        }
        finally { _switchingSetup = switching; }
    }

    private void ActivateInstallation(WowInstallation installation)
    {
        ResetAutoWatch();
        _switchingSetup = true;
        try
        {
            _currentInstallation = installation;
            _settings.Remember(installation);
            _client.SelectedIndex = installation.Client == ClientKind.Forever ? 1 : 0;
            _gameRoot.Text = installation.GameRoot ?? "";
            _savedVariables.Text = installation.SavedVariablesPath ?? "";
            if (string.IsNullOrWhiteSpace(_savedVariables.Text)) RefreshSavedVariables();
            _fileStamp = null;
            _previewRecords = _previewCharacters = null;
            _recordStatus.Text = "Refresh preview to inspect this setup.";
            RebuildInstallationPicker();
        }
        finally { _switchingSetup = false; }
        RefreshAddonStatus();
        RefreshGameState();
        RefreshAutoStatus();
    }

    private Form CreateSetupsDialog()
    {
        var dialog = new RealmlyWindow { Text = "Saved setups", Icon = Icon, Font = Font, ForeColor = ForeColor,
            BackColor = BackColor, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(660, 390), MinimumSize = new Size(620, 370), ShowInTaskbar = false };
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 5 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 5; row++) panel.RowStyles.Add(new RowStyle(row == 3 ? SizeType.Percent : SizeType.AutoSize, row == 3 ? 100 : 0));
        var name = new TextBox { Text = _currentInstallation.Name, MaxLength = 60, Dock = DockStyle.Top, AccessibleName = "Setup name", Margin = new Padding(0, 8, 0, 12) };
        var feedback = Copy("Save another setup for this client, or rename this one. Forget removes only this shortcut, never game files or characters.");
        var saveAs = new Button { Text = "Save as new" };
        var rename = new Button { Text = "Rename" };
        var forget = new Button { Text = "Forget setup", Enabled = _settings.Installations.Count > 1 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        foreach (var button in new[] { saveAs, rename, forget, cancel }) StyleButton(button);
        void Apply(Action action)
        {
            if (_busy) { feedback.Text = "Wait for the current operation to finish before changing setups."; return; }
            try { action(); SaveSettings(); dialog.DialogResult = DialogResult.OK; dialog.Close(); }
            catch (InvalidOperationException error) { feedback.Text = error.Message; }
        }
        saveAs.Click += (_, _) => Apply(() => { CaptureInstallation(); ActivateInstallation(_settings.SaveAs(_currentInstallation, name.Text)); });
        rename.Click += (_, _) => Apply(() => { _settings.Rename(_currentInstallation, name.Text); RebuildInstallationPicker(); });
        forget.Click += (_, _) =>
        {
            if (MessageBox.Show(dialog, "Forget this saved setup? Game files, characters and account connection will not be changed.", "Forget setup", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.OK)
                Apply(() => ActivateInstallation(_settings.Forget(_currentInstallation)));
        };
        panel.Controls.Add(Heading("SAVED SETUPS / " + SelectedClient.ToString().ToUpperInvariant()), 0, 0);
        panel.Controls.Add(name, 0, 1); panel.Controls.Add(feedback, 0, 2);
        panel.Controls.Add(Actions(saveAs, rename, forget, cancel), 0, 4);
        dialog.AcceptButton = saveAs; dialog.CancelButton = cancel; dialog.SetWindowContent(panel);
        return dialog;
    }

    private string BuildDiagnostics()
    {
        AddonVersion? installed = null;
        try { installed = WowFiles.InstalledVersion(_gameRoot.Text, SelectedClient); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { _diagnostics.Record(DiagnosticOperation.AddonCheck, error); }
        _latestAddons.TryGetValue(SelectedClient, out var latest);
        return DiagnosticReport.Build(new(SelectedClient, installed, latest, RefreshGameState(), Directory.Exists(_gameRoot.Text),
            File.Exists(_savedVariables.Text), !string.IsNullOrWhiteSpace(_settings.AccessToken), _settings.Installations.Count,
            _settings.CloseToTray, _settings.NotifyUpdates, _previewRecords, _previewCharacters, _journal.History.FirstOrDefault()?.Succeeded,
            _currentInstallation.AutoUpload, _autoPolicy.State), _diagnostics);
    }

    private Form CreateDiagnosticsDialog()
    {
        var report = BuildDiagnostics();
        var dialog = CreateTextDialog("Privacy-safe diagnostics", report, out var buttons);
        var copy = new Button { Text = "Copy diagnostics", Name = "copyDiagnostics" };
        var close = new Button { Text = "Close", DialogResult = DialogResult.Cancel };
        StyleButton(copy); StyleButton(close);
        copy.Click += (_, _) =>
        {
            try { _copyToClipboard(report); copy.Text = "Copied"; }
            catch (System.Runtime.InteropServices.ExternalException error)
            { _diagnostics.Record(DiagnosticOperation.General, error); copy.Text = "Copy again"; SetStatus("Clipboard is busy. Please try copying diagnostics again."); }
        };
        buttons.Controls.AddRange([copy, close]); dialog.AcceptButton = copy; dialog.CancelButton = close;
        return dialog;
    }

    private void ResetAutoWatch()
    {
        _autoCancellation?.Cancel();
        _autoPolicy.Reset();
    }

    private void AcceptConnectedToken(string token)
    {
        _settings.AccessToken = token;
        foreach (var installation in _settings.Installations) installation.AutoUpload = false;
        ResetAutoWatch();
        SaveSettings();
        RefreshAccount();
        RefreshAutoStatus();
    }

    private void DisableAutoForPathChange()
    {
        if (!_currentInstallation.AutoUpload) return;
        _currentInstallation.AutoUpload = false;
        ResetAutoWatch();
        RefreshAutoStatus();
        // Persist only the consent change here, not a partially typed folder path.
        if (_persistJournal)
        {
            try { _settings.Save(); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { _diagnostics.Record(DiagnosticOperation.Settings, error); }
        }
        SetStatus("Automatic uploads paused because the folder or Lua file changed. Enable again in App options if wanted.");
    }

    private string? AutoUploadPrerequisiteError() => string.IsNullOrWhiteSpace(_settings.AccessToken)
        ? "Connect your Realmly account before enabling automatic uploads."
        : !File.Exists(_savedVariables.Text)
            ? "Choose an existing SavedVariables Realmly.lua before enabling automatic uploads."
            : null;

    private bool SetAutoUploadPreference(bool enabled)
    {
        if (enabled && AutoUploadPrerequisiteError() is { } reason)
        { SetStatus(reason); return false; }
        _currentInstallation.AutoUpload = enabled;
        ResetAutoWatch();
        SaveSettings();
        RefreshAutoStatus();
        return true;
    }

    private void RefreshAutoStatus()
    {
        if (_closing || IsDisposed || Disposing) return;
        _autoStatus.Text = !_currentInstallation.AutoUpload ? "Auto upload: off" : string.IsNullOrWhiteSpace(_settings.AccessToken)
            ? "Auto upload: connect your account" : _autoPolicy.State switch
            {
                AutoUploadState.Uploading => "Auto upload: sending saved records...",
                AutoUploadState.UpToDate => "Auto upload: up to date",
                AutoUploadState.RetryLater => "Auto upload: retrying later",
                AutoUploadState.SignInRequired => "Auto upload: paused - reconnect your account",
                AutoUploadState.FileNeedsAttention => "Auto upload: paused - check the file or upload manually",
                AutoUploadState.WaitingForFile => "Auto upload: waiting for Realmly.lua",
                _ => "Auto upload: watching saved records"
            };
    }

    private static string? AutoFileStamp(string path)
    {
        var file = new FileInfo(path);
        return file.Exists ? $"{file.Length}:{file.LastWriteTimeUtc.Ticks}" : null;
    }

    private async Task ProcessAutomaticUploadAsync(DateTimeOffset now)
    {
        if (_autoPass || _busy || _closing || IsDisposed || Disposing || OwnedForms.Any(form => form.Visible) || !_currentInstallation.AutoUpload) return;
        var token = _settings.AccessToken;
        if (string.IsNullOrWhiteSpace(token)) { RefreshAutoStatus(); return; }
        var installationId = _currentInstallation.Id;
        var path = _savedVariables.Text;
        var client = SelectedClient;
        var tokenKey = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
        var target = installationId + "\0" + path + "\0" + tokenKey;
        bool StillSelected() => !_closing && !IsDisposed && !Disposing && _currentInstallation.AutoUpload && _currentInstallation.Id == installationId
            && _selectedSavedPath == path && _settings.AccessToken == token;
        _autoPass = true;
        var elapsed = Stopwatch.StartNew();
        try
        {
            var stamp = AutoFileStamp(path);
            if (!_autoPolicy.Observe(target, stamp, now)) { RefreshAutoStatus(); return; }
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            _autoCancellation = cancellation;
            await BusyAsync("Checking saved records for automatic upload...", async _ =>
            {
                UploadPreview? preview = null;
                var sent = false;
                try
                {
                    preview = await ReadPreviewAsync(cancellation.Token);
                    cancellation.Token.ThrowIfCancellationRequested();
                    if (!StillSelected()) return;
                    var afterRead = AutoFileStamp(path);
                    if (afterRead != stamp) { _autoPolicy.Observe(target, afterRead, now + elapsed.Elapsed); SetStatus("The saved file is changing; automatic upload will wait."); return; }
                    if (preview.NewRecords == 0) { _autoPolicy.Complete(); SetStatus("No new saved records to upload."); return; }
                    _autoPolicy.Uploading(); RefreshAutoStatus();
                    sent = true;
                    var result = await _api.UploadSourceAsync(preview.Source, token, cancellation.Token);
                    if (!result.Ok) throw new InvalidOperationException("Realmly did not confirm the automatic upload.");
                    _journal.Record(preview, client, result, automatic: true);
                    var saved = PersistJournal();
                    if (StillSelected())
                    {
                        _autoPolicy.Complete();
                        _recordStatus.Text = PreviewText(UploadPreview.Parse(preview.Source, _journal.UploadedRecords));
                        SetStatus("Saved records uploaded automatically." + (saved ? "" : " Local history could not be saved."));
                    }
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                { if (!IsDisposed) SetStatus("Automatic upload stopped. Any records already accepted by Realmly remain saved there."); }
                catch (Exception error)
                {
                    _diagnostics.Record(DiagnosticOperation.AutomaticUpload, error);
                    if (sent && preview is not null) { _journal.Record(preview, client, null, automatic: true); PersistJournal(); }
                    if (StillSelected()) _autoPolicy.Fail(error, now + elapsed.Elapsed);
                    if (!IsDisposed) SetStatus("Automatic upload paused or delayed. You can still review and upload manually.");
                }
            }, DiagnosticOperation.AutomaticUpload);
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException)
        { _diagnostics.Record(DiagnosticOperation.AutomaticUpload, error); if (StillSelected()) _autoPolicy.Fail(error, now + elapsed.Elapsed); }
        finally { _autoCancellation = null; _autoPass = false; RefreshAutoStatus(); }
    }

    private static bool ConfirmAutomaticUploads(IWin32Window owner) => MessageBox.Show(owner,
        "Enable automatic uploads for this setup's selected Realmly.lua?\n\nAll characters in this file will be sent to the connected Realmly account when new records are saved. This can include existing saved records on the first check.\n\nOnly the selected setup is watched while Companion is running. It never publishes posts or installs updates. Changing the folder/file or reconnecting your account turns this off.\n\nYou can turn it off here at any time. Already-sent data is not removed.",
        "Enable automatic uploads?", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.OK;

    private Form CreateOptionsDialog()
    {
        var dialog = new RealmlyWindow { Text = "Companion options", Icon = Icon, Font = Font, ForeColor = ForeColor,
            BackColor = BackColor, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(620, 470), MinimumSize = new Size(600, 450), ShowInTaskbar = false };
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 6 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 6; row++) panel.RowStyles.Add(new RowStyle(row == 4 ? SizeType.Percent : SizeType.AutoSize, row == 4 ? 100 : 0));
        var trayOption = new ReadableCheckBox { Name = "closeToTray", Text = "Close / minimize to system tray", AutoSize = true, Checked = _settings.CloseToTray, ForeColor = ForeColor, Margin = new Padding(0, 8, 0, 8) };
        var notifyOption = new ReadableCheckBox { Name = "notifyUpdates", Text = "Notify me about app and addon updates" + (_settings.CloseToTray ? "" : "\nRequires tray mode"), AutoSize = true, Checked = _settings.NotifyUpdates, Enabled = _settings.CloseToTray, ForeColor = ForeColor, Margin = new Padding(0, 8, 0, 8) };
        var autoOption = new ReadableCheckBox { Name = "autoUpload", Text = "Auto-upload this setup's saved records", AutoSize = true, AutoCheck = false, Checked = _currentInstallation.AutoUpload, ForeColor = ForeColor, Margin = new Padding(0, 8, 0, 8) };
        var autoFeedback = Copy("Automatic uploads are optional, for this setup only. Posts and addon updates stay manual. Use the tray menu to Open or Exit.");
        autoFeedback.Name = "autoUploadFeedback";
        var applyingAuto = false;
        autoOption.Click += (_, _) =>
        {
            if (applyingAuto) return;
            // A modal confirmation runs a nested message loop. Commit the requested
            // preference, never a transient checkbox state after focus/activation changes.
            var requested = !_currentInstallation.AutoUpload;
            applyingAuto = true;
            try
            {
                if (requested && AutoUploadPrerequisiteError() is { } reason)
                { autoFeedback.Text = reason; return; }
                if (requested && !_confirmAutomaticUploads(dialog))
                { autoFeedback.Text = "Automatic uploads remain off. Nothing was changed."; return; }
                if (!SetAutoUploadPreference(requested))
                { autoFeedback.Text = AutoUploadPrerequisiteError() ?? "Automatic uploads could not be enabled. Check your selected file and account."; return; }
                autoFeedback.Text = requested
                    ? "Automatic uploads are on for this setup. Watching starts after you close App options. Posts and updates stay manual."
                    : "Automatic uploads are off for this setup.";
            }
            finally { autoOption.Checked = _currentInstallation.AutoUpload; applyingAuto = false; }
        };
        void Apply()
        {
            notifyOption.Enabled = trayOption.Checked;
            notifyOption.Text = "Notify me about app and addon updates" + (trayOption.Checked ? "" : "\nRequires tray mode");
            SetTrayPreferences(trayOption.Checked, notifyOption.Checked);
        }
        trayOption.CheckedChanged += (_, _) => Apply();
        notifyOption.CheckedChanged += (_, _) => Apply();
        panel.Controls.Add(Heading("APP OPTIONS"), 0, 0);
        panel.Controls.Add(trayOption, 0, 1); panel.Controls.Add(notifyOption, 0, 2);
        panel.Controls.Add(autoOption, 0, 3);
        panel.Controls.Add(autoFeedback, 0, 4);
        var close = new Button { Text = "Done", DialogResult = DialogResult.OK }; StyleButton(close);
        var diagnostics = new Button { Text = "Diagnostics" }; StyleButton(diagnostics);
        diagnostics.Click += (_, _) => { using var report = CreateDiagnosticsDialog(); report.ShowDialog(dialog); };
        panel.Controls.Add(Actions(close, diagnostics), 0, 5); dialog.AcceptButton = close; dialog.CancelButton = close;
        dialog.SetWindowContent(panel);
        return dialog;
    }

    private void HideToTray()
    {
        if (!_tray.Enabled || IsDisposed) return;
        if (WindowState != FormWindowState.Minimized) _restoreState = WindowState;
        Hide();
        ShowInTaskbar = false;
    }

    private void RestoreFromTray()
    {
        if (IsDisposed) return;
        ShowInTaskbar = true;
        WindowState = _restoreState;
        Show();
        Activate();
    }

    private void RequestExit()
    {
        if (_busy) { RestoreFromTray(); SetStatus("An operation is still running. Wait for it to finish, then Exit from the tray menu."); return; }
        _exitRequested = true;
        Close();
    }

    private async Task BusyAsync(string message, Func<CancellationToken, Task> work, DiagnosticOperation operation = DiagnosticOperation.General)
    {
        if (_busy || _closing || IsDisposed || Disposing) return;
        _busy = true;
        try
        {
            SetStatus(message);
            UseWaitCursor = true;
            _connect.Enabled = _install.Enabled = _upload.Enabled = _checkAddon.Enabled = false;
            _checkCompanion.Enabled = _companionUpdate.Enabled = false;
            _scan.Enabled = _historyButton.Enabled = false;
            _client.Enabled = _gameRoot.Enabled = _savedVariables.Enabled = _installations.Enabled = _manageSetups.Enabled = false;
            foreach (var browse in Controls.Find("browse", true)) browse.Enabled = false;
            await work(_lifetime.Token);
        }
        catch (Exception error)
        {
            _diagnostics.Record(operation, error);
            if (!_closing && !IsDisposed && !Disposing) SetStatus(error is OperationCanceledException ? "Operation cancelled." : error.Message);
        }
        finally
        {
            _busy = false;
            if (!_closing && !IsDisposed && !Disposing)
            {
                UseWaitCursor = false;
                _connect.Enabled = _install.Enabled = _checkAddon.Enabled = true;
                _checkCompanion.Enabled = _companionUpdate.Enabled = true;
                _scan.Enabled = _historyButton.Enabled = true;
                _client.Enabled = _gameRoot.Enabled = _savedVariables.Enabled = _installations.Enabled = _manageSetups.Enabled = true;
                foreach (var browse in Controls.Find("browse", true)) browse.Enabled = true;
                RefreshAccount();
            }
        }
    }

    private void SaveSettings()
    {
        CaptureInstallation();
        if (!_persistJournal) return;
        try { _settings.Save(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _diagnostics.Record(DiagnosticOperation.Settings, error);
            SetStatus("Could not save Companion settings: " + error.Message);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _closing = true;
            _autoCancellation?.Cancel();
            _lifetime.Cancel();
            _previewTimer.Dispose();
            _updateTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    private void SetStatus(string text) { if (!_closing && !IsDisposed && !Disposing) _status.Text = text; }
}
