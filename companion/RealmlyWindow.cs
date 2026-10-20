using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace RealmlyCompanion;

internal class RealmlyWindow : Form
{
    private readonly RealmlyTitleBar _titleBar;
    private Control? _body;
    private ContextMenuStrip? _windowMenu;

    public RealmlyWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(15, 11, 9);
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        _titleBar = new RealmlyTitleBar(this);
        Controls.Add(_titleBar);
    }

    protected internal void SetWindowContent(Control content)
    {
        _body = content;
        content.Dock = DockStyle.None;
        Controls.Add(content);
        _titleBar.BringToFront();
        PerformLayout();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            // Retain the Windows sizing/system-menu/minimize/maximize behaviours.
            parameters.Style |= 0x00040000 | 0x00080000 | 0x00020000 | 0x00010000;
            return parameters;
        }
    }

    protected override void SetClientSizeCore(int width, int height)
    {
        // NCCALCSIZE makes the entire window client-owned. WinForms otherwise
        // adds an invisible WS_THICKFRAME allowance and caches a smaller client
        // size, leaving an unpainted strip outside our layout/border.
        SetBoundsCore(Left, Top, width, height, BoundsSpecified.Size);
        UpdateBounds(Left, Top, Width, Height, Width, Height);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (_titleBar is null) return;
        // Leave a real grip around child controls so they cannot swallow edge hit tests.
        var inset = WindowState == FormWindowState.Maximized ? 1 : Math.Max(6, 6 * DeviceDpi / 96);
        _titleBar.SetBounds(inset, inset, Math.Max(0, ClientSize.Width - inset * 2), 46 * DeviceDpi / 96);
        _body?.SetBounds(inset, _titleBar.Bottom, Math.Max(0, ClientSize.Width - inset * 2), Math.Max(0, ClientSize.Height - _titleBar.Bottom - inset));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var border = new Pen(Color.FromArgb(125, 89, 39));
        e.Graphics.DrawRectangle(border, 0, 0, Math.Max(0, ClientSize.Width - 1), Math.Max(0, ClientSize.Height - 1));
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        DisableNativeFrame();
    }

    private void DisableNativeFrame()
    {
        // WS_THICKFRAME is kept for resizing, but Windows must not render it.
        var disabled = 1; // DWMNCRP_DISABLED / DWMWA_NCRENDERING_POLICY
        DwmSetWindowAttribute(Handle, 2, ref disabled, sizeof(int));
        var noBorder = unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE on Windows 11; ignored on Windows 10.
        DwmSetWindowAttribute(Handle, 34, ref noBorder, sizeof(int));
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        // Invalidating only the exposed strip can preserve the previous border
        // inside the resized client surface and produce "paint trails".
        Invalidate(invalidateChildren: true);
    }

    internal static int ResizeHitTest(Point point, Size size, int border, bool maximized)
    {
        if (maximized) return 1;
        var left = point.X < border;
        var right = point.X >= size.Width - border;
        var top = point.Y < border;
        var bottom = point.Y >= size.Height - border;
        if (top) return left ? 13 : right ? 14 : 12;
        if (bottom) return left ? 16 : right ? 17 : 15;
        return left ? 10 : right ? 11 : 1;
    }

    internal void ToggleMaximize() => WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;

    internal ContextMenuStrip BuildWindowMenu()
    {
        _windowMenu?.Dispose();
        var menu = new ContextMenuStrip { BackColor = Color.FromArgb(29, 22, 16), ForeColor = Color.FromArgb(239, 207, 149),
            ShowImageMargin = false, Renderer = new ToolStripProfessionalRenderer(new RealmlyMenuColors()) };
        void Item(string text, bool enabled, Action action, string? shortcut = null)
        {
            var item = new ToolStripMenuItem(text) { Enabled = enabled, ShortcutKeyDisplayString = shortcut };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }
        Item("Restore", WindowState != FormWindowState.Normal, () => WindowState = FormWindowState.Normal);
        Item("Move", WindowState == FormWindowState.Normal, () => PostMessage(Handle, 0x0112, (IntPtr)0xF010, IntPtr.Zero));
        Item("Size", WindowState == FormWindowState.Normal, () => PostMessage(Handle, 0x0112, (IntPtr)0xF000, IntPtr.Zero));
        Item("Minimize", WindowState != FormWindowState.Minimized, () => WindowState = FormWindowState.Minimized);
        Item("Maximize", WindowState != FormWindowState.Maximized, () => WindowState = FormWindowState.Maximized);
        menu.Items.Add(new ToolStripSeparator());
        Item("Close", true, Close, "Alt+F4");
        return _windowMenu = menu;
    }

    internal void ShowWindowMenu() => BuildWindowMenu().Show(_titleBar, new Point(0, _titleBar.Height));

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (keyData == (Keys.Alt | Keys.Space)) { ShowWindowMenu(); return true; }
        if (keyData == (Keys.Alt | Keys.F4)) { Close(); return true; }
        return base.ProcessCmdKey(ref message, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _windowMenu?.Dispose();
        base.Dispose(disposing);
    }

    internal void DragWindow()
    {
        ReleaseCapture();
        SendMessage(Handle, 0x00A1, (IntPtr)2, IntPtr.Zero);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x0085) // WM_NCPAINT: all visible frame pixels are ours.
        {
            message.Result = IntPtr.Zero;
            return;
        }
        if (message.Msg == 0x0086 && WindowState != FormWindowState.Minimized) // WM_NCACTIVATE
        {
            // -1 preserves activation processing without repainting a native frame.
            message.Result = DefWindowProc(message.HWnd, message.Msg, message.WParam, (IntPtr)(-1));
            Invalidate();
            return;
        }
        if (message.Msg == 0x0083) // WM_NCCALCSIZE: paint our own frame.
        {
            message.Result = IntPtr.Zero;
            return;
        }
        base.WndProc(ref message);
        if (message.Msg == 0x031E) { DisableNativeFrame(); Invalidate(true); } // DWM composition changed.
        if (message.Msg == 0x0084) // Signed coordinates support monitors left/above the primary screen.
        {
            var packed = message.LParam.ToInt64();
            var point = PointToClient(new Point(unchecked((short)packed), unchecked((short)(packed >> 16))));
            var hit = ResizeHitTest(point, ClientSize, Math.Max(5, 6 * DeviceDpi / 96), WindowState == FormWindowState.Maximized);
            if (hit != 1) message.Result = (IntPtr)hit;
        }
        else if (message.Msg == 0x0024 && message.LParam != IntPtr.Zero) // WM_GETMINMAXINFO
        {
            var screen = Screen.FromHandle(message.HWnd);
            var info = Marshal.PtrToStructure<MinMaxInfo>(message.LParam);
            info.MaxPosition = new Point(screen.WorkingArea.Left - screen.Bounds.Left, screen.WorkingArea.Top - screen.Bounds.Top);
            info.MaxSize = screen.WorkingArea.Size;
            info.MinTrackSize = new Point(MinimumSize.Width, MinimumSize.Height);
            Marshal.StructureToPtr(info, message.LParam, false);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Size MaxSize;
        public Point MaxPosition;
        public Point MinTrackSize;
        public Point MaxTrackSize;
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr DefWindowProc(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}

internal sealed class RealmlyMenuColors : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Color.FromArgb(29, 22, 16);
    public override Color MenuItemSelected => Color.FromArgb(77, 53, 26);
    public override Color MenuItemBorder => Color.FromArgb(160, 116, 51);
    public override Color MenuBorder => Color.FromArgb(125, 89, 39);
}

internal sealed class RealmlyTitleBar : Panel
{
    private readonly RealmlyWindow _owner;
    private readonly CaptionButton[] _buttons;
    private readonly ToolTip _tips = new();

    internal RealmlyTitleBar(RealmlyWindow owner)
    {
        _owner = owner;
        DoubleBuffered = true;
        AccessibleName = "Realmly Companion title bar";
        _buttons = [new CaptionButton("Minimize"), new CaptionButton("Maximize"), new CaptionButton("Close")];
        _buttons[0].Click += (_, _) => owner.WindowState = FormWindowState.Minimized;
        _buttons[1].Click += (_, _) => owner.ToggleMaximize();
        _buttons[2].Click += (_, _) => owner.Close();
        foreach (var button in _buttons)
        {
            Controls.Add(button);
            _tips.SetToolTip(button, button.AccessibleName);
        }
        owner.Resize += (_, _) =>
        {
            _buttons[1].Restore = owner.WindowState == FormWindowState.Maximized;
            _buttons[1].AccessibleName = _buttons[1].Restore ? "Restore window" : "Maximize";
            _tips.SetToolTip(_buttons[1], _buttons[1].AccessibleName);
            _buttons[1].Invalidate();
        };
        MouseDown += (_, e) => { if (e.Button == MouseButtons.Left && e.Clicks == 1) owner.DragWindow(); };
        MouseDoubleClick += (_, e) => { if (e.Button == MouseButtons.Left) owner.ToggleMaximize(); };
        MouseUp += (_, e) => { if (e.Button == MouseButtons.Right) owner.ShowWindowMenu(); };
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (_buttons is null) return;
        var width = 46 * DeviceDpi / 96;
        for (var index = 0; index < _buttons.Length; index++)
            _buttons[index].SetBounds(Width - (_buttons.Length - index) * width, 0, width, Height - 1);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using var gradient = new LinearGradientBrush(ClientRectangle, Color.FromArgb(49, 33, 20), Color.FromArgb(22, 17, 13), 0f);
        e.Graphics.FillRectangle(gradient, ClientRectangle);
        var scale = DeviceDpi / 96f;
        var icon = (int)(25 * scale);
        if (_owner.Icon is not null) e.Graphics.DrawIcon(_owner.Icon, new Rectangle((int)(14 * scale), (Height - icon) / 2, icon, icon));
        using var font = new Font("Segoe UI", 9f, FontStyle.Bold);
        TextRenderer.DrawText(e.Graphics, "REALMLY COMPANION", font,
            new Rectangle((int)(49 * scale), 0, Math.Max(0, Width - (int)(200 * scale)), Height),
            Color.FromArgb(229, 190, 113), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        using var line = new Pen(Color.FromArgb(112, 77, 31));
        e.Graphics.DrawLine(line, 0, Height - 1, Width, Height - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tips.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class CaptionButton : Button
{
    private readonly string _action;
    private bool _hover;
    internal bool Restore { get; set; }

    internal CaptionButton(string action)
    {
        _action = action;
        Name = "window" + action;
        AccessibleName = action;
        AccessibleRole = AccessibleRole.PushButton;
        TabStop = false; // Like native caption controls; Alt+Space/Alt+F4 remain Windows shortcuts.
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(_hover ? (_action == "Close" ? Color.FromArgb(125, 45, 32) : Color.FromArgb(74, 51, 26)) : Color.FromArgb(27, 20, 14));
        var size = Math.Max(10, 10 * DeviceDpi / 96);
        var x = (Width - size) / 2;
        var y = (Height - size) / 2;
        using var pen = new Pen(Color.FromArgb(238, 208, 151), Math.Max(1, DeviceDpi / 96f));
        if (_action == "Close")
        {
            e.Graphics.DrawLine(pen, x, y, x + size, y + size);
            e.Graphics.DrawLine(pen, x + size, y, x, y + size);
        }
        else if (_action == "Minimize") e.Graphics.DrawLine(pen, x, y + size, x + size, y + size);
        else if (Restore)
        {
            e.Graphics.DrawRectangle(pen, x + size / 4, y, size * 3 / 4, size * 3 / 4);
            using var background = new SolidBrush(Color.FromArgb(27, 20, 14));
            e.Graphics.FillRectangle(background, x, y + size / 4, size * 3 / 4, size * 3 / 4);
            e.Graphics.DrawRectangle(pen, x, y + size / 4, size * 3 / 4, size * 3 / 4);
        }
        else e.Graphics.DrawRectangle(pen, x, y, size, size);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(4, 4, Width - 8, Height - 8), pen.Color, BackColor);
    }
}
