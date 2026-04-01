using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using HwMonitorAlignment.Models;
using HwMonitorAlignment.ViewModels;

namespace HwMonitorAlignment.Views;

/// <summary>
/// Full-screen semi-transparent overlay window placed on one physical monitor.
/// One instance is created per monitor during alignment mode.
///
/// NOTE on DPI / positioning:
///   Window.Left / Window.Top are in WPF device-independent pixels (DIPs) at 96 DPI.
///   The DEVMODE positions returned by Win32 are in physical pixels.
///   On a 100%-scaling (96 DPI) system they are the same.
///   On HiDPI screens you must divide by the per-monitor DPI scale factor.
///   For simplicity this implementation uses the raw pixel values which is correct
///   at 100 % scale. A future improvement could query per-monitor DPI via
///   GetDpiForMonitor() and divide accordingly.
/// </summary>
public partial class AlignWindow : Window
{
    private readonly AlignViewModel _vm;
    private readonly MonitorInfo    _monitor;

    // Keep a reference to all sibling windows so we can close them together
    private static readonly List<AlignWindow> _allWindows = new();

    public AlignWindow(AlignViewModel vm, MonitorInfo monitor)
    {
        InitializeComponent();

        _vm      = vm;
        _monitor = monitor;

        // ---- Position and size the window on its physical monitor ----
        // Raw pixel values – correct at 96 DPI / 100 % scale.
        Left   = monitor.X;
        Top    = monitor.Y;
        Width  = monitor.Width;
        Height = monitor.Height;

        // ---- Info box labels ----
        TbMonitorLabel.Text = monitor.DisplayLabel;
        TbDeviceName.Text   = monitor.DeviceName;
        TbResolution.Text   = monitor.ResolutionText;

        if (monitor.IsPrimary)
        {
            TbPrimary.Text       = "PRIMARY";
            TbPrimary.Visibility = Visibility.Visible;
            ControlBox.Visibility = Visibility.Visible;
        }

        // ---- Listen for position changes ----
        monitor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MonitorInfo.X) ||
                e.PropertyName == nameof(MonitorInfo.Y) ||
                e.PropertyName == nameof(MonitorInfo.PositionText))
            {
                TbPosition.Text = monitor.PositionText;
                DrawLines();
            }
        };

        // ---- Listen for selection changes ----
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AlignViewModel.SelectedMonitor))
                UpdateSelectionHighlight();
        };

        // ---- Listen for line-spacing changes ----
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AlignViewModel.LineSpacing))
                DrawLines();
        };

        _allWindows.Add(this);

        Loaded += (_, _) =>
        {
            TbPosition.Text = monitor.PositionText;
            DrawLines();
            UpdateSelectionHighlight();
        };
    }

    // ----------------------------------------------------------------
    // Alignment line drawing
    // ----------------------------------------------------------------

    /// <summary>
    /// Draws horizontal alignment lines on LinesCanvas.
    /// Lines are positioned relative to the primary monitor's vertical center
    /// and spaced by LineSpacing pixels.  They are drawn in the coordinate
    /// system of THIS monitor's overlay window.
    /// </summary>
    private void DrawLines()
    {
        LinesCanvas.Children.Clear();

        var primary = _vm.Primary;
        if (primary == null) return;

        // Y position of primary's vertical center in virtual-screen coordinates
        double primaryCenterY = primary.Y + primary.Height / 2.0;

        // Line spacing in pixels
        double spacing = _vm.LineSpacing;
        if (spacing <= 0) spacing = 100;

        double monitorTop    = _monitor.Y;
        double monitorBottom = _monitor.Y + _monitor.Height;
        double monitorWidth  = _monitor.Width;
        double monitorHeight = _monitor.Height;

        // Choose a color based on whether this monitor is primary or secondary
        var lineColor = _monitor.IsPrimary
            ? Color.FromArgb(200, 255, 255, 255)
            : Color.FromArgb(160, 180, 210, 255);

        var pen = new SolidColorBrush(lineColor);
        pen.Freeze();

        // How many lines above and below center to cover the whole virtual screen height
        int linesNeeded = (int)(Math.Max(_vm.Monitors.Count > 0
            ? _vm.Monitors.Max(m => Math.Abs(m.Y - primaryCenterY) + m.Height)
            : monitorHeight, monitorHeight) / spacing) + 2;

        for (int n = -linesNeeded; n <= linesNeeded; n++)
        {
            // Virtual-screen Y of this line
            double lineVirtualY = primaryCenterY + n * spacing;

            // Convert to this window's local Y
            double lineLocalY = lineVirtualY - monitorTop;

            // Skip lines outside this window
            if (lineLocalY < -2 || lineLocalY > monitorHeight + 2)
                continue;

            bool isCenterLine = (n == 0);
            double lineThickness = isCenterLine ? 3.0 : 1.5;
            double lineOpacity   = isCenterLine ? 1.0 : 0.65;

            var line = new Line
            {
                X1              = 0,
                Y1              = lineLocalY,
                X2              = monitorWidth,
                Y2              = lineLocalY,
                Stroke          = pen,
                StrokeThickness = lineThickness,
                Opacity         = lineOpacity,
                IsHitTestVisible = false,
            };
            LinesCanvas.Children.Add(line);

            // Tick marks every major line on the center line
            if (isCenterLine)
            {
                // Small vertical tick in the middle
                var tick = new Line
                {
                    X1              = monitorWidth / 2 - 12,
                    Y1              = lineLocalY - 8,
                    X2              = monitorWidth / 2 - 12,
                    Y2              = lineLocalY + 8,
                    Stroke          = pen,
                    StrokeThickness = 1.5,
                    Opacity         = 0.7,
                    IsHitTestVisible = false,
                };
                LinesCanvas.Children.Add(tick);
            }
        }
    }

    // ----------------------------------------------------------------
    // Selection highlight
    // ----------------------------------------------------------------

    private void UpdateSelectionHighlight()
    {
        bool isSelected = (_vm.SelectedMonitor == _monitor);
        SelectionBorder.Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed;

        // Slightly lighter background tint when selected
        Background = isSelected
            ? new SolidColorBrush(Color.FromArgb(0xCC, 0x00, 0x1A, 0x33))
            : new SolidColorBrush(Color.FromArgb(0xCC, 0x00, 0x00, 0x00));
    }

    // ----------------------------------------------------------------
    // Input handling
    // ----------------------------------------------------------------

    private void Window_Activated(object sender, EventArgs e)
    {
        // Redraw when window gains focus (e.g. after another window was clicked)
        DrawLines();
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_monitor.IsPrimary)
        {
            _vm.SelectedMonitor = _monitor;
        }
        // Ensure this window has keyboard focus for arrow-key events
        Focus();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                CloseAll();
                e.Handled = true;
                break;

            case Key.Up:
                // In Qt the Y axis is "up=positive"; in Win32 virtual screen coordinates
                // Y increases downward.  The Python code adds 1 for Up and subtracts for Down.
                // We do the same: pressing Up moves the monitor upward = decreasing Y.
                // But the Python source says: Up -> position_y += 1 (which is confusing).
                // Looking at the Python source: Up adds 1 to position_y which moves DOWN visually
                // because in Qt the scene y-axis is inverted for the overlay.
                // In Win32 (and WPF) Y increases downward, so Up key = Y -= 1.
                _vm.MoveSelected(0, -1);
                e.Handled = true;
                break;

            case Key.Down:
                _vm.MoveSelected(0, +1);
                e.Handled = true;
                break;

            case Key.Left:
                _vm.MoveSelected(-1, 0);
                e.Handled = true;
                break;

            case Key.Right:
                _vm.MoveSelected(+1, 0);
                e.Handled = true;
                break;

            case Key.PageUp:
                _vm.MoveSelected(0, -10);
                e.Handled = true;
                break;

            case Key.PageDown:
                _vm.MoveSelected(0, +10);
                e.Handled = true;
                break;
        }
    }

    // ----------------------------------------------------------------
    // Control box button handlers (primary monitor only)
    // ----------------------------------------------------------------

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        // Apply changes to OS
        bool ok = _vm.ApplyChanges();

        if (!ok)
        {
            MessageBox.Show(
                "ChangeDisplaySettingsEx did not return DISP_CHANGE_SUCCESSFUL.\n" +
                "The display configuration may not have been saved.",
                "HwMonitorAlignment – Warning",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        // Show rollback dialog with 15-second countdown
        var rollbackDlg = new RollbackDialog { Topmost = true };
        bool keepChanges = rollbackDlg.ShowAndWait();

        if (!keepChanges)
        {
            // Revert
            _vm.RevertChanges();
        }

        // Close all align windows regardless of choice
        CloseAll();
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        _vm.ResetPositions();
        DrawLines();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        CloseAll();
    }

    // ----------------------------------------------------------------
    // Line spacing text box
    // ----------------------------------------------------------------

    private void TbLineSpacing_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (double.TryParse(TbLineSpacing.Text, out double val) && val >= 20 && val <= 960)
        {
            _vm.LineSpacing = val;
        }
    }

    // ----------------------------------------------------------------
    // Close all sibling windows
    // ----------------------------------------------------------------

    private void CloseAll()
    {
        // Copy list because closing modifies _allWindows
        var windows = new List<AlignWindow>(_allWindows);
        foreach (var w in windows)
        {
            try { w.Close(); } catch { /* ignore */ }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _allWindows.Remove(this);
        base.OnClosed(e);
    }
}
