using System.ComponentModel;
using System.Windows;
using DayZLauncher.App.ViewModels;
using DayZLauncher.Core.Models;

namespace DayZLauncher.App;

public partial class LogWindow : Window
{
    private readonly MainViewModel _mainViewModel;
    private bool _allowRealClose;

    public LogWindow(MainViewModel mainViewModel)
    {
        InitializeComponent();
        _mainViewModel = mainViewModel;
        DataContext = mainViewModel.Logs;

        mainViewModel.Logs.ContentAppended += OnContentAppended;
        mainViewModel.PropertyChanged += OnMainViewModelPropertyChanged;
        Closed += (_, _) =>
        {
            mainViewModel.Logs.ContentAppended -= OnContentAppended;
            mainViewModel.PropertyChanged -= OnMainViewModelPropertyChanged;
        };

        // No saved bounds (first open) falls back to centering, set here in code rather than via
        // XAML's WindowStartupLocation - see MainWindow.RestoreWindowBounds for why.
        if (_mainViewModel.TryGetLogWindowBounds(out var left, out var top, out var width, out var height))
        {
            WindowBoundsHelper.Restore(this, left, top, width, height);
            if (_mainViewModel.LogWindowMaximized) WindowState = WindowState.Maximized;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        Closing += OnClosing;

        SourceInitialized += (_, _) => ApplyTitleBarTheme();
        SourceInitialized += (_, _) => MaximizeWorkAreaFix.Apply(this);
    }

    /// <summary>Closing this window (the [X], Alt+F4, or Escape) just hides it instead of actually
    /// destroying it - this instance is reused for the whole app session. Repeatedly creating a new
    /// LogWindow every time it was closed/reopened eventually broke the Client/Server RadioButton
    /// toggle after a handful of cycles (confirmed by automated testing: reliably reproducible by
    /// the 5th-6th reopen) - the two RadioButtons share GroupName="logsource", and WPF's grouping
    /// mechanism apparently doesn't cope well with many short-lived windows registering RadioButtons
    /// under the same group name over and over. Call <see cref="AllowRealClose"/> before Close() to
    /// actually tear it down (only done once, at app shutdown).</summary>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        var bounds = WindowBoundsHelper.CaptureBounds(this);
        _mainViewModel.SaveLogWindowBounds(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        _mainViewModel.LogWindowMaximized = WindowState == WindowState.Maximized;

        if (_allowRealClose) return;
        e.Cancel = true;
        Hide();
    }

    public void AllowRealClose() => _allowRealClose = true;

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Theme))
            ApplyTitleBarTheme();
    }

    private void ApplyTitleBarTheme() => DarkTitleBar.Apply(this, _mainViewModel.Theme == AppTheme.Gray);

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void OnContentAppended()
    {
        if (DataContext is LogsViewModel { AutoScroll: true })
            LogTextBox.ScrollToEnd();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape) Close();
    }
}
