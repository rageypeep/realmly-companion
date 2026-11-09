using System.ComponentModel;
using System.Reflection;
using RealmlyCompanion;

internal static class DiscordTests
{
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    private static IEnumerable<Control> Children(Control parent)
    {
        foreach (Control child in parent.Controls) { yield return child; foreach (var nested in Children(child)) yield return nested; }
    }
    internal static void Run(string output)
    {
        Directory.CreateDirectory(output); var opened = new List<string>();
        using var form = new CompanionForm(new CompanionSettings(), openDiscord: opened.Add);
        form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-30000, -30000); form.Show(); Application.DoEvents();
        var tabs = Children(form).OfType<RealmlyFeatureTabs>().Single();
        var panel = Children(form).OfType<DiscordPanel>().Single();
        Assert(tabs.SelectedIndex == 0 && !panel.Visible && opened.Count == 0, "Discord must not open automatically");
        tabs.SelectFeature(3); Application.DoEvents(); Assert(panel.Visible && opened.Count == 0, "Tab selection does not join or launch Discord");
        var join = Children(panel).OfType<Button>().Single();
        Assert(join.Text == "Join the Discord" && join.Width >= 260 && join.Height >= 54, "Large accessible join button");
        join.PerformClick(); Assert(opened.SequenceEqual(new[] { "https://discord.gg/Js2mcA7w9" }), "Use only the user's invite");
        var link = Children(panel).OfType<RealmlyTextBox>().Single();
        Assert(link.Text == DiscordPanel.InviteUrl && link.ReadOnly && link.ShortcutsEnabled && link.BackColor != Color.White, "Selectable themed invite link");
        foreach (var size in new[] { new Size(900, 830), new Size(780, 700) })
        {
            form.ClientSize = size; Application.DoEvents();
            var scroll = Children(panel).OfType<RealmlyScrollPanel>().Single();
            Assert(scroll.ContentWidth <= scroll.ViewportWidth, "Discord page fits its viewport");
            scroll.ScrollTo(0);
            using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
            image.Save(Path.Combine(output, $"discord-{size.Width}.png"));
        }
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var buttons = (Button[])typeof(RealmlyFeatureTabs).GetField("_tabs", flags)!.GetValue(tabs)!;
        typeof(Control).GetMethod("OnKeyDown", flags)!.Invoke(buttons[3], [new KeyEventArgs(Keys.Right)]);
        Assert(tabs.SelectedIndex == 0, "Right arrow wraps from Discord to Realmly");
        typeof(Control).GetMethod("OnKeyDown", flags)!.Invoke(buttons[0], [new KeyEventArgs(Keys.Left)]);
        Assert(tabs.SelectedIndex == 3, "Left arrow wraps to Discord");
        using var failed = new DiscordPanel(_ => throw new Win32Exception());
        using var host = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000) };
        failed.Dock = DockStyle.Fill; host.Controls.Add(failed); host.Show();
        Children(failed).OfType<Button>().Single().PerformClick();
        Assert(Children(failed).OfType<Label>().Any(label => label.Text.Contains("Couldn't open your browser")), "Browser failure leaves the invite available");
        host.Close(); form.Close();
        Console.WriteLine("PASS Discord tab: explicit invite launch, no automatic join, accessible themed link/button, narrow layouts, errors and four-tab keyboard navigation");
    }
}
