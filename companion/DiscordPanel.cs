using System.ComponentModel;
using System.Diagnostics;

namespace RealmlyCompanion;

internal sealed class DiscordPanel : UserControl
{
    internal const string InviteUrl = "https://discord.gg/Js2mcA7w9";
    private readonly Label _status = Copy("The invite opens in your browser. You choose whether to join in Discord.");

    internal DiscordPanel(Action<string>? open = null)
    {
        open ??= url => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        BackColor = Color.FromArgb(15, 11, 9); ForeColor = Color.FromArgb(245, 229, 184);
        Font = new Font("Segoe UI", 10F); AccessibleName = "Realmly Discord community";
        var scroll = new RealmlyScrollPanel { Dock = DockStyle.Fill };
        var content = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 1,
            Padding = new Padding(24, 24, 24, 24), BackColor = BackColor };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Add(Control control)
        {
            var row = content.RowCount++; content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right; content.Controls.Add(control, 0, row);
        }
        Add(Copy("REALMLY COMMUNITY / A PLACE BY THE HEARTH", true));
        var title = Copy("The tavern is open."); title.Font = new Font("Georgia", 25F); Add(title);
        Add(Copy("Pull up a chair. Meet fellow adventurers, share your stories and help shape Realmly. Whether you roam Forever or Retail, you're welcome here."));
        Add(Copy("JOIN THE DISCORD", true));
        var join = new Button { Text = "Join the Discord", AccessibleName = "Join the Realmly Discord",
            AccessibleDescription = "Opens the Realmly invite in your browser. Joining is your choice in Discord." };
        CompanionForm.StyleButton(join);
        void SizeJoin() => join.MinimumSize = new Size(260 * DeviceDpi / 96, 54 * DeviceDpi / 96);
        join.FontChanged += (_, _) => SizeJoin();
        VisibleChanged += (_, _) => { if (Visible) SizeJoin(); };
        SizeJoin();
        join.Click += (_, _) =>
        {
            try { open(InviteUrl); _status.Text = "Discord opened in your browser. Join there when you're ready."; }
            catch (Exception error) when (error is Win32Exception or InvalidOperationException)
            { _status.Text = "Couldn't open your browser. Copy the invite below and open it yourself."; }
        };
        Add(join); join.Anchor = AnchorStyles.Left;
        var inviteBorder = new Panel { Height = 40, Padding = new Padding(9), Margin = new Padding(0, 12, 0, 16), BackColor = Color.FromArgb(32, 23, 15) };
        inviteBorder.Controls.Add(new RealmlyTextBox { Text = InviteUrl, ReadOnly = true, ShortcutsEnabled = true,
            BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, BackColor = inviteBorder.BackColor,
            ForeColor = Color.FromArgb(245, 229, 184), AccessibleName = "Discord invite link" });
        Add(inviteBorder);
        Add(Copy("ADVENTURE TOGETHER", true));
        Add(Copy("Share chronicles, screenshots and those unexpected moments that make a character's journey their own."));
        Add(Copy("A LITTLE HELP ALONG THE WAY", true));
        Add(Copy("Ask about Realmly, get help with the Companion or add-ons, and report bugs. Discord is the best way to contact the project."));
        Add(Copy("HELP WRITE THE NEXT CHAPTER", true));
        Add(Copy("Suggest features, compare notes and join testing. A friendly community for both WoW Forever and Retail."));
        Add(_status);
        content.SizeChanged += (_, _) =>
        {
            foreach (var label in content.Controls.OfType<Label>())
                label.MaximumSize = new Size(Math.Max(1, content.ClientSize.Width - content.Padding.Horizontal), 0);
            scroll.PerformLayout();
        };
        scroll.SetContent(content); Controls.Add(scroll);
    }
    private static Label Copy(string text, bool heading = false) => new() { Text = text, AutoSize = true,
        UseMnemonic = false, Margin = new Padding(0, 0, 0, 14), MaximumSize = new Size(680, 0),
        ForeColor = heading ? Color.FromArgb(216, 168, 79) : Color.FromArgb(195, 176, 143),
        Font = new Font("Segoe UI", heading ? 9F : 10F, heading ? FontStyle.Bold : FontStyle.Regular) };
}
