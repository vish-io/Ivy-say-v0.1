using Forms = System.Windows.Forms;

namespace IvySay;

// This icon remains available when the WPF pill window is hidden.
internal sealed class NotificationArea : IDisposable
{
    private readonly Forms.NotifyIcon icon;
    private readonly Forms.ContextMenuStrip menu;
    private readonly System.Drawing.Icon image;
    public NotificationArea(Action openSettings, Action quit) : this(openSettings, quit, () => { }) { }
    public NotificationArea(Action openSettings, Action quit, Action recover)
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/IvySay;component/IvySay.ico"))
            ?? throw new InvalidOperationException("Ivy Say notification icon is missing.");
        using (resource.Stream)
        using (var loaded = new System.Drawing.Icon(resource.Stream)) image = (System.Drawing.Icon)loaded.Clone();
        menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Ivy Say settings", null, (_, _) => openSettings());
        menu.Items.Add("Quit Ivy Say", null, (_, _) => quit());
        menu.Items.Add("Recover last dictation", null, (_, _) => recover());
        icon = new Forms.NotifyIcon { Icon = image, Text = "Ivy Say — click for settings", ContextMenuStrip = menu, Visible = true };
        icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) openSettings(); };
    }
    public void ShowRecoveryAvailable() { try { icon.ShowBalloonTip(4000, "Ivy Say", "Text needs attention. Right-click Ivy Say and choose Recover last dictation.", Forms.ToolTipIcon.Info); } catch { } }
    public void ShowSavedNote() { try { icon.ShowBalloonTip(3000, "Ivy Say", "Saved to Quick Notes", Forms.ToolTipIcon.Info); } catch { } }
    public void Dispose() { icon.Visible = false; icon.Dispose(); menu.Dispose(); image.Dispose(); }
}
