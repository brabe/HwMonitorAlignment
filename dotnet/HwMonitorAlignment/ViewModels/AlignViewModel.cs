using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using HwMonitorAlignment.Models;
using HwMonitorAlignment.Win32;

namespace HwMonitorAlignment.ViewModels;

/// <summary>
/// Shared view-model for the alignment mode.
/// A single instance is shared by all AlignWindow instances that are open simultaneously.
/// </summary>
public class AlignViewModel : INotifyPropertyChanged
{
    // ----------------------------------------------------------------
    // Monitor data
    // ----------------------------------------------------------------

    /// <summary>All active monitors (read-only from outside)</summary>
    public IReadOnlyList<MonitorInfo> Monitors { get; }

    /// <summary>The primary monitor (never null when Monitors is non-empty)</summary>
    public MonitorInfo? Primary => Monitors.FirstOrDefault(m => m.IsPrimary)
                                ?? Monitors.FirstOrDefault();

    // ----------------------------------------------------------------
    // Selection
    // ----------------------------------------------------------------

    private MonitorInfo? _selectedMonitor;

    /// <summary>
    /// The secondary monitor currently being adjusted.
    /// The primary monitor cannot be selected.
    /// </summary>
    public MonitorInfo? SelectedMonitor
    {
        get => _selectedMonitor;
        set
        {
            if (value != null && value.IsPrimary)
                return; // primary cannot be selected
            if (_selectedMonitor == value) return;
            _selectedMonitor = value;
            OnPropertyChanged();
        }
    }

    // ----------------------------------------------------------------
    // Line settings
    // ----------------------------------------------------------------

    private double _lineSpacing = 100.0;

    /// <summary>Spacing between horizontal alignment lines in physical pixels</summary>
    public double LineSpacing
    {
        get => _lineSpacing;
        set { if (_lineSpacing != value) { _lineSpacing = value; OnPropertyChanged(); } }
    }

    // ----------------------------------------------------------------
    // Mementos for rollback
    // ----------------------------------------------------------------

    // Saved original positions per DeviceName
    private readonly Dictionary<string, (int x, int y)> _originalPositions;

    // ----------------------------------------------------------------
    // Constructor
    // ----------------------------------------------------------------

    public AlignViewModel(IReadOnlyList<MonitorInfo> monitors)
    {
        Monitors = monitors;

        // Snapshot original positions for rollback
        _originalPositions = new Dictionary<string, (int, int)>(monitors.Count);
        foreach (var m in monitors)
            _originalPositions[m.DeviceName] = (m.X, m.Y);

        // Pre-select first secondary monitor (if any)
        SelectedMonitor = Monitors.FirstOrDefault(m => !m.IsPrimary);
    }

    // ----------------------------------------------------------------
    // Movement
    // ----------------------------------------------------------------

    /// <summary>
    /// Move the selected secondary monitor by (dx, dy) physical pixels.
    /// Does nothing if no secondary monitor is selected.
    /// </summary>
    public void MoveSelected(int dx, int dy)
    {
        if (SelectedMonitor == null || SelectedMonitor.IsPrimary)
            return;
        SelectedMonitor.X += dx;
        SelectedMonitor.Y += dy;
    }

    // ----------------------------------------------------------------
    // Apply / Reset
    // ----------------------------------------------------------------

    /// <summary>
    /// Applies current monitor positions to the OS via Win32.
    /// Returns true if the OS call succeeded.
    /// </summary>
    public bool ApplyChanges()
    {
        return DisplayBackend.ApplyMonitorPositions(Monitors);
    }

    /// <summary>
    /// Reverts all monitors to their original positions (in-memory only – no Win32 call).
    /// Call ApplyChanges() after this to also push the rollback to the OS.
    /// </summary>
    public void ResetPositions()
    {
        foreach (var m in Monitors)
        {
            if (_originalPositions.TryGetValue(m.DeviceName, out var orig))
            {
                m.X = orig.x;
                m.Y = orig.y;
            }
        }
    }

    /// <summary>
    /// Reverts positions AND applies them to the OS (used for automatic 15-s rollback).
    /// </summary>
    public bool RevertChanges()
    {
        ResetPositions();
        return ApplyChanges();
    }

    // ----------------------------------------------------------------
    // Virtual-screen geometry helpers (computed from monitor list)
    // ----------------------------------------------------------------

    /// <summary>
    /// Bounding rectangle that contains all monitors (in physical pixel coordinates).
    /// Returns (minX, minY, totalWidth, totalHeight).
    /// </summary>
    public (int minX, int minY, int width, int height) GetVirtualScreenBounds()
    {
        if (!Monitors.Any())
            return (0, 0, 1920, 1080);

        int minX = Monitors.Min(m => m.X);
        int minY = Monitors.Min(m => m.Y);
        int maxX = Monitors.Max(m => m.X + m.Width);
        int maxY = Monitors.Max(m => m.Y + m.Height);
        return (minX, minY, maxX - minX, maxY - minY);
    }

    // ----------------------------------------------------------------
    // INotifyPropertyChanged
    // ----------------------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
