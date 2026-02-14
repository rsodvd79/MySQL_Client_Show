using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MySQLClientShow.App.Services;
using MySQLClientShow.App.ViewModels;
using MySQLClientShow.App.Views;

namespace MySQLClientShow.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var configurationStore = new JsonAppConfigurationStore();
            var configuration = configurationStore.Load();
            var mainWindowViewModel = new MainWindowViewModel();
            mainWindowViewModel.ApplyConfiguration(configuration);

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel
            };

            desktop.Exit += (_, _) =>
            {
                try
                {
                    mainWindowViewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
                catch
                {
                    // Best effort shutdown.
                }

                try
                {
                    configurationStore.Save(mainWindowViewModel.BuildConfiguration());
                }
                catch
                {
                    // Best effort persistence.
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
