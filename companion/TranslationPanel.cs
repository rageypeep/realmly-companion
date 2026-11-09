using System.Runtime.InteropServices;

namespace RealmlyCompanion;

internal sealed class TranslationPanel : UserControl
{
    private readonly RealmlyApi _api;
    private readonly Func<string?> _token;
    private readonly Action<string> _copy;
    private readonly Func<string> _paste;
    private readonly RealmlyTextBox _message = Input("Message to translate", 3000);
    private readonly RealmlyTextBox _intent = Input("Optional reply guidance", 500);
    private readonly RealmlyTextBox _english = Output("English translation");
    private readonly RealmlyTextBox _reply = Output("Suggested reply in the original language");
    private readonly RealmlyTextBox _replyEnglish = Output("Suggested reply in English");
    private readonly Label _language = Label("DETECTED LANGUAGE / —", true);
    private readonly Label _account = Label("");
    private readonly Label _status = Label("Paste a message to begin.");
    private readonly Label _notes = Label("");
    private readonly Button _translate = ActionButton("Translate & reply");
    private readonly ReadableCheckBox _includeReply = new() { Text = "Suggest a reply", Checked = true, AutoSize = true,
        AccessibleName = "Suggest a reply", Margin = new Padding(0, 12, 12, 2), ForeColor = Color.FromArgb(245, 229, 184) };
    private readonly Control _intentHeading, _intentArea, _replies;
    private readonly Button _cancel = ActionButton("Cancel");
    private readonly Button _pasteMessage = ActionButton("Paste message");
    private readonly Button _copyReply = ActionButton("Copy reply");
    private readonly Button _copyEnglish = ActionButton("Copy English reply");
    private readonly RealmlyScrollPanel _scroll = new() { Dock = DockStyle.Fill };
    private CancellationTokenSource? _work;
    private int _version;
    private bool _busy, _connected;

    internal TranslationPanel(RealmlyApi api, Func<string?> token, Action<string>? copy = null, Func<string>? paste = null)
    {
        _api = api; _token = token; _copy = copy ?? Clipboard.SetText; _paste = paste ?? Clipboard.GetText;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(15, 11, 9); ForeColor = Color.FromArgb(245, 229, 184);
        Font = new Font("Segoe UI", 10F); AccessibleName = "Translate messages";
        var content = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1,
            Padding = new Padding(24, 18, 24, 18), BackColor = BackColor };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Add(Control control)
        {
            var row = content.RowCount++;
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            content.Controls.Add(control, 0, row);
        }
        Add(Label("REALMLY TRANSLATE / A FEW WORDS BETWEEN ADVENTURERS", true));
        var title = Label("Find the words."); title.Font = new Font("Georgia", 22F); Add(title);
        Add(Label("Understand a message in English. Add a suggested reply when you want one."));
        Add(_account);
        Add(Label("PASTE A MESSAGE  /  UP TO 3,000 CHARACTERS", true));
        Add(TextArea(_message, 112));
        _intentHeading = Label("WHAT WOULD YOU LIKE TO SAY?  /  OPTIONAL, UP TO 500 CHARACTERS", true); Add(_intentHeading);
        _intent.PlaceholderText = "For example: Thank them, but explain I'm busy right now.";
        _intentArea = TextArea(_intent, 58); Add(_intentArea);
        Add(Label("Clicking Translate sends this message to OpenAI through Realmly. Reply guidance is sent only when Suggest a reply is on. Text stays separate from profiles, uploads and chronicles; it is not saved in Companion history. Review the AI's wording before copying."));

        var clear = ActionButton("Clear");
        _pasteMessage.Click += (_, _) =>
        {
            try
            {
                var value = _paste();
                if (value.Length > 3000) { _status.Text = "The clipboard text is too long. Paste up to 3,000 characters."; return; }
                _message.Text = value; _message.Focus();
            }
            catch (ExternalException) { _status.Text = "The clipboard is busy. Try Ctrl+V in the message box."; }
        };
        clear.Click += (_, _) => Clear();
        _translate.Click += async (_, _) => await TranslateAsync();
        _cancel.Click += (_, _) => _work?.Cancel();
        _copyReply.Click += (_, _) => Copy(_reply.Text);
        _copyEnglish.Click += (_, _) => Copy(_replyEnglish.Text);
        Add(Actions(_translate, _includeReply));
        Add(Actions(_pasteMessage, _cancel, clear));
        Add(_status); Add(_language);
        Add(Label("ENGLISH TRANSLATION", true)); Add(TextArea(_english, 100));
        var replies = new TableLayoutPanel { ColumnCount = 2, RowCount = 3, AutoSize = true, Dock = DockStyle.Top };
        replies.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); replies.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        replies.Controls.Add(Label("SUGGESTED REPLY / THEIR LANGUAGE", true), 0, 0);
        replies.Controls.Add(Label("SUGGESTED REPLY / ENGLISH", true), 1, 0);
        replies.Controls.Add(TextArea(_reply, 120), 0, 1); replies.Controls.Add(TextArea(_replyEnglish, 120), 1, 1);
        replies.Controls.Add(_copyReply, 0, 2); replies.Controls.Add(_copyEnglish, 1, 2);
        _replies = replies; Add(replies); Add(_notes);
        _includeReply.CheckedChanged += (_, _) =>
        {
            _version++; _work?.Cancel(); ClearResults();
            _intentHeading.Visible = _intentArea.Visible = _replies.Visible = _includeReply.Checked;
            InputsChanged();
        };
        _scroll.SetContent(content); Controls.Add(_scroll);
        _message.TextChanged += (_, _) => InputsChanged();
        _intent.TextChanged += (_, _) => InputsChanged();
        VisibleChanged += (_, _) => { if (Visible) Availability(refreshConnection: true); };
        content.SizeChanged += (_, _) =>
        {
            foreach (var label in content.Controls.OfType<Label>())
                label.MaximumSize = new Size(Math.Max(1, content.ClientSize.Width - content.Padding.Horizontal), 0);
            _scroll.PerformLayout();
        };
        Availability(refreshConnection: true);
    }

    private static RealmlyTextBox Input(string name, int limit) => new() { Multiline = true, WordWrap = true,
        MaxLength = limit, BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(24, 18, 13),
        ForeColor = Color.FromArgb(245, 229, 184), AccessibleName = name, ShortcutsEnabled = true };
    private static RealmlyTextBox Output(string name)
    {
        var box = Input(name, 0); box.ReadOnly = true; return box;
    }
    private static Label Label(string value, bool heading = false) => new() { Text = value, AutoSize = true,
        UseMnemonic = false, Margin = new Padding(0, 0, 0, 8), MaximumSize = new Size(760, 0),
        ForeColor = heading ? Color.FromArgb(216, 168, 79) : Color.FromArgb(195, 176, 143),
        Font = new Font("Segoe UI", heading ? 9F : 10F, heading ? FontStyle.Bold : FontStyle.Regular) };
    private static Button ActionButton(string value)
    {
        var button = new TranslationButton { Text = value };
        CompanionForm.StyleButton(button, compact: true); return button;
    }
    private sealed class TranslationButton : Button
    {
        protected override void OnPaint(PaintEventArgs args)
        {
            if (Enabled) { base.OnPaint(args); return; }
            args.Graphics.Clear(BackColor);
            using var border = new Pen(Color.FromArgb(100, 73, 33));
            args.Graphics.DrawRectangle(border, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
            TextRenderer.DrawText(args.Graphics, Text, Font, ClientRectangle, Color.FromArgb(175, 150, 106),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    private static Control Actions(params Control[] buttons)
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        panel.Controls.AddRange(buttons); return panel;
    }
    private static Control TextArea(RealmlyTextBox box, int height)
    {
        var border = new Panel { Dock = DockStyle.Top, Height = height, Padding = new Padding(1),
            Margin = new Padding(0, 0, 10, 10), BackColor = Color.FromArgb(115, 80, 34) };
        border.Controls.Add(box.WithScrollBar()); return border;
    }
    private void InputsChanged()
    {
        if (!_busy) { ClearResults(); _status.Text = "Ready when you are. Click Translate to send this text."; }
        Availability();
    }
    private void ClearResults()
    {
        _english.Clear(); _reply.Clear(); _replyEnglish.Clear(); _notes.Text = "";
        _language.Text = "DETECTED LANGUAGE / —"; _copyReply.Enabled = _copyEnglish.Enabled = false;
    }
    private void Availability(bool refreshConnection = false)
    {
        if (refreshConnection) _connected = !string.IsNullOrWhiteSpace(_token());
        _account.Text = _connected ? "Using your connected Realmly account. No character or game setup needed."
            : "Connect your account in the Realmly tab to use Translate.";
        _translate.Enabled = !_busy && _connected && !string.IsNullOrWhiteSpace(_message.Text);
        _translate.Text = _busy ? "Translating..." : _includeReply.Checked ? "Translate & reply" : "Translate";
        _translate.MinimumSize = new Size(Math.Max(260 * DeviceDpi / 96, _translate.MinimumSize.Width), 50 * DeviceDpi / 96);
        _includeReply.Enabled = !_busy;
        _pasteMessage.Enabled = !_busy; _cancel.Enabled = _busy; _message.ReadOnly = _busy; _intent.ReadOnly = _busy || !_includeReply.Checked;
    }
    internal async Task TranslateAsync()
    {
        if (_busy || IsDisposed) return;
        var token = _token();
        if (string.IsNullOrWhiteSpace(token)) { _status.Text = "Connect your account in the Realmly tab first."; return; }
        var includeReply = _includeReply.Checked;
        var message = _message.Text.Trim(); var intent = includeReply ? _intent.Text.Trim() : "";
        if (message.Length is 0 or > 3000 || intent.Length > 500) { _status.Text = "Use up to 3,000 message characters and 500 reply-guidance characters."; return; }
        var version = ++_version; var showResult = false;
        using var work = new CancellationTokenSource(); _work = work; _busy = true; ClearResults(); Availability();
        _status.Text = includeReply ? "Translating and finding a reply..." : "Translating your message..."; UseWaitCursor = true;
        try
        {
            var result = await _api.TranslateAsync(message, intent, token, work.Token, includeReply);
            if (IsDisposed || version != _version || work.IsCancellationRequested) return;
            _english.Text = result.TranslationEnglish; _reply.Text = result.SuggestedReply; _replyEnglish.Text = result.SuggestedReplyEnglish;
            _language.Text = "DETECTED LANGUAGE / " + result.DetectedLanguage;
            _notes.Text = result.Notes.Length == 0 ? "" : "AI note: " + result.Notes;
            _copyReply.Enabled = _copyEnglish.Enabled = includeReply;
            _status.Text = includeReply ? "Ready. Check both versions, then copy the reply you want to send." : "Ready. Your English translation is below. No reply was generated.";
            showResult = true;
        }
        catch (OperationCanceledException) { if (!IsDisposed && version == _version) _status.Text = "Translation cancelled or timed out. Your pasted text is still here."; }
        catch (RealmlyApiException error) { if (!IsDisposed && version == _version) _status.Text = error.Message; }
        catch (HttpRequestException) { if (!IsDisposed && version == _version) _status.Text = "Couldn't reach Realmly. Check your connection and try again."; }
        catch (InvalidOperationException) { if (!IsDisposed && version == _version) _status.Text = "The translation response could not be read. Please try again."; }
        finally
        {
            if (ReferenceEquals(_work, work))
            {
                _work = null; _busy = false;
                if (!IsDisposed)
                {
                    UseWaitCursor = false; Availability();
                    if (showResult) { _scroll.PerformLayout(); _scroll.ScrollTo(Math.Max(0, _language.Top - 12)); }
                }
            }
        }
    }
    internal void Clear()
    {
        _version++; _work?.Cancel(); _work = null; _busy = false; UseWaitCursor = false;
        _message.Clear(); _intent.Clear(); ClearResults(); _status.Text = "Cleared. Paste a message to begin."; Availability(); _scroll.ScrollTo(0);
    }
    private void Copy(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        try { _copy(value); _status.Text = "Reply copied. Paste it into the game when you're ready."; }
        catch (ExternalException) { _status.Text = "The clipboard is busy. Select the reply and press Ctrl+C."; }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { _version++; _work?.Cancel(); }
        base.Dispose(disposing);
    }
}
