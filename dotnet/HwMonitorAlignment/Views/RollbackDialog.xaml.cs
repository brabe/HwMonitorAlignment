using System;
using System.Windows;
using System.Windows.Threading;

namespace HwMonitorAlignment.Views;

/// <summary>
/// Modal dialog that shows a 15-second countdown.
/// If the user clicks "Keep Changes" the dialog closes with result = true.
/// If the user clicks "Revert" OR the timer reaches zero the result is false.
///
/// Use ShowAndWait() instead of ShowDialog() for a clean bool return value.
/// </summary>
public partial class RollbackDialog : Window
{
    private const int TimeoutSeconds = 15;

    private readonly DispatcherTimer _timer;
    private int _secondsRemaining;
    private bool _keepChanges;

    public RollbackDialog()
    {
        InitializeComponent();

        _secondsRemaining = TimeoutSeconds;
        PbCountdown.Maximum = TimeoutSeconds;
        PbCountdown.Value   = TimeoutSeconds;
        UpdateCountdownLabel();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;
    }

    // ----------------------------------------------------------------
    // Public API
    // ----------------------------------------------------------------

    /// <summary>
    /// Shows the dialog as a modal window and blocks until it closes.
    /// Returns true if the user clicked "Keep Changes", false otherwise.
    /// </summary>
    public bool ShowAndWait()
    {
        _timer.Start();
        ShowDialog();   // blocks here
        return _keepChanges;
    }

    // ----------------------------------------------------------------
    // Timer
    // ----------------------------------------------------------------

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _secondsRemaining--;
        PbCountdown.Value = _secondsRemaining;
        UpdateCountdownLabel();

        if (_secondsRemaining <= 0)
        {
            _timer.Stop();
            _keepChanges = false;
            Close();
        }
    }

    private void UpdateCountdownLabel()
    {
        TbCountdown.Text = $"Reverting in {_secondsRemaining} second{(_secondsRemaining == 1 ? "" : "s")}…";
    }

    // ----------------------------------------------------------------
    // Button handlers
    // ----------------------------------------------------------------

    private void BtnKeep_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _keepChanges = true;
        Close();
    }

    private void BtnRevert_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _keepChanges = false;
        Close();
    }

    // ----------------------------------------------------------------
    // Cleanup
    // ----------------------------------------------------------------

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }
}
