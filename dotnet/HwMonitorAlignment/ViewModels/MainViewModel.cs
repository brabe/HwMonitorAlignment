using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using HwMonitorAlignment.Models;
using HwMonitorAlignment.Win32;

namespace HwMonitorAlignment.ViewModels;

/// <summary>
/// ViewModel for the main window.  Loads monitor information and exposes
/// the virtual-screen bounds needed to draw the scaled overview canvas.
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    // ----------------------------------------------------------------
    // Monitor list
    // ----------------------------------------------------------------

    public ObservableCollection<MonitorInfo> Monitors { get; } = new();

    public MonitorInfo? PrimaryMonitor =>
        Monitors.FirstOrDefault(m => m.IsPrimary) ?? Monitors.FirstOrDefault();

    // ----------------------------------------------------------------
    // Virtual screen geometry (in physical pixels)
    // ----------------------------------------------------------------

    private int _vscreenX, _vscreenY, _vscreenWidth, _vscreenHeight;

    public int VScreenX      { get => _vscreenX;      private set { _vscreenX = value;      OnPropertyChanged(); } }
    public int VScreenY      { get => _vscreenY;      private set { _vscreenY = value;      OnPropertyChanged(); } }
    public int VScreenWidth  { get => _vscreenWidth;  private set { _vscreenWidth = value;  OnPropertyChanged(); } }
    public int VScreenHeight { get => _vscreenHeight; private set { _vscreenHeight = value; OnPropertyChanged(); } }

    // ----------------------------------------------------------------
    // Construction / refresh
    // ----------------------------------------------------------------

    public MainViewModel()
    {
        Refresh();
    }

    /// <summary>
    /// Re-enumerates monitors from Win32 and refreshes all bound properties.
    /// Safe to call after a display-settings change.
    /// </summary>
    public void Refresh()
    {
        Monitors.Clear();

        List<MonitorInfo> monitors;
        try
        {
            monitors = DisplayBackend.GetMonitors();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to enumerate monitors:\n\n{ex.Message}",
                "HwMonitorAlignment",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        foreach (var m in monitors)
            Monitors.Add(m);

        // Update virtual-screen bounds
        try
        {
            var (x, y, w, h) = DisplayBackend.GetVirtualScreenBounds();
            VScreenX      = x;
            VScreenY      = y;
            VScreenWidth  = w;
            VScreenHeight = h;
        }
        catch
        {
            // Compute from monitor list as fallback
            if (Monitors.Count > 0)
            {
                VScreenX      = Monitors.Min(m => m.X);
                VScreenY      = Monitors.Min(m => m.Y);
                VScreenWidth  = Monitors.Max(m => m.X + m.Width)  - VScreenX;
                VScreenHeight = Monitors.Max(m => m.Y + m.Height) - VScreenY;
            }
        }

        OnPropertyChanged(nameof(PrimaryMonitor));
    }

    // ----------------------------------------------------------------
    // INotifyPropertyChanged
    // ----------------------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
