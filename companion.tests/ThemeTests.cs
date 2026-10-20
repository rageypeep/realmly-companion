using System.Reflection;
using RealmlyCompanion;

internal static class ThemeTests
{
    internal static void Run(CompanionForm owner, string output)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object?[] arguments = ["Upload history", string.Join("\n", Enumerable.Range(1, 100).Select(i => $"Record {i}: a long field note that wraps when the dialog gets narrower. Adventure records remain selectable and readable.")), null];
        using var dialog = (Form)typeof(CompanionForm).GetMethod("CreateTextDialog", flags)!.Invoke(owner, arguments)!;
        dialog.StartPosition = FormStartPosition.Manual;
        dialog.Location = new Point(-30000, -30000);
        dialog.Show();
        var text = Descendants(dialog).OfType<RealmlyTextBox>().Single();
        var bar = Descendants(dialog).OfType<RealmlyScrollBar>().Single();
        foreach (var width in new[] { 680, 540 })
        {
            dialog.ClientSize = new Size(width, 500);
            Application.DoEvents();
            if (text.ScrollBars != ScrollBars.None || !text.ReadOnly || bar.Maximum <= 0)
                throw new Exception("Dialog text/scrollbar setup failed");
            typeof(RealmlyScrollBar).GetMethod("OnKeyDown", flags)!.Invoke(bar, [new KeyEventArgs(Keys.End)]);
            if (text.Offset != bar.Maximum) throw new Exception($"Dialog End scroll failed: {text.Offset}/{bar.Maximum}");
            using var bitmap = new Bitmap(dialog.Width, dialog.Height);
            dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(Path.Combine(output, $"companion-history-scrolled-{width}.png"));
            typeof(RealmlyScrollBar).GetMethod("OnKeyDown", flags)!.Invoke(bar, [new KeyEventArgs(Keys.Home)]);
            if (text.Offset != 0) throw new Exception("Dialog Home scroll failed");
            typeof(RealmlyScrollBar).GetMethod("OnKeyDown", flags)!.Invoke(bar, [new KeyEventArgs(Keys.Down)]);
            if (text.Offset != 1) throw new Exception("Dialog arrow should scroll one line");
            typeof(RealmlyScrollBar).GetMethod("OnMouseWheel", flags)!.Invoke(bar, [new HandledMouseEventArgs(MouseButtons.None, 0, 0, 0, -120)]);
            if (text.Offset != 4) throw new Exception("Wheel over the scrollbar failed");
            // Exercise the actual native wheel message handler.
            object[] wheel = [Message.Create(text.Handle, 0x20A, (IntPtr)(-120 << 16), IntPtr.Zero)];
            typeof(RealmlyTextBox).GetMethod("WndProc", flags)!.Invoke(text, wheel);
            if (text.Offset <= 0) throw new Exception("Dialog mouse wheel failed");
            text.Focus(); text.Select(text.TextLength, 0); text.ScrollToCaret();
            Application.DoEvents();
            text.ScrollTo(0);
        }
        foreach (var enabled in new[] { true, false })
        foreach (var selected in new[] { true, false })
        {
            using var checkbox = new ReadableCheckBox { Text = "Themed option", Size = new Size(260, 40), Enabled = enabled, Checked = selected };
            using var bitmap = new Bitmap(260, 40);
            checkbox.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            if (bitmap.GetPixel(5, 15).ToArgb() == Color.White.ToArgb()) throw new Exception("Native white checkbox remains");
        }
        dialog.Close();
        Console.WriteLine("PASS themed modal scrollbars, resize, Home/End, native wheel, read-only text and checkbox states");
    }
    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
