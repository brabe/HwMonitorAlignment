using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace HwMonitorAlignment.Views;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TbGitHubLink_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName        = TbGitHubLink.Text,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not open browser:\n{ex.Message}",
                "HwMonitorAlignment",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
