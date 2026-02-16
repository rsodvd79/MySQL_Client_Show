using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using MySQLClientShow.App.Services;
using MySQLClientShow.App.ViewModels;
using MySQLClientShow.App.Views;

namespace MySQLClientShow.App;

public partial class App : Application
{
    private const string ApplicationDisplayName = "MySQL Client Show";
    private const string MacFileMenuHeader = "File";

    public App()
    {
        Name = ApplicationDisplayName;

        if (OperatingSystem.IsMacOS())
        {
            // Prevent Avalonia from auto-creating the default app menu with "About Avalonia".
            NativeMenu.SetMenu(this, new NativeMenu());
        }
    }

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

            ConfigureMacApplicationMenu(desktop);

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

    private static void ConfigureMacApplicationMenu(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var mainWindow = desktop.MainWindow;
        if (!OperatingSystem.IsMacOS() || mainWindow is null)
        {
            return;
        }

        ConfigureMacWindowMenu(desktop, mainWindow);
    }

    private static void ConfigureMacWindowMenu(IClassicDesktopStyleApplicationLifetime desktop, Window mainWindow)
    {
        if (mainWindow is not MainWindow appMainWindow)
        {
            return;
        }

        var fileMenuRoot = new NativeMenuItem(MacFileMenuHeader)
        {
            Menu = new NativeMenu()
        };

        if (appMainWindow.DataContext is MainWindowViewModel viewModel)
        {
            fileMenuRoot.Menu.Add(new NativeMenuItem("Start")
            {
                Command = viewModel.StartCommand
            });
            fileMenuRoot.Menu.Add(new NativeMenuItem("Stop")
            {
                Command = viewModel.StopCommand
            });
            fileMenuRoot.Menu.Add(new NativeMenuItem("Clear")
            {
                Command = viewModel.ClearCommand
            });
        }
        else
        {
            var startItem = new NativeMenuItem("Start");
            startItem.Click += async (_, _) => await appMainWindow.ExecuteStartFromMenuAsync();
            fileMenuRoot.Menu.Add(startItem);

            var stopItem = new NativeMenuItem("Stop");
            stopItem.Click += async (_, _) => await appMainWindow.ExecuteStopFromMenuAsync();
            fileMenuRoot.Menu.Add(stopItem);

            var clearItem = new NativeMenuItem("Clear");
            clearItem.Click += (_, _) => appMainWindow.ExecuteClearFromMenu();
            fileMenuRoot.Menu.Add(clearItem);
        }

        var exportCsvItem = new NativeMenuItem("Export CSV");
        exportCsvItem.Click += async (_, _) => await appMainWindow.ExecuteExportCsvFromMenuAsync();
        fileMenuRoot.Menu.Add(exportCsvItem);

        var helpItem = new NativeMenuItem("Help / Aiuto (?)");
        helpItem.Click += async (_, _) => await appMainWindow.ExecuteHelpFromMenuAsync();
        fileMenuRoot.Menu.Add(helpItem);

        fileMenuRoot.Menu.Add(new NativeMenuItemSeparator());

        var quitItem = new NativeMenuItem($"Quit {ApplicationDisplayName}")
        {
            Gesture = new KeyGesture(Key.Q, KeyModifiers.Meta)
        };
        quitItem.Click += (_, _) => _ = desktop.TryShutdown(0);

        fileMenuRoot.Menu.Add(quitItem);

        var menuBar = new NativeMenu();
        menuBar.Add(fileMenuRoot);

        NativeMenu.SetMenu(mainWindow, menuBar);
    }
}
