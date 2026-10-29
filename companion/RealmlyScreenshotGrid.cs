namespace RealmlyCompanion;

// Retain DataGridView's keyboard navigation and checkbox accessibility while
// replacing its native blue glyphs and Windows scrollbar with Realmly controls.
internal sealed class RealmlyScreenshotGrid : DataGridView
{
    internal RealmlyScrollBar ScrollBar { get; }
    private bool _syncing;

    internal RealmlyScreenshotGrid()
    {
        DoubleBuffered = true;
        ScrollBars = ScrollBars.None;
        AllowUserToResizeRows = false;
        ScrollBar = new RealmlyScrollBar(ScrollToRow, smallChange: 1) { Dock = DockStyle.Right, AccessibleName = "Scroll screenshots", Visible = false };
        Scroll += (_, _) => SyncScroll();
        RowsAdded += (_, _) => SyncScroll();
        RowsRemoved += (_, _) => SyncScroll();
        SizeChanged += (_, _) => SyncScroll();
        ColumnHeadersHeightChanged += (_, _) => SyncScroll();
    }

    internal Control CreateViewport()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = BackgroundColor };
        Dock = DockStyle.Fill;
        panel.Controls.Add(this);
        panel.Controls.Add(ScrollBar);
        ScrollBar.Width = Math.Max(12, 12 * DeviceDpi / 96);
        return panel;
    }

    private int VisibleRows => Math.Max(1, DisplayedRowCount(false));
    internal void ScrollToRow(int row)
    {
        if (Rows.Count == 0) return;
        FirstDisplayedScrollingRowIndex = Math.Clamp(row, 0, Math.Max(0, Rows.Count - VisibleRows));
        SyncScroll();
    }

    private void SyncScroll()
    {
        if (_syncing || ScrollBar is null || !IsHandleCreated) return;
        _syncing = true;
        try
        {
            var visible = VisibleRows;
            ScrollBar.Visible = Rows.Count > visible;
            ScrollBar.Enabled = Enabled;
            ScrollBar.Width = Math.Max(12, 12 * DeviceDpi / 96);
            ScrollBar.SetRange(Rows.Count, visible, Math.Max(0, FirstDisplayedScrollingRowIndex));
        }
        finally { _syncing = false; }
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); SyncScroll(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); SyncScroll(); Invalidate(); }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        var lines = SystemInformation.MouseWheelScrollLines;
        ScrollToRow(Math.Max(0, FirstDisplayedScrollingRowIndex) - e.Delta / 120 * (lines < 0 ? VisibleRows : lines));
        if (e is HandledMouseEventArgs handled) handled.Handled = true;
    }
}

internal sealed class RealmlyCheckBoxCell : DataGridViewCheckBoxCell
{
    protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
        DataGridViewElementStates cellState, object? value, object? formattedValue, string? errorText,
        DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
    {
        base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText,
            cellStyle, advancedBorderStyle, paintParts & ~DataGridViewPaintParts.ContentForeground);
        if ((paintParts & DataGridViewPaintParts.ContentForeground) == 0 || DataGridView is not { } grid) return;
        var size = Math.Min(Math.Max(13, 13 * grid.DeviceDpi / 96), Math.Max(1, Math.Min(cellBounds.Width, cellBounds.Height) - 4));
        var glyph = new Rectangle(cellBounds.Left + (cellBounds.Width - size) / 2, cellBounds.Top + (cellBounds.Height - size) / 2, size, size);
        var current = grid.CurrentCellAddress;
        var pointer = grid.PointToClient(Cursor.Position);
        var hover = grid.HitTest(pointer.X, pointer.Y);
        var active = grid.Focused && current.X == ColumnIndex && current.Y == rowIndex || hover.ColumnIndex == ColumnIndex && hover.RowIndex == rowIndex;
        ReadableCheckBox.DrawGlyph(graphics, glyph, formattedValue is true or CheckState.Checked, grid.Enabled, active);
    }
}
