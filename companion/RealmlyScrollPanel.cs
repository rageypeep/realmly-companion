using System.Drawing.Drawing2D;

namespace RealmlyCompanion;

internal sealed class RealmlyScrollPanel : Panel
{
    private readonly Panel _viewport = new() { BackColor = Color.FromArgb(15, 11, 9) };
    private readonly RealmlyScrollBar _bar;
    private Control? _content;
    private bool _arranging;
    internal int Offset { get; private set; }
    internal bool FollowFocus { get; set; } = true;
    internal int MaximumOffset => Math.Max(0, (_content?.Height ?? 0) - _viewport.Height);
    internal int ContentWidth => _content?.Width ?? 0;
    internal int ViewportWidth => _viewport.Width;

    internal RealmlyScrollPanel()
    {
        DoubleBuffered = true;
        _bar = new RealmlyScrollBar(ScrollTo);
        Controls.Add(_viewport);
        Controls.Add(_bar);
    }

    internal void SetContent(Control content)
    {
        _content = content;
        content.Dock = DockStyle.None;
        content.AutoSize = false; // The viewport owns width; table preferred size supplies height only.
        _viewport.Controls.Add(content);
        content.SizeChanged += (_, _) => Arrange();
        content.Layout += (_, _) => Arrange();
        HookFocus(content);
        Arrange();
    }

    private void HookFocus(Control control)
    {
        // Container Enter events fire as focus travels into their descendants.
        // Scrolling a whole section/content table would jump the page on startup.
        if (control.Controls.Count == 0 && control.TabStop)
            control.Enter += (_, _) => EnsureVisible(control);
        foreach (Control child in control.Controls) HookFocus(child);
    }

    private void EnsureVisible(Control control)
    {
        if (!FollowFocus || _arranging || !control.IsHandleCreated || _viewport.Height <= 0 || FindForm()?.Visible != true) return;
        var top = _viewport.PointToClient(control.PointToScreen(Point.Empty)).Y;
        if (top < 0) ScrollTo(Offset + top);
        else if (top + control.Height > _viewport.Height) ScrollTo(Offset + top + control.Height - _viewport.Height);
    }

    internal void ScrollTo(int value)
    {
        Offset = Math.Clamp(value, 0, MaximumOffset);
        if (_content is not null) _content.Top = -Offset;
        _bar.SetRange(_content?.Height ?? 0, _viewport.Height, Offset);
    }

    internal void ScrollByWheel(int delta) => ScrollTo(Offset - delta / 120 * 54 * DeviceDpi / 96);

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        ScrollByWheel(e.Delta);
        if (e is HandledMouseEventArgs handled) handled.Handled = true;
        base.OnMouseWheel(e);
    }

    protected override void OnLayout(LayoutEventArgs levent) { base.OnLayout(levent); Arrange(); }

    private void Arrange()
    {
        if (_arranging || _content is null || _bar is null) return;
        _arranging = true;
        try
        {
            var height = _content.GetPreferredSize(new Size(Math.Max(1, Width), 0)).Height;
            var show = height > Height;
            var width = show ? Math.Max(12, 12 * DeviceDpi / 96) : 0;
            _bar.Visible = show;
            _viewport.SetBounds(0, 0, Math.Max(1, Width - width), Height);
            _bar.SetBounds(Width - width, 0, width, Height);
            var contentHeight = _content.GetPreferredSize(new Size(_viewport.Width, 0)).Height;
            _content.SetBounds(0, -Offset, _viewport.Width, contentHeight);
            ScrollTo(Offset);
        }
        finally { _arranging = false; }
    }
}

internal sealed class RealmlyScrollBar : Control
{
    private readonly Action<int> _scroll;
    private readonly int _smallChange;
    private int _total, _viewport, _offset;
    private int? _drag;
    private bool _hover;
    internal int Maximum => Math.Max(0, _total - _viewport);

    internal RealmlyScrollBar(Action<int> scroll, int smallChange = 32)
    {
        _scroll = scroll;
        _smallChange = smallChange;
        TabStop = true;
        AccessibleName = "Scroll field notes";
        AccessibleRole = AccessibleRole.ScrollBar;
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }

    internal void SetRange(int total, int viewport, int offset)
    {
        var changed = _offset != offset;
        _total = total; _viewport = viewport; _offset = offset;
        Invalidate();
        if (changed && IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
    }

    internal Rectangle Thumb
    {
        get
        {
            var padding = Math.Max(3, 3 * DeviceDpi / 96);
            var track = Math.Max(1, Height - padding * 2);
            var height = Math.Min(track, Math.Max(28 * DeviceDpi / 96, (int)((long)track * _viewport / Math.Max(1, _total))));
            var top = padding + (Maximum == 0 ? 0 : (int)((long)(track - height) * _offset / Maximum));
            return new Rectangle(padding, top, Math.Max(1, Width - padding * 2), height);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.FromArgb(21, 16, 12));
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var thumb = Thumb;
        using var brush = new SolidBrush(_hover || _drag.HasValue || Focused ? Color.FromArgb(224, 182, 96) : Color.FromArgb(137, 100, 46));
        using var shape = new GraphicsPath();
        var radius = Math.Min(thumb.Width, thumb.Height);
        shape.AddArc(thumb.Left, thumb.Top, radius, radius, 180, 180);
        shape.AddArc(thumb.Left, thumb.Bottom - radius, radius, radius, 0, 180);
        shape.CloseFigure();
        e.Graphics.FillPath(brush, shape);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        if (Thumb.Contains(e.Location)) { _drag = e.Y - Thumb.Top; Capture = true; }
        else _scroll(_offset + (e.Y < Thumb.Top ? -_viewport : _viewport));
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_drag is not int drag) return;
        var padding = Math.Max(3, 3 * DeviceDpi / 96);
        var travel = Math.Max(1, Height - padding * 2 - Thumb.Height);
        _scroll((int)((long)(e.Y - drag - padding) * Maximum / travel));
    }
    protected override void OnMouseUp(MouseEventArgs e) { _drag = null; Capture = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnMouseCaptureChanged(EventArgs e) { _drag = null; base.OnMouseCaptureChanged(e); }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        _scroll(_offset - e.Delta / 120 * _smallChange * 3);
        if (e is HandledMouseEventArgs handled) handled.Handled = true;
        // Do not bubble to a parent viewport and scroll twice.
    }
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        var target = e.KeyCode switch { Keys.Up => _offset - _smallChange, Keys.Down => _offset + _smallChange,
            Keys.PageUp => _offset - _viewport, Keys.PageDown => _offset + _viewport,
            Keys.Home => 0, Keys.End => Maximum, _ => (int?)null };
        if (target.HasValue) { _scroll(target.Value); e.Handled = true; e.SuppressKeyPress = true; }
        base.OnKeyDown(e);
    }
    protected override AccessibleObject CreateAccessibilityInstance() => new ScrollAccessibility(this);
    private sealed class ScrollAccessibility(RealmlyScrollBar owner) : ControlAccessibleObject(owner)
    {
        public override string? Value
        {
            get => $"{owner._offset} of {owner.Maximum}";
            set { if (int.TryParse(value, out var offset)) owner._scroll(offset); }
        }
    }
}
