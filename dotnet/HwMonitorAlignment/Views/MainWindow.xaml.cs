using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using HwMonitorAlignment.Models;
using HwMonitorAlignment.ViewModels;

namespace HwMonitorAlignment.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    // Track open align windows so we can close them if the main window closes first
    private List<AlignWindow>? _alignWindows;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        Loaded += (_, _) => DrawOverview();
    }

    // ----------------------------------------------------------------
    // Overview canvas drawing
    // ----------------------------------------------------------------

    private void DrawOverview()
    {
        OverviewCanvas.Children.Clear();

        if (_vm.Monitors.Count == 0 || _vm.VScreenWidth == 0 || _vm.VScreenHeight == 0)
            return;

        double canvasW = OverviewCanvas.ActualWidth;
        double canvasH = OverviewCanvas.ActualHeight;
        if (canvasW <= 0 || canvasH <= 0) return;

        // Compute uniform scale so the entire virtual screen fits the canvas with padding
        const double padding = 12;
        double scaleX = (canvasW - padding * 2) / _vm.VScreenWidth;
        double scaleY = (canvasH - padding * 2) / _vm.VScreenHeight;
        double scale  = Math.Min(scaleX, scaleY);

        // Center the scaled virtual screen on the canvas
        double scaledW = _vm.VScreenWidth  * scale;
        double scaledH = _vm.VScreenHeight * scale;
        double offsetX = (canvasW - scaledW) / 2.0 - _vm.VScreenX * scale;
        double offsetY = (canvasH - scaledH) / 2.0 - _vm.VScreenY * scale;

        foreach (var monitor in _vm.Monitors)
        {
            double rx = monitor.X * scale + offsetX;
            double ry = monitor.Y * scale + offsetY;
            double rw = monitor.Width  * scale;
            double rh = monitor.Height * scale;

            // Monitor rectangle
            var rect = new Rectangle
            {
                Width           = rw,
                Height          = rh,
                Fill            = monitor.IsPrimary
                                    ? new SolidColorBrush(Color.FromArgb(200, 0, 122, 204))
                                    : new SolidColorBrush(Color.FromArgb(180, 45, 45, 48)),
                Stroke          = monitor.IsPrimary
                                    ? Brushes.CornflowerBlue
                                    : new SolidColorBrush(Color.FromRgb(100, 100, 110)),
                StrokeThickness = 1.5,
                RadiusX         = 3,
                RadiusY         = 3,
            };
            Canvas.SetLeft(rect, rx);
            Canvas.SetTop(rect,  ry);
            OverviewCanvas.Children.Add(rect);

            // Monitor label
            var label = new TextBlock
            {
                Text            = monitor.DisplayLabel,
                Foreground      = Brushes.White,
                FontSize        = Math.Max(7, Math.Min(12, rw / 12)),
                FontFamily      = new FontFamily("Segoe UI"),
                TextWrapping    = TextWrapping.Wrap,
                TextAlignment   = TextAlignment.Center,
                MaxWidth        = rw - 4,
                MaxHeight       = rh - 4,
            };
            // Position label centered inside the rectangle
            label.Measure(new Size(rw, rh));
            double lx = rx + (rw - label.DesiredSize.Width)  / 2;
            double ly = ry + (rh - label.DesiredSize.Height) / 2;
            Canvas.SetLeft(label, lx);
            Canvas.SetTop(label,  ly);
            OverviewCanvas.Children.Add(label);

            // Resolution sub-label
            var resLabel = new TextBlock
            {
                Text       = monitor.ResolutionText,
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                FontSize   = Math.Max(6, Math.Min(9, rw / 18)),
                FontFamily = new FontFamily("Segoe UI"),
                TextAlignment = TextAlignment.Center,
                MaxWidth   = rw - 4,
            };
            resLabel.Measure(new Size(rw, rh));
            double rlx = rx + (rw - resLabel.DesiredSize.Width) / 2;
            double rly = ly + label.DesiredSize.Height + 2;
            Canvas.SetLeft(resLabel, rlx);
            Canvas.SetTop(resLabel,  rly);
            OverviewCanvas.Children.Add(resLabel);
        }
    }

    private void OverviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawOverview();
    }

    // ----------------------------------------------------------------
    // Button handlers
    // ----------------------------------------------------------------

    private void BtnAdjust_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Monitors.Count == 0)
        {
            MessageBox.Show("No monitors detected.", "HwMonitorAlignment",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Build a snapshot list for AlignViewModel (working copies of positions)
        var vm = new AlignViewModel(_vm.Monitors);

        // Open one full-screen overlay per monitor
        _alignWindows = new List<AlignWindow>();
        foreach (var monitor in _vm.Monitors)
        {
            var win = new AlignWindow(vm, monitor);
            _alignWindows.Add(win);
        }

        // Show all windows
        foreach (var win in _alignWindows)
            win.Show();
    }

    private void BtnAbout_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AboutDialog { Owner = this };
        dlg.ShowDialog();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // ----------------------------------------------------------------
    // Cleanup
    // ----------------------------------------------------------------

    protected override void OnClosed(EventArgs e)
    {
        if (_alignWindows != null)
        {
            foreach (var win in _alignWindows)
            {
                try { win.Close(); } catch { /* ignore */ }
            }
        }
        base.OnClosed(e);
    }
}
