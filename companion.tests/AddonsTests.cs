using System.Reflection;
using RealmlyCompanion;

internal static class AddonsTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    private static IEnumerable<Control> Children(Control parent)
    {
        foreach (Control child in parent.Controls) { yield return child; foreach (var nested in Children(child)) yield return nested; }
    }
    internal static void Run(string output)
    {
        Directory.CreateDirectory(output);
        using var form = new CompanionForm(new CompanionSettings { GameRoot = Path.Combine(output, "no-install-selected") });
        form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-30000, -30000);
        form.Show(); Application.DoEvents();
        var tabs = Children(form).OfType<RealmlyFeatureTabs>().Single();
        var panel = Children(form).OfType<AddonsPanel>().Single();
        Assert(tabs.SelectedIndex == 0 && !panel.Visible, "No automatic download or startup change");
        var buttons = (Button[])typeof(RealmlyFeatureTabs).GetField("_tabs", Private)!.GetValue(tabs)!;
        Assert(buttons.Select(b => b.Text).SequenceEqual(new[] { "Realmly", "Translate", "Add-ons", "Discord" }), "Third tab follows Translate");
        tabs.SelectFeature(2); Application.DoEvents(); Assert(panel.Visible, "Browsing never opens a download");
        var actions = Children(panel).OfType<Button>().ToArray();
        Assert(!actions.Any(b => b.Text.StartsWith("Download ")), "No manual download buttons");
        Assert(actions.Count(b => b.AccessibleName?.StartsWith("Install or update ") == true) == 2, "Keep native install/update controls for both add-ons");
        Assert(!Children(panel).OfType<Label>().Any(l => l.Text.Contains("Realmly Gathering")), "Old product name must not appear");
        Assert(Children(panel).OfType<Label>().Any(l => l.Text.Contains("not Retail")), "Compatibility must be clear");
        foreach (var size in new[] { new Size(900, 830), new Size(780, 700) })
        {
            form.ClientSize = size; Application.DoEvents();
            var scroll = Children(panel).OfType<RealmlyScrollPanel>().Single();
            Assert(scroll.ContentWidth <= scroll.ViewportWidth, "Add-on cards must fit the viewport");
            foreach (var bottom in new[] { false, true })
            {
                scroll.ScrollTo(bottom ? int.MaxValue : 0); Application.DoEvents();
                var content = (Control)typeof(RealmlyScrollPanel).GetField("_content", Private)!.GetValue(scroll)!;
                Assert(Math.Abs(content.Height - content.GetPreferredSize(new Size(content.Width, 0)).Height) <= 2,
                    "Wrapped content must settle without a blank scroll region");
                File.WriteAllText(Path.Combine(output, $"addons-{size.Width}-{bottom}-bounds.txt"),
                    $"offset={scroll.Offset} max={scroll.MaximumOffset} content={content.Bounds} preferred={content.GetPreferredSize(new Size(content.Width, 0))}\n"
                    + string.Join("\n", content.Controls.Cast<Control>().Select(c => $"{c.GetType().Name} {c.Text} {c.Bounds} preferred={c.GetPreferredSize(new Size(content.Width, 0))}")));
                using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                image.Save(Path.Combine(output, $"addons-{size.Width}-{(bottom ? "bottom" : "top")}.png"));
            }
            var sparkles = Children(panel).OfType<TableLayoutPanel>().Single(c => c.AccessibleName == "Sparkles");
            scroll.ScrollTo(Math.Max(0, sparkles.Top - 12)); Application.DoEvents();
            using var cardImage = new Bitmap(form.Width, form.Height); form.DrawToBitmap(cardImage, new Rectangle(Point.Empty, cardImage.Size));
            cardImage.Save(Path.Combine(output, $"sparkles-{size.Width}.png"));
        }
        typeof(Control).GetMethod("OnKeyDown", Private)!.Invoke(buttons[2], [new KeyEventArgs(Keys.Left)]);
        Assert(tabs.SelectedIndex == 1, "Left arrow chooses the preceding tab");
        typeof(Control).GetMethod("OnKeyDown", Private)!.Invoke(buttons[1], [new KeyEventArgs(Keys.Right)]);
        Assert(tabs.SelectedIndex == 2, "Right arrow chooses the next tab");
        tabs.SelectFeature(0); Assert(!panel.Visible, "Return to the main tools");
        form.Close();
        Console.WriteLine("PASS Add-ons tab, short names, native install/update controls without manual download buttons, Forever labels, keyboard navigation and responsive cards");
    }
}
