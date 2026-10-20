using System.Runtime.InteropServices;

namespace RealmlyCompanion;

// Retain native text selection, wrapping and accessibility, but paint our own scrollbar.
internal sealed class RealmlyTextBox : TextBox
{
    private const int FirstVisibleLine = 0xCE, LineCount = 0xBA, LineScroll = 0xB6;
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    internal event Action? ScrollChanged;
    internal int Offset => IsHandleCreated ? (int)SendMessage(Handle, FirstVisibleLine, 0, 0) : 0;
    internal int TotalLines => IsHandleCreated ? (int)SendMessage(Handle, LineCount, 0, 0) : 1;
    internal int VisibleLines => Math.Max(1, ClientSize.Height / Math.Max(1, Font.Height));

    internal void ScrollTo(int line)
    {
        if (!IsHandleCreated) return;
        line = Math.Clamp(line, 0, Math.Max(0, TotalLines - VisibleLines));
        SendMessage(Handle, LineScroll, 0, line - Offset);
        ScrollChanged?.Invoke();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x20A) // Wheel scrolling must work even without a native scrollbar.
        {
            var delta = unchecked((short)((long)m.WParam >> 16));
            var lines = SystemInformation.MouseWheelScrollLines;
            ScrollTo(Offset - delta / 120 * (lines < 0 ? VisibleLines : lines));
            return;
        }
        base.WndProc(ref m);
        // Caret navigation/selection can scroll the native edit control too.
        if (m.Msg is 0x100 or 0x101 or 0x200 or 0x202 or 0x115 or 0x5 or 0xC or 0x30 or 0xB7 or 0xB1 or 0x113 or 0xF)
            ScrollChanged?.Invoke();
    }

    internal Control WithScrollBar()
    {
        var container = new Panel { Dock = DockStyle.Fill, BackColor = BackColor };
        var bar = new RealmlyScrollBar(ScrollTo, smallChange: 1) { Dock = DockStyle.Right,
            Width = Math.Max(12, 12 * DeviceDpi / 96), AccessibleName = "Scroll " + AccessibleName };
        Dock = DockStyle.Fill;
        ScrollBars = ScrollBars.None;
        container.Controls.Add(this);
        container.Controls.Add(bar);
        void RefreshRange() => bar.SetRange(TotalLines, VisibleLines, Offset);
        ScrollChanged += RefreshRange;
        HandleCreated += (_, _) => RefreshRange();
        SizeChanged += (_, _) => RefreshRange();
        TextChanged += (_, _) => RefreshRange();
        FontChanged += (_, _) => RefreshRange();
        container.DpiChangedAfterParent += (_, _) => bar.Width = Math.Max(12, 12 * DeviceDpi / 96);
        return container;
    }
}
