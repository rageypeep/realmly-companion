using System.Reflection;
using RealmlyCompanion;

internal static class ScreenshotThemeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Key(RealmlyScrollBar bar, Keys key) => typeof(RealmlyScrollBar).GetMethod("OnKeyDown", Private)!.Invoke(bar, [new KeyEventArgs(key)]);
    public static void Run(string output)
    {
        using var form = new RealmlyWindow { ClientSize=new Size(820,650),StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000),Font=new Font("Segoe UI",10) };
        var content = new TableLayoutPanel { ColumnCount=1,RowCount=2,Dock=DockStyle.Fill,Padding=new Padding(20) };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));content.RowStyles.Add(new RowStyle(SizeType.Percent,100));content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        using var grid = new RealmlyScreenshotGrid { AllowUserToAddRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.FromArgb(20,16,11),EnableHeadersVisualStyles=false };
        grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor=Color.FromArgb(25,20,14),ForeColor=Color.Gold,SelectionBackColor=Color.FromArgb(81,55,24),SelectionForeColor=Color.Gold };
        grid.ColumnHeadersDefaultCellStyle = grid.DefaultCellStyle;
        grid.Columns.Add(new DataGridViewCheckBoxColumn { CellTemplate=new RealmlyCheckBoxCell(),HeaderText="Send",FillWeight=25 });
        grid.Columns.Add(new DataGridViewTextBoxColumn {HeaderText="Recorded milestone",FillWeight=75});
        for(var i=0;i<80;i++)grid.Rows.Add(i%2==0,"Adventure milestone "+(i+1));
        content.Controls.Add(grid.CreateViewport(),0,0);
        var combo = new RealmlyComboBox { Dock=DockStyle.Fill };
        combo.Items.AddRange(Enumerable.Range(1,40).Select(i=>(object)("Adventure day "+i)).ToArray());combo.SelectedIndex=0;content.Controls.Add(combo,0,1);
        form.SetWindowContent(content);form.Show();Application.DoEvents();
        Check(grid.ScrollBars==ScrollBars.None && grid.ScrollBar.Visible && grid.ScrollBar.Maximum>0,"Native scrollbar remains, or custom scrollbar unavailable");
        Check(grid.Columns[0].CellTemplate is RealmlyCheckBoxCell && grid.Rows[0].Cells[0].AccessibilityObject.Role==new DataGridViewCheckBoxCell().AccessibilityObject.Role,"Checkbox semantics lost: " + grid.Rows[0].Cells[0].GetType().Name + "/" + grid.Rows[0].Cells[0].AccessibilityObject.Role);
        Key(grid.ScrollBar,Keys.End);Check(grid.FirstDisplayedScrollingRowIndex==grid.ScrollBar.Maximum,"Scrollbar End does not reach last page");
        Key(grid.ScrollBar,Keys.Home);Check(grid.FirstDisplayedScrollingRowIndex==0,"Scrollbar Home failed");
        Key(grid.ScrollBar,Keys.Down);Check(grid.FirstDisplayedScrollingRowIndex==1,"Scrollbar Down failed");
        Key(grid.ScrollBar,Keys.PageDown);Check(grid.FirstDisplayedScrollingRowIndex>1,"Scrollbar PageDown failed");
        typeof(RealmlyScreenshotGrid).GetMethod("OnMouseWheel",Private)!.Invoke(grid,[new HandledMouseEventArgs(MouseButtons.None,0,20,20,120)]);
        Check(grid.FirstDisplayedScrollingRowIndex<grid.ScrollBar.Maximum,"Grid wheel failed");
        grid.ScrollToRow(9999);Check(grid.FirstDisplayedScrollingRowIndex==grid.ScrollBar.Maximum,"Last row clamping failed");
        grid.ScrollToRow(-20);Check(grid.FirstDisplayedScrollingRowIndex==0,"Top clamping failed");
        Check(combo.DrawMode==DrawMode.OwnerDrawFixed && combo.DropDownStyle==ComboBoxStyle.DropDownList,"Native dropdown behavior changed");
        combo.SelectedIndex=5;Check(combo.Text=="Adventure day 6","Dropdown selection lost");
        using(var bitmap=new Bitmap(340,50))
        {
            using var graphics=Graphics.FromImage(bitmap);
            typeof(RealmlyComboBox).GetMethod("OnDrawItem",Private)!.Invoke(combo,[new DrawItemEventArgs(graphics,combo.Font,new Rectangle(0,0,340,50),5,DrawItemState.Selected)]);
            Check(bitmap.GetPixel(2,2).ToArgb()==Color.FromArgb(81,55,24).ToArgb(),"Dropdown highlight is not bronze");
            bitmap.Save(Path.Combine(output,"companion-dropdown-selected.png"));
        }
        combo.DroppedDown=true;Application.DoEvents();Check(combo.DroppedDown,"Native dropdown no longer opens");combo.DroppedDown=false;
        Application.DoEvents();
        using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(output,"companion-themed-screenshot-controls.png"));}
        grid.Rows.Clear();Application.DoEvents();Check(!grid.ScrollBar.Visible,"Scrollbar remains on an empty gallery");form.Close();
        Console.WriteLine("PASS screenshot checkbox accessibility, themed dropdowns and native selection, custom scrollbar Home/End/arrows/page/wheel/clamping/empty state");
    }
}
