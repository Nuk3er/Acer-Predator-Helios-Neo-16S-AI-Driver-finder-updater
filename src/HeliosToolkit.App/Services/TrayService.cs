using System.IO;
using System.Windows;
using Serilog;
using WinForms = System.Windows.Forms;

namespace HeliosToolkit.App.Services;

/// <summary>
/// Tray icon + minimize-to-tray. Minimizing hides the window into the tray so the
/// timer-resolution hold keeps running while gaming; double-click or "Open" restores.
/// Closing the window still exits the app — predictable and explicit.
/// </summary>
public sealed class TrayService(System.TimerResolutionService timerResolution) : IDisposable
{
    private WinForms.NotifyIcon? _icon;
    private Window? _window;
    private bool _balloonShown;
    private WinForms.ToolStripMenuItem? _holdItem;

    public void Attach(Window window)
    {
        _window = window;

        _icon = new WinForms.NotifyIcon
        {
            Text = "Helios Neo Toolkit",
            Visible = true,
            Icon = LoadIcon(),
        };

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Open Helios Toolkit", null, (_, _) => Restore());
        _holdItem = new WinForms.ToolStripMenuItem("Hold timer", null, (_, _) =>
        {
            if (timerResolution.IsHolding)
            {
                timerResolution.Stop();
            }
            else
            {
                timerResolution.Start();
            }
        });
        menu.Items.Add(_holdItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Application.Current.Shutdown());
        menu.Opening += (_, _) =>
        {
            _holdItem.Checked = timerResolution.IsHolding;
            _holdItem.Text = $"Hold timer ({timerResolution.TargetMs:0.0000} ms)";
        };
        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (_, _) => Restore();

        window.StateChanged += (_, _) =>
        {
            if (window.WindowState == WindowState.Minimized)
            {
                window.Hide();
                if (!_balloonShown)
                {
                    _balloonShown = true;
                    _icon?.ShowBalloonTip(
                        4000,
                        "Still running in the tray",
                        "Helios keeps running here — the 0.5 ms timer hold stays active while you game. Double-click to reopen.",
                        WinForms.ToolTipIcon.Info);
                }
            }
        };
    }

    private void Restore()
    {
        if (_window is null)
        {
            return;
        }

        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private static global::System.Drawing.Icon LoadIcon()
    {
        try
        {
            using Stream stream = Application
                .GetResourceStream(new Uri("pack://application:,,,/Resources/icon.ico"))!.Stream;
            return new global::System.Drawing.Icon(stream);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Tray icon resource unavailable, using fallback");
            return global::System.Drawing.SystemIcons.Application;
        }
    }

    public void Dispose()
    {
        if (_icon is not null)
        {
            _icon.Visible = false;
            _icon.Dispose();
            _icon = null;
        }
    }
}
