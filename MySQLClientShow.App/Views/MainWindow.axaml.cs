using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MySQLClientShow.App.ViewModels;

namespace MySQLClientShow.App.Views;

public partial class MainWindow : Window
{
    private bool _closeAfterStop;
    private bool _stopInProgress;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeAfterStop || _stopInProgress)
        {
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel || !viewModel.IsRunning)
        {
            return;
        }

        e.Cancel = true;
        _stopInProgress = true;

        try
        {
            await viewModel.StopPollingForExitAsync();
        }
        catch
        {
            // Best effort stop during application exit.
        }
        finally
        {
            _stopInProgress = false;
        }

        _closeAfterStop = true;
        Close();
    }
}
