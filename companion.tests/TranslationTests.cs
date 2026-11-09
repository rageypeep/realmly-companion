using System.Net;
using System.Reflection;
using System.Text.Json;
using RealmlyCompanion;

internal static class TranslationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly ChatTranslation Result = new("German", "Do you need help?", "Danke, gerade nicht.", "Thanks, not right now.", "");
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    private static T Field<T>(object instance, string field) => (T)instance.GetType().GetField(field, Private)!.GetValue(instance)!;
    private static IEnumerable<Control> Children(Control parent)
    {
        foreach (Control child in parent.Controls) { yield return child; foreach (var nested in Children(child)) yield return nested; }
    }
    private static void Pump(Task task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(5); }
        Assert(task.IsCompleted, "Translation test did not finish in time"); task.GetAwaiter().GetResult();
    }
    internal static void Run(string output)
    {
        Directory.CreateDirectory(output);
        using var handler = new Handler(); using var http = new HttpClient(handler) { BaseAddress = new Uri("https://realmly.me/") };
        var api = new RealmlyApi(http);
        string? copied = null;
        using var form = new CompanionForm(new CompanionSettings { AccessToken = "translation-fixture-token" }, api: api, copyToClipboard: value => copied = value);
        form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-30000, -30000); form.Show(); Application.DoEvents();
        var tabs = Children(form).OfType<RealmlyFeatureTabs>().Single();
        var panel = Children(form).OfType<TranslationPanel>().Single();
        Assert(tabs.SelectedIndex == 0 && !panel.Visible && handler.Requests == 0, "Translate must be a separate inactive tab with no automatic AI request");
        tabs.SelectFeature(1); Application.DoEvents();
        var message = Field<RealmlyTextBox>(panel, "_message"); var intent = Field<RealmlyTextBox>(panel, "_intent");
        var reply = Field<RealmlyTextBox>(panel, "_reply"); var english = Field<RealmlyTextBox>(panel, "_english");
        var replyEnglish = Field<RealmlyTextBox>(panel, "_replyEnglish");
        var mode = Field<ReadableCheckBox>(panel, "_includeReply"); var action = Field<Button>(panel, "_translate");
        Assert(mode.Checked && action.Text == "Translate & reply" && action.Height >= 50 && action.Width >= 260, "Preserve reply mode and a large action button");
        message.Text = "Brauchst du Hilfe?"; intent.Text = "Politely decline";
        Assert(handler.Requests == 0 && message.MaxLength == 3000 && intent.MaxLength == 500, "Typing must not send any text");
        Pump(panel.TranslateAsync());
        Assert(handler.Requests == 1 && handler.LastMessage == message.Text && handler.LastIntent == intent.Text, "Send only pasted message and intent");
        Assert(reply.Text == Result.SuggestedReply && replyEnglish.Text == Result.SuggestedReplyEnglish && english.Text == Result.TranslationEnglish, "Translation result fields");
        Assert(reply.ReadOnly && english.ReadOnly && replyEnglish.ReadOnly, "Results must remain selectable, read-only text");
        Assert(Field<RealmlyScrollPanel>(panel, "_scroll").Offset > 0, "Show the results after translation without requiring manual scrolling");
        Field<Button>(panel, "_copyReply").PerformClick(); Assert(copied == Result.SuggestedReply, "Copy only their-language reply");
        Field<Button>(panel, "_copyEnglish").PerformClick(); Assert(copied == Result.SuggestedReplyEnglish, "Copy English counterpart");
        foreach (var size in new[] { new Size(900, 830), new Size(780, 700) })
        {
            form.ClientSize = size; Application.DoEvents();
            var scroll = Field<RealmlyScrollPanel>(panel, "_scroll");
            scroll.ScrollTo(0); Application.DoEvents();
            Assert(scroll.ContentWidth <= scroll.ViewportWidth && panel.Width > 0, "Translation panel must fit its themed viewport");
            foreach (var box in new[] { message, intent, reply, english, replyEnglish })
                Assert(box.BackColor.ToArgb() != Color.White.ToArgb() && box.ScrollBars == ScrollBars.None, "Native white text area or scrollbar");
            using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(Path.Combine(output, $"translate-{size.Width}.png"));
            scroll.ScrollTo(int.MaxValue); Application.DoEvents();
            using var lower = new Bitmap(form.Width, form.Height); form.DrawToBitmap(lower, new Rectangle(Point.Empty, lower.Size));
            lower.Save(Path.Combine(output, $"translate-results-{size.Width}.png"));
        }
        mode.Checked = false; Application.DoEvents();
        Assert(action.Text == "Translate" && reply.Text.Length == 0 && !Field<Control>(panel, "_replies").Visible
            && !Field<Control>(panel, "_intentArea").Visible, "Translate-only hides and clears reply results and guidance");
        Pump(panel.TranslateAsync());
        Assert(!handler.LastIncludeReply && handler.LastIntent == "" && english.Text == Result.TranslationEnglish
            && reply.Text == "" && replyEnglish.Text == "" && !Field<Button>(panel, "_copyReply").Enabled,
            "Translation only accepts empty replies and sends no hidden guidance");
        Field<RealmlyScrollPanel>(panel, "_scroll").ScrollTo(0); Application.DoEvents();
        using (var only = new Bitmap(form.Width, form.Height))
        { form.DrawToBitmap(only, new Rectangle(Point.Empty, only.Size)); only.Save(Path.Combine(output, "translate-only.png")); }
        Assert(action.Height >= 50 && action.Width >= 260, "Translate-only button stays large");
        mode.Checked = true; Application.DoEvents();
        Assert(action.Text == "Translate & reply" && english.Text == "" && Field<Control>(panel, "_replies").Visible, "Switching modes invalidates old results");
        // Changing input invalidates an old reply immediately.
        intent.Text = "Another intention"; Assert(reply.Text.Length == 0 && !Field<Button>(panel, "_copyReply").Enabled, "Stale reply must not remain copyable after editing input");
        handler.Delay = true;
        var delayed = panel.TranslateAsync(); Application.DoEvents();
        Assert(!mode.Enabled && message.ReadOnly && !Field<Button>(panel, "_pasteMessage").Enabled && Field<Button>(panel, "_cancel").Enabled, "Busy controls must keep request input stable");
        panel.Clear(); handler.Finish(); Pump(delayed);
        Assert(message.Text.Length == 0 && intent.Text.Length == 0 && reply.Text.Length == 0, "Clear must cancel and suppress a late result");
        handler.Delay = false; handler.Invalid = true; message.Text = "Hallo"; Pump(panel.TranslateAsync());
        Assert(reply.Text.Length == 0 && Field<Label>(panel, "_status").Text.Contains("could not be read"), "Malformed responses must not crash or expose old output");
        tabs.SelectFeature(0); Assert(!panel.Visible && tabs.SelectedIndex == 0, "Return to independent Realmly tools");
        Assert(!JsonSerializer.Serialize(new CompanionSettings()).Contains("Brauchst"), "Translation input must never become settings/history");
        form.Close();
        Console.WriteLine("PASS independent themed Translate tab, manual authenticated request/consent, exact reply copies, input limits, resize/scrollbars, stale-result prevention, busy/cancel/Clear and malformed responses");
    }
    private sealed class Handler : HttpMessageHandler
    {
        internal int Requests; internal string? LastMessage, LastIntent; internal bool Delay, Invalid, LastIncludeReply;
        private TaskCompletionSource? _pending;
        internal void Finish() => _pending?.TrySetResult();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert(request.RequestUri!.AbsolutePath == "/api/companion/translate", "Translation must not use an upload/chronicle endpoint");
            Assert(request.Headers.Authorization?.Parameter == "translation-fixture-token", "Translation requires the existing paired account token");
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Assert(body.RootElement.EnumerateObject().Select(item => item.Name).Order().SequenceEqual(new[] { "consent", "includeReply", "message", "replyIntent" }), "Do not send character/setup/file data");
            Assert(body.RootElement.GetProperty("consent").GetBoolean(), "Button click supplies explicit send intent");
            LastIncludeReply = body.RootElement.GetProperty("includeReply").GetBoolean();
            LastMessage = body.RootElement.GetProperty("message").GetString(); LastIntent = body.RootElement.GetProperty("replyIntent").GetString(); Requests++;
            if (Delay) { _pending = new TaskCompletionSource(); await _pending.Task; }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Invalid ? "not JSON" : JsonSerializer.Serialize(new { translation = LastIncludeReply ? Result : Result with { SuggestedReply = "", SuggestedReplyEnglish = "" } })) };
        }
    }
}
