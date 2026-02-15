using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using MySQLClientShow.App.Models;
using MySQLClientShow.App.Utilities;

namespace MySQLClientShow.App.Views;

public partial class QueryDetailWindow : Window
{
    public QueryDetailWindow()
    {
        InitializeComponent();
        DataContext = new QueryDetailViewModel(string.Empty, string.Empty, string.Empty);
    }

    public QueryDetailWindow(GeneralLogEntry entry)
        : this()
    {
        var formattedSql = SqlQueryFormatter.Format(entry.SqlText);
        DataContext = new QueryDetailViewModel(
            entry.EventTime.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture),
            entry.UserHost,
            formattedSql);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void OnCopySqlClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not QueryDetailViewModel viewModel)
        {
            return;
        }

        if (Clipboard is null)
        {
            return;
        }

        await Clipboard.SetTextAsync(viewModel.FormattedSql);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private sealed record QueryDetailViewModel(string EventTimeText, string UserHost, string FormattedSql);
}
