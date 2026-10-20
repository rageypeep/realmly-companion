using System.Drawing.Drawing2D;

namespace RealmlyCompanion;

// Keep native checkbox behavior and accessibility, with dark bronze/gold rendering.
internal sealed class ReadableCheckBox : CheckBox
{
    private bool _hover;
    internal ReadableCheckBox() => SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    internal static Color DisabledTextColor => Color.FromArgb(190, 167, 123);
    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        var glyph = Math.Max(13, 13 * DeviceDpi / 96);
        return new Size(text.Width + glyph + Math.Max(5, 5 * DeviceDpi / 96) + 8, Math.Max(text.Height, glyph) + 6);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? BackColor);
        var size = Math.Max(13, 13 * DeviceDpi / 96);
        var glyph = new Rectangle(1, Math.Max(1, (Height - size) / 2), size, size);
        var gap = Math.Max(5, 5 * DeviceDpi / 96);
        using var fill = new SolidBrush(Checked ? Color.FromArgb(91, 62, 25) : Color.FromArgb(30, 22, 15));
        using var border = new Pen(!Enabled ? Color.FromArgb(105, 88, 59) : _hover || Focused ? Color.FromArgb(255, 215, 128) : Color.FromArgb(180, 133, 55));
        e.Graphics.FillRectangle(fill, glyph);
        e.Graphics.DrawRectangle(border, glyph);
        if (Checked)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var tick = new Pen(Enabled ? Color.FromArgb(255, 222, 150) : DisabledTextColor, Math.Max(2, 2 * DeviceDpi / 96)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            e.Graphics.DrawLines(tick, [new PointF(glyph.Left + size * .22f, glyph.Top + size * .52f),
                new PointF(glyph.Left + size * .43f, glyph.Top + size * .73f), new PointF(glyph.Left + size * .8f, glyph.Top + size * .27f)]);
        }
        var bounds = new Rectangle(glyph.Right + gap, 0, Math.Max(0, Width - glyph.Right - gap), Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, bounds, Enabled ? ForeColor : DisabledTextColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, bounds, ForeColor, BackColor);
    }
}
