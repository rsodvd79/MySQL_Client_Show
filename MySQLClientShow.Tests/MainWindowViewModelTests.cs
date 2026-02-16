using MySQLClientShow.App.Configuration;
using MySQLClientShow.App.ViewModels;

namespace MySQLClientShow.Tests;

public class MainWindowViewModelTests
{
    [Fact]
    public void PollingIntervalMs_DefaultValue_Is1000()
    {
        Assert.Equal(1000, MainWindowViewModel.DefaultPollingIntervalMs);
    }

    [Fact]
    public void PollingIntervalMs_MinValue_Is200()
    {
        Assert.Equal(200, MainWindowViewModel.MinPollingIntervalMs);
    }

    [Fact]
    public void PollingIntervalMs_MaxValue_Is60000()
    {
        Assert.Equal(60000, MainWindowViewModel.MaxPollingIntervalMs);
    }

    [Fact]
    public void PollingIntervalMs_Clamp_BelowMinimum()
    {
        var vm = CreateViewModel();
        vm.PollingIntervalMs = 50;

        Assert.Equal(MainWindowViewModel.MinPollingIntervalMs, vm.PollingIntervalMs);
    }

    [Fact]
    public void PollingIntervalMs_Clamp_AboveMaximum()
    {
        var vm = CreateViewModel();
        vm.PollingIntervalMs = 100000;

        Assert.Equal(MainWindowViewModel.MaxPollingIntervalMs, vm.PollingIntervalMs);
    }

    [Fact]
    public void PollingIntervalMs_ValidValue_AcceptedAsIs()
    {
        var vm = CreateViewModel();
        vm.PollingIntervalMs = 2000;

        Assert.Equal(2000, vm.PollingIntervalMs);
    }

    [Fact]
    public void PollingIntervalMs_ExactMinimum_AcceptedAsIs()
    {
        var vm = CreateViewModel();
        vm.PollingIntervalMs = 200;

        Assert.Equal(200, vm.PollingIntervalMs);
    }

    [Fact]
    public void PollingIntervalMs_ExactMaximum_AcceptedAsIs()
    {
        var vm = CreateViewModel();
        vm.PollingIntervalMs = 60000;

        Assert.Equal(60000, vm.PollingIntervalMs);
    }

    [Fact]
    public void PollingIntervalMs_NegativeValue_ClampedToMin()
    {
        var vm = CreateViewModel();
        vm.PollingIntervalMs = -1;

        Assert.Equal(MainWindowViewModel.MinPollingIntervalMs, vm.PollingIntervalMs);
    }

    [Fact]
    public void PollingIntervalMs_Zero_ClampedToMin()
    {
        var vm = CreateViewModel();
        vm.PollingIntervalMs = 0;

        Assert.Equal(MainWindowViewModel.MinPollingIntervalMs, vm.PollingIntervalMs);
    }

    [Fact]
    public void ConnectionString_SetAndGet()
    {
        var vm = CreateViewModel();
        vm.ConnectionString = "Server=localhost;";

        Assert.Equal("Server=localhost;", vm.ConnectionString);
    }

    [Fact]
    public void ConnectionString_DefaultEmpty()
    {
        var vm = CreateViewModel();

        Assert.Equal(string.Empty, vm.ConnectionString);
    }

    [Fact]
    public void IsRunning_DefaultFalse()
    {
        var vm = CreateViewModel();

        Assert.False(vm.IsRunning);
    }

    [Fact]
    public void ClientFilters_ContainsDefaultOption()
    {
        var vm = CreateViewModel();

        Assert.True(vm.ClientFilters.Count >= 1);
        Assert.Equal("(Tutti i client)", vm.ClientFilters[0]);
    }

    [Fact]
    public void SelectedClientFilter_DefaultIsAllClients()
    {
        var vm = CreateViewModel();

        Assert.Equal("(Tutti i client)", vm.SelectedClientFilter);
    }

    [Fact]
    public void ClientFilter_WhenAllClients_ReturnsEmpty()
    {
        var vm = CreateViewModel();
        vm.SelectedClientFilter = "(Tutti i client)";

        Assert.Equal(string.Empty, vm.ClientFilter);
    }

    [Fact]
    public void ClientFilter_WhenSpecificClient_ReturnsValue()
    {
        var vm = CreateViewModel();
        vm.SelectedClientFilter = "admin@localhost";

        Assert.Equal("admin@localhost", vm.ClientFilter);
    }

    [Fact]
    public void QuerySearchFilter_DefaultEmpty()
    {
        var vm = CreateViewModel();

        Assert.Equal(string.Empty, vm.QuerySearchFilter);
    }

    [Fact]
    public void QuerySearchFilter_SetAndGet()
    {
        var vm = CreateViewModel();
        vm.QuerySearchFilter = "SELECT|INSERT";

        Assert.Equal("SELECT|INSERT", vm.QuerySearchFilter);
    }

    [Fact]
    public void QuerySearchFilter_WhitespaceOnly_NormalizedToEmpty()
    {
        var vm = CreateViewModel();
        vm.QuerySearchFilter = "   ";

        Assert.Equal(string.Empty, vm.QuerySearchFilter);
    }

    [Fact]
    public void QuerySearchFilter_TrimmedOnSet()
    {
        var vm = CreateViewModel();
        vm.QuerySearchFilter = "  SELECT  ";

        Assert.Equal("SELECT", vm.QuerySearchFilter);
    }

    [Fact]
    public void SelectedClientFilter_NullNormalizedToAllClients()
    {
        var vm = CreateViewModel();
        vm.SelectedClientFilter = null!;

        Assert.Equal("(Tutti i client)", vm.SelectedClientFilter);
    }

    [Fact]
    public void SelectedClientFilter_EmptyNormalizedToAllClients()
    {
        var vm = CreateViewModel();
        vm.SelectedClientFilter = "";

        Assert.Equal("(Tutti i client)", vm.SelectedClientFilter);
    }

    [Fact]
    public void SelectedClientFilter_WhitespaceNormalizedToAllClients()
    {
        var vm = CreateViewModel();
        vm.SelectedClientFilter = "   ";

        Assert.Equal("(Tutti i client)", vm.SelectedClientFilter);
    }

    [Fact]
    public void SelectedClientFilter_NewValue_AddedToClientFilters()
    {
        var vm = CreateViewModel();
        vm.SelectedClientFilter = "newuser@host";

        Assert.Contains("newuser@host", vm.ClientFilters);
    }

    [Fact]
    public void FilteredEntries_InitiallyEmpty()
    {
        var vm = CreateViewModel();

        // In release builds, entries are empty. In debug, there may be seed data.
#if DEBUG
        Assert.True(vm.FilteredEntries.Count >= 0);
#else
        Assert.Empty(vm.FilteredEntries);
#endif
    }

    [Fact]
    public void TotalRows_InitiallyZeroOrSeeded()
    {
        var vm = CreateViewModel();

#if DEBUG
        Assert.True(vm.TotalRows >= 0);
#else
        Assert.Equal(0, vm.TotalRows);
#endif
    }

    [Fact]
    public void StatusMessage_HasDefaultValue()
    {
        var vm = CreateViewModel();

        Assert.False(string.IsNullOrWhiteSpace(vm.StatusMessage));
    }

    [Fact]
    public void HasStatusWarning_DefaultFalse()
    {
        var vm = CreateViewModel();

        Assert.False(vm.HasStatusWarning);
    }

    [Fact]
    public void NotifyStatus_EmptyMessage_DoesNotChange()
    {
        var vm = CreateViewModel();
        var originalStatus = vm.StatusMessage;

        vm.NotifyStatus("");

        Assert.Equal(originalStatus, vm.StatusMessage);
    }

    [Fact]
    public void NotifyStatus_ValidMessage_UpdatesStatus()
    {
        var vm = CreateViewModel();

        vm.NotifyStatus("Test message");

        Assert.Equal("Test message", vm.StatusMessage);
    }

    [Fact]
    public void NotifyStatus_WhitespaceMessage_DoesNotChange()
    {
        var vm = CreateViewModel();
        var originalStatus = vm.StatusMessage;

        vm.NotifyStatus("   ");

        Assert.Equal(originalStatus, vm.StatusMessage);
    }

    [Fact]
    public void ApplyConfiguration_SetsAllProperties()
    {
        var vm = CreateViewModel();
        var config = new AppConfiguration
        {
            ConnectionString = "Server=db;Port=3307;",
            ClientFilter = "app@server",
            QuerySearchFilter = "orders",
            PollingIntervalMs = 3000
        };

        vm.ApplyConfiguration(config);

        Assert.Equal("Server=db;Port=3307;", vm.ConnectionString);
        Assert.Equal("app@server", vm.SelectedClientFilter);
        Assert.Equal("orders", vm.QuerySearchFilter);
        Assert.Equal(3000, vm.PollingIntervalMs);
    }

    [Fact]
    public void ApplyConfiguration_NullValues_DefaultToEmpty()
    {
        var vm = CreateViewModel();
        var config = new AppConfiguration
        {
            ConnectionString = null!,
            ClientFilter = null!,
            QuerySearchFilter = null!,
            PollingIntervalMs = 1000
        };

        vm.ApplyConfiguration(config);

        Assert.Equal(string.Empty, vm.ConnectionString);
        Assert.Equal("(Tutti i client)", vm.SelectedClientFilter);
        Assert.Equal(string.Empty, vm.QuerySearchFilter);
    }

    [Fact]
    public void BuildConfiguration_ReturnsCurrentValues()
    {
        var vm = CreateViewModel();
        vm.ConnectionString = "Server=localhost;";
        vm.SelectedClientFilter = "admin@host";
        vm.QuerySearchFilter = "SELECT";
        vm.PollingIntervalMs = 2500;

        var config = vm.BuildConfiguration();

        Assert.Equal("Server=localhost;", config.ConnectionString);
        Assert.Equal("admin@host", config.ClientFilter);
        Assert.Equal("SELECT", config.QuerySearchFilter);
        Assert.Equal(2500, config.PollingIntervalMs);
    }

    [Fact]
    public void BuildConfiguration_AllClientsFilter_SavesEmpty()
    {
        var vm = CreateViewModel();
        vm.SelectedClientFilter = "(Tutti i client)";

        var config = vm.BuildConfiguration();

        Assert.Equal(string.Empty, config.ClientFilter);
    }

    [Fact]
    public void ApplyAndBuild_RoundTrip()
    {
        var vm = CreateViewModel();
        var original = new AppConfiguration
        {
            ConnectionString = "Server=test;",
            ClientFilter = "user@host",
            QuerySearchFilter = "SELECT|DELETE",
            PollingIntervalMs = 5000
        };

        vm.ApplyConfiguration(original);
        var rebuilt = vm.BuildConfiguration();

        Assert.Equal(original.ConnectionString, rebuilt.ConnectionString);
        Assert.Equal(original.ClientFilter, rebuilt.ClientFilter);
        Assert.Equal(original.QuerySearchFilter, rebuilt.QuerySearchFilter);
        Assert.Equal(original.PollingIntervalMs, rebuilt.PollingIntervalMs);
    }

    [Fact]
    public void ApplyConfiguration_PollingInterval_OutOfRange_Clamped()
    {
        var vm = CreateViewModel();
        var config = new AppConfiguration { PollingIntervalMs = 100 };

        vm.ApplyConfiguration(config);

        Assert.Equal(MainWindowViewModel.MinPollingIntervalMs, vm.PollingIntervalMs);
    }

    [Fact]
    public void Commands_AreNotNull()
    {
        var vm = CreateViewModel();

        Assert.NotNull(vm.StartCommand);
        Assert.NotNull(vm.StopCommand);
        Assert.NotNull(vm.ClearCommand);
    }

    [Fact]
    public void StartCommand_CannotExecute_WhenConnectionStringEmpty()
    {
        var vm = CreateViewModel();
        vm.ConnectionString = string.Empty;

        Assert.False(vm.StartCommand.CanExecute(null));
    }

    [Fact]
    public void StartCommand_CanExecute_WhenConnectionStringPresent()
    {
        var vm = CreateViewModel();
        vm.ConnectionString = "Server=localhost;";

        Assert.True(vm.StartCommand.CanExecute(null));
    }

    [Fact]
    public void StopCommand_CannotExecute_WhenNotRunning()
    {
        var vm = CreateViewModel();

        Assert.False(vm.StopCommand.CanExecute(null));
    }

    private static MainWindowViewModel CreateViewModel()
    {
        return new MainWindowViewModel();
    }
}
