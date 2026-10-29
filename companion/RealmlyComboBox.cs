using System.Runtime.InteropServices;

namespace RealmlyCompanion;

// A native, accessible dropdown with Realmly's dark face, gold arrow and menu.
internal sealed class RealmlyComboBox : ComboBox
{
    private bool _hover;
    internal RealmlyComboBox()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        BackColor = Color.FromArgb(25, 18, 12);
        ForeColor = Color.FromArgb(239, 215, 164);
        IntegralHeight = false;
    }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var selected = (e.State & DrawItemState.Selected) != 0;
        using var fill = new SolidBrush(selected ? Color.FromArgb(81, 55, 24) : BackColor);
        e.Graphics.FillRectangle(fill, e.Bounds);
        var text = e.Index >= 0 && e.Index < Items.Count ? GetItemText(Items[e.Index]) : Text;
        TextRenderer.DrawText(e.Graphics, text, Font, Rectangle.Inflate(e.Bounds, -5, 0),
            Enabled ? ForeColor : ReadableCheckBox.DisabledTextColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
        base.OnDrawItem(e);
    }
    private void PaintFace(Graphics graphics)
    {
        if (DropDownStyle != ComboBoxStyle.DropDownList) return;
        var bounds = ClientRectangle;
        if (bounds.Width < 2 || bounds.Height < 2) return;
        using var fill = new SolidBrush(BackColor);
        graphics.FillRectangle(fill, bounds);
        var arrowWidth = Math.Max(24, 24 * DeviceDpi / 96);
        var arrow = new Rectangle(Math.Max(0, bounds.Width - arrowWidth), 1, arrowWidth - 1, bounds.Height - 2);
        using var arrowFill = new SolidBrush(Enabled && (_hover || DroppedDown) ? Color.FromArgb(72, 48, 21) : Color.FromArgb(44, 30, 17));
        graphics.FillRectangle(arrowFill, arrow);
        var color = Enabled ? ForeColor : ReadableCheckBox.DisabledTextColor;
        using var border = new Pen(Enabled && (Focused || _hover) ? Color.FromArgb(227, 183, 98) : Color.FromArgb(139, 102, 47));
        graphics.DrawRectangle(border, 0, 0, bounds.Width - 1, bounds.Height - 1);
        graphics.DrawLine(border, arrow.Left, 1, arrow.Left, bounds.Height - 2);
        var middle = new Point(arrow.Left + arrow.Width / 2, arrow.Top + arrow.Height / 2);
        var size = Math.Max(3, 3 * DeviceDpi / 96);
        using var gold = new SolidBrush(color);
        graphics.FillPolygon(gold, new Point[] {new Point(middle.X - size, middle.Y - 1), new Point(middle.X + size, middle.Y - 1), new Point(middle.X, middle.Y + size) });
        var textBounds = new Rectangle(6, 2, Math.Max(0, arrow.Left - 10), Math.Max(0, bounds.Height - 4));
        TextRenderer.DrawText(graphics, SelectedIndex >= 0 ? GetItemText(SelectedItem) : Text, Font, textBounds, color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(graphics, textBounds, color, BackColor);
    }
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (!IsHandleCreated || IsDisposed) return;
        if (m.Msg == 0xF) { using var graphics = Graphics.FromHwnd(Handle); PaintFace(graphics); }
        else if (m.Msg is 0x317 or 0x318 && m.WParam != IntPtr.Zero) { using var graphics = Graphics.FromHdc(m.WParam); PaintFace(graphics); }
    }
    protected override void OnDropDown(EventArgs e)
    {
        // Let Windows render a dark scrollbar for long native dropdown lists.
        var info = new ComboInfo { Size = Marshal.SizeOf<ComboInfo>() };
        if (GetComboBoxInfo(Handle, ref info)) SetWindowTheme(info.List, "DarkMode_Explorer", null);
        base.OnDropDown(e); Invalidate();
    }
    protected override void OnDropDownClosed(EventArgs e) { base.OnDropDownClosed(e); Invalidate(); }
    protected override void OnSelectedIndexChanged(EventArgs e) { base.OnSelectedIndexChanged(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnMouseEnter(EventArgs e) { _hover = true; base.OnMouseEnter(e); Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; base.OnMouseLeave(e); Invalidate(); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); ItemHeight = Font.Height + Math.Max(6, 6 * DeviceDpi / 96); }
    [StructLayout(LayoutKind.Sequential)]
    private struct ComboInfo { public int Size; public Rectangle Item, Button; public int State; public IntPtr Combo, Edit, List; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetComboBoxInfo(IntPtr handle, ref ComboInfo info);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr handle, string theme, string? subId);
}
