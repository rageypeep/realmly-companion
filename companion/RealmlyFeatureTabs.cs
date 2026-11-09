namespace RealmlyCompanion;

// Themed navigation without native white TabControl borders/pages.
internal sealed class RealmlyFeatureTabs : UserControl
{
    private readonly Control[] _pages;
    private readonly Button[] _tabs;
    private readonly Font _regularFont = new("Segoe UI", 10F);
    private readonly Font _selectedFont = new("Segoe UI", 10F, FontStyle.Bold);
    internal int SelectedIndex { get; private set; }

    internal RealmlyFeatureTabs(Control realmly, Control translation, Control addons, Control discord)
    {
        BackColor = Color.FromArgb(15, 11, 9);
        Dock = DockStyle.Fill; Font = _regularFont;
        _pages = [realmly, translation, addons, discord];
        var body = new Panel { Dock = DockStyle.Fill, BackColor = BackColor };
        var strip = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false,
            Padding = new Padding(20, 8, 20, 3), BackColor = Color.FromArgb(25, 18, 12),
            AccessibleName = "Companion features", AccessibleRole = AccessibleRole.PageTabList };
        _tabs = [new Button { Text = "Realmly", AccessibleName = "Realmly tools tab" },
            new Button { Text = "Translate", AccessibleName = "Translate tab" },
            new Button { Text = "Add-ons", AccessibleName = "Add-ons tab" },
            new Button { Text = "Discord", AccessibleName = "Discord tab" }];
        for (var index = 0; index < _tabs.Length; index++)
        {
            var selected = index;
            var tab = _tabs[index];
            CompanionForm.StyleButton(tab, compact: true);
            tab.AccessibleRole = AccessibleRole.PageTab;
            tab.Click += (_, _) => SelectFeature(selected);
            tab.KeyDown += (_, args) =>
            {
                if (args.KeyCode is not (Keys.Left or Keys.Right)) return;
                SelectFeature((SelectedIndex + (args.KeyCode == Keys.Left ? _tabs.Length - 1 : 1)) % _tabs.Length);
                _tabs[SelectedIndex].Focus(); args.Handled = true;
            };
            strip.Controls.Add(tab);
            _pages[index].Dock = DockStyle.Fill;
            body.Controls.Add(_pages[index]);
        }
        Controls.Add(body); Controls.Add(strip);
        SelectFeature(0);
    }

    internal void SelectFeature(int index)
    {
        if (index < 0 || index >= _pages.Length) return;
        SelectedIndex = index;
        for (var i = 0; i < _pages.Length; i++)
        {
            _pages[i].Visible = i == index;
            _tabs[i].BackColor = i == index ? Color.FromArgb(86, 57, 24) : Color.FromArgb(37, 24, 15);
            _tabs[i].Font = i == index ? _selectedFont : _regularFont;
        }
        _pages[index].BringToFront();
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) { _regularFont.Dispose(); _selectedFont.Dispose(); }
    }
}
