using System.ComponentModel;
using System.Runtime.CompilerServices;
using HwMonitorAlignment.Win32;

namespace HwMonitorAlignment.Models;

/// <summary>
/// Data model for a single physical monitor.
/// Implements INotifyPropertyChanged so the UI can bind directly to X/Y for live updates.
/// </summary>
public class MonitorInfo : INotifyPropertyChanged
{
    // ----------------------------------------------------------------
    // Identity / static properties
    // ----------------------------------------------------------------

    /// <summary>Win32 device name, e.g. "\\.\DISPLAY1"</summary>
    public string DeviceName   { get; set; } = string.Empty;

    /// <summary>Adapter string, e.g. "NVIDIA GeForce RTX 3080"</summary>
    public string AdapterName  { get; set; } = string.Empty;

    /// <summary>Human-readable monitor name from EDID / QueryDisplayConfig</summary>
    public string FriendlyName { get; set; } = string.Empty;

    /// <summary>Physical width in pixels</summary>
    public int Width  { get; set; }

    /// <summary>Physical height in pixels</summary>
    public int Height { get; set; }

    /// <summary>Display orientation (0=Landscape, 1=Portrait, 2=FlippedLandscape, 3=FlippedPortrait)</summary>
    public uint Orientation { get; set; }

    public bool IsPrimary { get; set; }

    // ----------------------------------------------------------------
    // Mutable position (raises PropertyChanged so overlays repaint)
    // ----------------------------------------------------------------

    private int _x;
    private int _y;

    /// <summary>Current virtual-screen X position in physical pixels</summary>
    public int X
    {
        get => _x;
        set { if (_x != value) { _x = value; OnPropertyChanged(); OnPropertyChanged(nameof(PositionText)); } }
    }

    /// <summary>Current virtual-screen Y position in physical pixels</summary>
    public int Y
    {
        get => _y;
        set { if (_y != value) { _y = value; OnPropertyChanged(); OnPropertyChanged(nameof(PositionText)); } }
    }

    // ----------------------------------------------------------------
    // DEVMODE snapshots for apply/rollback
    // ----------------------------------------------------------------

    /// <summary>DEVMODE captured at enumeration time – used for rollback</summary>
    public DEVMODE OriginalDevMode { get; set; }

    /// <summary>Working copy of DEVMODE – updated before calling ApplyMonitorPositions</summary>
    public DEVMODE CurrentDevMode  { get; set; }

    // ----------------------------------------------------------------
    // Derived display helpers
    // ----------------------------------------------------------------

    public string ResolutionText  => $"{Width} × {Height}";
    public string PositionText    => $"({X}, {Y})";
    public string OrientationText => Orientation switch
    {
        0 => "Landscape",
        1 => "Portrait",
        2 => "Landscape (Flipped)",
        3 => "Portrait (Flipped)",
        _ => $"Unknown ({Orientation})"
    };
    public string PrimaryText => IsPrimary ? "Yes" : "No";

    /// <summary>Display label used in overlays and overview</summary>
    public string DisplayLabel => string.IsNullOrWhiteSpace(FriendlyName) ? DeviceName : FriendlyName;

    // ----------------------------------------------------------------
    // INotifyPropertyChanged
    // ----------------------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
