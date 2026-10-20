namespace RealmlyCompanion;

internal sealed class CompanionTray : IDisposable
{
    private readonly NotifyIcon? _icon;
    private readonly ContextMenuStrip? _menu;
    private readonly HashSet<string> _notified = new(StringComparer.Ordinal);
    private bool _enabled;
    public bool Enabled
    {
        get => _enabled;
        set { _enabled = value; if (_icon is not null) _icon.Visible = value; }
    }
    internal int NotificationCount => _notified.Count;

    public CompanionTray(Icon icon, Action open, Action checkUpdates, Action exit, bool native = true)
    {
        if (!native) return; // Offscreen tests never create an icon in the user's tray.
        _menu = new ContextMenuStrip { BackColor = Color.FromArgb(29, 22, 16), ForeColor = Color.FromArgb(245, 229, 184), ShowImageMargin = false };
        _menu.Items.Add("Open Realmly Companion", null, (_, _) => open());
        _menu.Items.Add("Check for updates", null, (_, _) => { open(); checkUpdates(); });
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Exit", null, (_, _) => exit());
        _icon = new NotifyIcon { Icon = icon, Text = "Realmly Companion", ContextMenuStrip = _menu };
        _icon.DoubleClick += (_, _) => open();
        _icon.BalloonTipClicked += (_, _) => open();
    }

    public bool NotifyUpdate(string key, string message, bool notificationsEnabled)
    {
        if (!Enabled || !notificationsEnabled || !_notified.Add(key)) return false;
        try
        {
            _icon?.ShowBalloonTip(6000, "Realmly update available", message, ToolTipIcon.Info);
            return true;
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception or ArgumentException)
        {
            _notified.Remove(key);
            return false; // Notifications are optional; an unavailable shell must not break update checks.
        }
    }

    public void Dispose()
    {
        Enabled = false;
        _icon?.Dispose();
        _menu?.Dispose();
    }
}
