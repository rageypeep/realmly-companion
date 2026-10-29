using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace RealmlyCompanion;

internal sealed class ScreenshotManagerForm : RealmlyWindow
{
    private readonly RealmlyApi _api;
    private readonly string? _token;
    private readonly ClientKind _client;
    private readonly string _lua;
    private string _folder;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly DataGridView _grid = new();
    private readonly PictureBox _preview = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(8, 7, 5) };
    private readonly Label _status = new() { AutoSize = true, UseMnemonic = false, Dock = DockStyle.Fill };
    private readonly Label _details = new() { AutoSize = true, UseMnemonic = false, Dock = DockStyle.Fill };
    private readonly ComboBox _capture = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox _character = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox _day = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly Button _upload;
    private readonly Button _cancel;
    private readonly Func<string, bool> _confirmUpload;
    private CancellationTokenSource? _work;
    private CancellationToken WorkToken => _work?.Token ?? _lifetime.Token;
    private readonly List<Button> _buttons = [];
    private readonly HashSet<string> _uploaded = new(StringComparer.Ordinal);
    private ScreenshotCharacter[] _characters = [];
    private bool _busy, _changing;
    private int _previewVersion, _dayVersion;
    private readonly SemaphoreSlim _previewGate = new(1, 1);
    private LocalScreenshot? Selected => _grid.CurrentRow?.Tag as LocalScreenshot;

    public ScreenshotManagerForm(string gameRoot, ClientKind client, string lua, string? token, RealmlyApi api, Func<string, bool>? confirmUpload = null)
    {
        _confirmUpload = confirmUpload ?? (message => MessageBox.Show(this, message, "Review screenshot upload", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK);
        _api = api; _client = client; _lua = lua; _token = token; _folder = ScreenshotLibrary.Folder(gameRoot, client);
        Text = $"Realmly Screenshots / {client}"; Font = new Font("Segoe UI", 10);
        ForeColor = Color.FromArgb(239, 215, 164); MinimumSize = new Size(940, 680); ClientSize = new Size(1180, 820);
        StartPosition = FormStartPosition.CenterParent;
        using (var iconStream = typeof(CompanionForm).Assembly.GetManifestResourceStream("RealmlyCompanion.Icon.ico"))
            if (iconStream is not null) { using var icon = new Icon(iconStream); Icon = (Icon)icon.Clone(); }
        _preview.DoubleClick += (_, _) => { if (Selected is { } shot) { try { Process.Start(new ProcessStartInfo(shot.Path) { UseShellExecute = true }); } catch (Exception error) { _status.Text = error.Message; } } };
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(20) };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize)); body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (var i = 0; i < 3; i++) body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.Controls.Add(new Label { Text = $"YOUR VISUAL CHRONICLE / {client.ToString().ToUpperInvariant()}", AutoSize = true, Padding = new Padding(0, 0, 0, 12), Font = new Font(Font, FontStyle.Bold) }, 0, 0);
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        actions.Controls.Add(Action("Refresh", RefreshAsync));
        actions.Controls.Add(Action("Choose folder...", async () => { using var picker = new FolderBrowserDialog { SelectedPath = _folder, Description = "Choose this setup's screenshot folder" }; if (picker.ShowDialog(this) == DialogResult.OK) { _folder = picker.SelectedPath; await RefreshAsync(); } }));
        actions.Controls.Add(Action("Open folder", () => { if (Directory.Exists(_folder)) Process.Start(new ProcessStartInfo(_folder) { UseShellExecute = true }); return Task.CompletedTask; }));
        actions.Controls.Add(Action("Manage on Realmly", () => { Process.Start(new ProcessStartInfo("https://realmly.me/screenshots") { UseShellExecute = true }); return Task.CompletedTask; }));
        body.Controls.Add(actions, 0, 1);
        var split = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 10) };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48)); split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52)); split.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        ConfigureGrid(); split.Controls.Add(_grid, 0, 0);
        var right = new TableLayoutPanel { ColumnCount = 1, RowCount = 4, Dock = DockStyle.Fill, Padding = new Padding(12, 0, 0, 0) };
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (var i = 0; i < 3; i++) right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.Controls.Add(_preview, 0, 0); right.Controls.Add(_details, 0, 1);
        right.Controls.Add(new Label { Text = "Recorded capture (choose when ambiguous)", AutoSize = true, Padding = new Padding(0, 10, 0, 6) }, 0, 2);
        right.Controls.Add(_capture, 0, 3); split.Controls.Add(right, 1, 0); body.Controls.Add(split, 0, 2);
        var target = new TableLayoutPanel { ColumnCount = 2, RowCount = 2, Dock = DockStyle.Fill, AutoSize = true };
        target.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60)); target.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        target.Controls.Add(new Label { Text = "Destination for unmatched / plain images", AutoSize = true }, 0, 0);
        target.Controls.Add(new Label { Text = "Adventure day (UTC)", AutoSize = true }, 1, 0);
        target.Controls.Add(_character, 0, 1); target.Controls.Add(_day, 1, 1); body.Controls.Add(target, 0, 3);
        var bottom = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 5) };
        _upload = Action("Review & upload checked images privately", UploadAsync); _upload.Enabled = false; bottom.Controls.Add(_upload);
        _cancel = Action("Cancel operation", () => { _work?.Cancel(); return Task.CompletedTask; }); _buttons.Remove(_cancel); _cancel.Enabled = false; bottom.Controls.Add(_cancel);
        bottom.Controls.Add(Action("Close", () => { Close(); return Task.CompletedTask; })); body.Controls.Add(bottom, 0, 4); body.Controls.Add(_status, 0, 5);
        body.SizeChanged += (_, _) => _status.MaximumSize = new Size(Math.Max(100, body.ClientSize.Width - body.Padding.Horizontal), 0);
        right.SizeChanged += (_, _) => _details.MaximumSize = new Size(Math.Max(100, right.ClientSize.Width - right.Padding.Horizontal), 0);
        SetWindowContent(body);
        _grid.SelectionChanged += async (_, _) => await PreviewAsync();
        _capture.SelectedIndexChanged += (_, _) => { if (!_changing && Selected is { } shot) { shot.Capture = _capture.SelectedItem as ScreenshotCapture; UpdateRow(_grid.CurrentRow!); } };
        _character.SelectedIndexChanged += async (_, _) => await LoadDaysAsync();
        Shown += async (_, _) => await RefreshAsync();
        FormClosing += (_, e) => { if (_busy) { e.Cancel = true; _status.Text = "Wait for the current operation to finish before closing."; } };
        FormClosed += (_, _) => { _lifetime.Cancel(); _lifetime.Dispose(); _preview.Image?.Dispose(); };
    }
    private Button Action(string text, Func<Task> action)
    {
        var button = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(10, 7, 10, 7), Margin = new Padding(0, 0, 9, 5), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(45, 30, 17), ForeColor = ForeColor, UseMnemonic = false };
        button.FlatAppearance.BorderColor = Color.FromArgb(157, 112, 47);
        button.Click += async (_, _) => { try { await action(); } catch (Exception error) { if (!IsDisposed) _status.Text = error.Message; } };
        _buttons.Add(button); return button;
    }
    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill; _grid.AllowUserToAddRows = _grid.AllowUserToDeleteRows = false; _grid.RowHeadersVisible = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _grid.MultiSelect = false;
        _grid.BackgroundColor = Color.FromArgb(20, 16, 11); _grid.BorderStyle = BorderStyle.FixedSingle; _grid.EnableHeadersVisualStyles = false;
        _grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(25, 20, 14), ForeColor = ForeColor, SelectionBackColor = Color.FromArgb(80, 57, 26), SelectionForeColor = Color.FromArgb(255, 238, 192) };
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(42, 30, 16), ForeColor = ForeColor };
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Include", HeaderText = "Send", MinimumWidth = 64, FillWeight = 23 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "File", HeaderText = "Screenshot", ReadOnly = true, FillWeight = 95 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Match", HeaderText = "Recorded milestone", ReadOnly = true, FillWeight = 95 });
        _grid.CurrentCellDirtyStateChanged += (_, _) => { if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
    }
    private void Lock(bool value)
    {
        if (value) _work = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        else { _work?.Dispose(); _work = null; }
        _cancel.Enabled = value;
        _busy = value; foreach (var button in _buttons) button.Enabled = !value;
        _grid.Enabled = _capture.Enabled = _character.Enabled = _day.Enabled = !value; UseWaitCursor = value;
        if (!value) _upload.Enabled = !string.IsNullOrWhiteSpace(_token) && _characters.Length > 0;
    }
    private void UpdateRow(DataGridViewRow row)
    {
        var shot = (LocalScreenshot)row.Tag!;
        row.Cells[2].Value = shot.Uploaded ? "Uploaded this visit" : shot.Capture is not null ? $"{shot.Capture.CharacterName}: {shot.Capture.Title}" : shot.Matches.Length > 1 ? "Ambiguous / choose capture or plain" : "Plain image / choose destination";
    }
    private async Task RefreshAsync()
    {
        if (_busy) return; Lock(true); _status.Text = "Reading local screenshots and saved capture records...";
        try
        {
            string? warning = null;
            var files = await Task.Run(() => {
                ScreenshotCapture[] captures = [];
                try { if (File.Exists(_lua)) { if (new FileInfo(_lua).Length > 2 * 1024 * 1024) throw new InvalidOperationException("Realmly.lua exceeds 2 MB."); captures = ScreenshotLibrary.Captures(File.ReadAllText(_lua), _client); } else warning = "Save Realmly.lua after playing to match captures."; }
                catch (Exception error) { warning = "Capture matching unavailable: " + error.Message; }
                return ScreenshotLibrary.Scan(_folder, captures, WorkToken);
            }, WorkToken);
            if (IsDisposed) return;
            _grid.Rows.Clear();
            foreach (var file in files) { var index = _grid.Rows.Add(false, Path.GetFileName(file.Path), ""); _grid.Rows[index].Tag = file; UpdateRow(_grid.Rows[index]); }
            _characters = [];
            if (!string.IsNullOrWhiteSpace(_token))
            {
                try { _characters = (await _api.ScreenshotCharactersAsync(_token, WorkToken)).Characters.Where(c => string.Equals(c.GameVersion, _client.ToString(), StringComparison.OrdinalIgnoreCase)).ToArray(); }
                catch (Exception error) { warning = "Local browsing is available. " + error.Message; }
            }
            _character.Items.Clear(); _character.Items.AddRange(_characters.Cast<object>().ToArray());
            if (_characters.Length > 0) _character.SelectedIndex = 0;
            _status.Text = $"{files.Length} local images / {_client}. Originals stay on disk. " + (warning ?? (string.IsNullOrWhiteSpace(_token) ? "Connect your Realmly account in the main window to upload." : "Upload addon data first. Check images, review destinations, then upload privately. Six per day / 200 per character. JPG, PNG and BMP supported."));
            await PreviewAsync();
        }
        catch (Exception error) { _status.Text = error.Message; }
        finally { if (!IsDisposed) Lock(false); }
    }
    private async Task LoadDaysAsync()
    {
        var generation = ++_dayVersion; _day.Items.Clear();
        if (_character.SelectedItem is not ScreenshotCharacter c || string.IsNullOrWhiteSpace(_token)) return;
        try
        {
            var result = await _api.ScreenshotDaysAsync(c.Slug, _token, WorkToken);
            if (IsDisposed || generation != _dayVersion) return;
            _day.Items.AddRange(result.Days.Cast<object>().ToArray()); if (_day.Items.Count > 0) _day.SelectedIndex = 0;
        }
        catch (Exception error) { if (!IsDisposed && generation == _dayVersion) _status.Text = error.Message; }
    }
    private async Task PreviewAsync()
    {
        var generation = ++_previewVersion; var shot = Selected;
        _changing = true; _capture.Items.Clear(); _capture.Items.Add("No capture / plain image");
        if (shot is not null) _capture.Items.AddRange(shot.Matches.Cast<object>().ToArray());
        _capture.SelectedItem = (object?)shot?.Capture ?? _capture.Items[0]; _changing = false;
        var old = _preview.Image; _preview.Image = null; old?.Dispose();
        if (shot is null) { _details.Text = "Select a screenshot to preview it."; return; }
        _details.Text = Path.GetFileName(shot.Path) + Environment.NewLine + ((shot.Clock is { } clock ? clock.ToString("yyyy-MM-dd HH:mm:ss") + " (game local time)" : null) ?? "Renamed image / choose destination manually");
        try
        {
            await _previewGate.WaitAsync(WorkToken);
            try
            {
                if (IsDisposed || generation != _previewVersion) return;
                var image = await Task.Run(() => ScreenshotLibrary.Preview(ScreenshotLibrary.ReadImageBytes(shot.Path)), WorkToken);
                if (IsDisposed || generation != _previewVersion) { image.Dispose(); return; }
                old = _preview.Image; _preview.Image = image; old?.Dispose();
            }
            finally { _previewGate.Release(); }
        }
        catch (Exception error) { if (!IsDisposed && generation == _previewVersion) _details.Text += Environment.NewLine + error.Message; }
    }
    private async Task UploadAsync()
    {
        if (_busy || string.IsNullOrWhiteSpace(_token)) return;
        _grid.EndEdit();
        var selected = _grid.Rows.Cast<DataGridViewRow>().Where(r => r.Cells[0].Value is true && r.Tag is LocalScreenshot).ToArray();
        if (selected.Length == 0) { _status.Text = "Check the Send box beside the screenshots you want to upload."; return; }
        if (selected.Length > 6) { _status.Text = "Review at most six images per upload."; return; }
        var plan = new List<(DataGridViewRow Row, LocalScreenshot File, ScreenshotCharacter Character, string Day)>();
        foreach (var row in selected)
        {
            var file = (LocalScreenshot)row.Tag!;
            var character = file.Capture is { } capture ? _characters.SingleOrDefault(c => c.AdventurerId == capture.AdventurerId) : _character.SelectedItem as ScreenshotCharacter;
            var day = file.Capture?.Day ?? _day.SelectedItem as string;
            if (character is null || day is null) { _status.Text = "A destination is missing. Upload addon data to your connected account first, or choose a character and adventure day for plain images."; return; }
            plan.Add((row, file, character, day));
        }
        var summary = string.Join(Environment.NewLine, plan.Select(p => $"{Path.GetFileName(p.File.Path)} -> {p.Character.CharacterName} / {p.Day}" + (p.File.Capture is null ? " (plain image)" : $" / {p.File.Capture.Title}")));
        if (!_confirmUpload("Upload these images privately to Realmly for automated moderation?\n\n" + summary + "\n\nNothing is posted to the social feed. Original files stay untouched.")) return;
        Lock(true); var completed = 0;
        try
        {
            foreach (var item in plan)
            {
                _status.Text = $"Checking and uploading {completed + 1} of {plan.Count}...";
                var bytes = await Task.Run(() => ScreenshotLibrary.ReadImageBytes(item.File.Path), WorkToken);
                var key = Convert.ToHexString(SHA256.HashData(bytes.Concat(Encoding.UTF8.GetBytes(item.Character.AdventurerId + "\0" + item.Day)).ToArray()));
                if (_uploaded.Contains(key)) throw new InvalidOperationException("This image was already uploaded to this character/day during this visit. Manage it on Realmly.");
                string? milestoneId = null;
                if (item.File.Capture is { } capture)
                {
                    var matches = await _api.ScreenshotMatchesAsync(item.Character.Slug, item.Day, Path.GetFileName(item.File.Path), _token, WorkToken);
                    if (!matches.Matches.Any(c => c.Id == capture.Id)) throw new InvalidOperationException("The recorded capture is not available on Realmly yet. Upload the latest Realmly.lua first, then try again.");
                    milestoneId = capture.Id;
                }
                var image = await Task.Run(() => ScreenshotLibrary.UploadImage(bytes), WorkToken);
                await _api.UploadScreenshotAsync(item.Character.Slug, item.Day, image, milestoneId, _token, WorkToken);
                _uploaded.Add(key); item.File.Uploaded = true; item.Row.Cells[0].Value = false; UpdateRow(item.Row); completed++;
            }
            _status.Text = $"{completed} screenshot(s) saved privately. Open Manage on Realmly to preview badges, edit captions or share to your character's feed.";
        }
        catch (Exception error) { _status.Text = $"{completed} saved. {error.Message} Remaining images are still checked."; }
        finally { if (!IsDisposed) Lock(false); }
    }
}
