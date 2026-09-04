using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DayZLauncher.App.ViewModels;
using DayZLauncher.Core.Models;

namespace DayZLauncher.App;

public partial class MainWindow : Window
{
    private const double SettingsPanelWidth = 600;

    private LogWindow? _logWindow;
    private readonly GlobalHotkeyManager _hotkeyManager;
    private BranchProfileViewModel? _observedProfile;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private bool _settingsPanelOpen;
    private readonly bool _isFirstLaunch;

    public MainWindow(bool isFirstLaunch = false)
    {
        InitializeComponent();
        _isFirstLaunch = isFirstLaunch;

        _hotkeyManager = new GlobalHotkeyManager(this);

        SourceInitialized += (_, _) => ApplyTitleBarTheme();
        if (DataContext is MainViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            RestoreWindowBounds(vm);
            ObserveProfile(vm);
            SourceInitialized += (_, _) => UpdateAllHotkeys(vm);
            vm.ModMissingSignatureKeys += OpenMissingKeysDialog;
            vm.ModAlreadyAdded += OpenDuplicateModDialog;
        }

        StateChanged += MainWindow_StateChanged;
        Closing += MainWindow_Closing;

        // ToolbarBorder's height isn't known until after the first layout pass - push the settings
        // panel down by that measured height so it starts right under the toolbar's bottom edge
        // instead of overlapping the Запустить/Логи row, while the scrim behind it (unaffected by
        // this margin) still covers the toolbar too.
        Loaded += (_, _) => SettingsPanelHost.Margin = new Thickness(0, ToolbarBorder.ActualHeight, 0, 0);

        if (_isFirstLaunch) Loaded += (_, _) => OpenFirstLaunchDialog();
    }

    /// <summary>Replaces a plain Show() call at startup - honors "Запускать свернуто". With
    /// MinimizeToTray also on, the window never becomes visible at all: it goes straight to the tray
    /// icon instead of flashing normal-then-minimized-then-hidden, which is what setting WindowState
    /// before the first Show() actually produces (confirmed by testing - the window ends up as a
    /// native minimized taskbar window, not hidden, because StateChanged only fires on a live
    /// transition, not from the initial startup state).</summary>
    public void ShowRespectingStartupPreferences()
    {
        if (DataContext is MainViewModel { StartMinimized: true, MinimizeToTray: true })
        {
            _trayIcon = CreateTrayIcon();
            _trayIcon.Visible = true;
            return;
        }

        Show();
        if (DataContext is MainViewModel { StartMinimized: true })
            WindowState = WindowState.Minimized;
    }

    /// <summary>Only the saved position is restored - the window is a fixed 1280x860 (ResizeMode
    /// prevents resizing), so its own current Width/Height are used instead of whatever was saved,
    /// in case an older settings.json still has a different size from before that was fixed. No saved
    /// bounds (first launch, or after "Сбросить настройки") falls back to centering - set here in code
    /// rather than via XAML's WindowStartupLocation="CenterScreen", which was found to re-center the
    /// window even after Left/Top were already set explicitly below, silently overriding every
    /// restored position on every launch.</summary>
    private void RestoreWindowBounds(MainViewModel vm)
    {
        if (!vm.TryGetWindowBounds(out var left, out var top, out _, out _))
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }
        WindowBoundsHelper.Restore(this, left, top, Width, Height);
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var bounds = WindowBoundsHelper.CaptureBounds(this);
        vm.SaveWindowBounds(bounds.Left, bounds.Top, bounds.Width, bounds.Height);

        // Only ever hidden on Close() (see LogWindow.OnClosing) so it can be reused instead of
        // recreated - actually tear it down here, once, for real.
        _logWindow?.AllowRealClose();
        _logWindow?.Close();

        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        // Explicit, rather than relying on the default OnLastWindowClose shutdown mode: when the app
        // starts minimized straight to the tray (ShowRespectingStartupPreferences), this window is
        // never Shown, so it's unclear whether WPF would still count it as "the last open window" and
        // shut down on its own once this Close() completes.
        System.Windows.Application.Current.Shutdown();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized) return;
        if (DataContext is not MainViewModel vm || !vm.MinimizeToTray) return;

        HideToTray();
    }

    private void HideToTray()
    {
        // Hiding to tray skips MainWindow_Closing entirely (Hide(), not Close()) - save bounds here
        // too, or a window moved/resized in this session and then sent to tray (via the minimize
        // button, or the "✕" button with CloseToTray on) would lose that change the moment the app
        // actually exits later from the tray, since Closing never ran to capture it.
        if (DataContext is MainViewModel vm)
        {
            var bounds = WindowBoundsHelper.CaptureBounds(this);
            vm.SaveWindowBounds(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        }

        _trayIcon ??= CreateTrayIcon();
        Hide();
        _trayIcon.Visible = true;
    }

    private void RestoreFromTray()
    {
        if (_trayIcon is not null) _trayIcon.Visible = false;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private System.Windows.Forms.NotifyIcon CreateTrayIcon()
    {
        var trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName!),
            Text = "DayZ Manager",
            Visible = false,
        };
        trayIcon.DoubleClick += (_, _) => RestoreFromTray();

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Открыть", null, (_, _) => RestoreFromTray());
        menu.Items.Add("Выход", null, (_, _) => Close());
        trayIcon.ContextMenuStrip = menu;

        return trayIcon;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Theme))
            ApplyTitleBarTheme();

        if (DataContext is not MainViewModel vm) return;

        if (e.PropertyName is nameof(MainViewModel.EmergencyStopEnabled) or nameof(MainViewModel.EmergencyStopHotkey)
            or nameof(MainViewModel.StartServerHotkeyEnabled) or nameof(MainViewModel.StartServerHotkey)
            or nameof(MainViewModel.StartClientHotkeyEnabled) or nameof(MainViewModel.StartClientHotkey))
        {
            UpdateAllHotkeys(vm);
        }

        if (e.PropertyName == nameof(MainViewModel.Profile))
        {
            ObserveProfile(vm);
            UpdateAllHotkeys(vm);
        }
    }

    /// <summary>Keeps track of the active branch's own PropertyChanged so the "Запустить клиент"
    /// hotkey can be re-evaluated when ChainClientAfterServerLaunch changes (it's disabled while
    /// chain-launch is on) - both when that flag flips on the current branch, and when the branch
    /// itself switches to a profile with a different value.</summary>
    private void ObserveProfile(MainViewModel vm)
    {
        if (_observedProfile is not null) _observedProfile.PropertyChanged -= OnProfilePropertyChanged;
        _observedProfile = vm.Profile;
        _observedProfile.PropertyChanged += OnProfilePropertyChanged;
    }

    private void OnProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BranchProfileViewModel.ChainClientAfterServerLaunch)) return;
        if (DataContext is not MainViewModel vm) return;

        // Turning chain-launch on makes the standalone "Запустить клиент" hotkey redundant (the
        // client starts on its own after the server does) - switch it off outright rather than just
        // leaving its checkbox disabled-but-still-checked.
        if (sender is BranchProfileViewModel { ChainClientAfterServerLaunch: true })
            vm.StartClientHotkeyEnabled = false;

        UpdateAllHotkeys(vm);
    }

    private void UpdateAllHotkeys(MainViewModel vm)
    {
        UpdateHotkey("emergency-stop", vm.EmergencyStopEnabled, vm.EmergencyStopHotkey, vm.EmergencyStop);
        UpdateHotkey("start-server", vm.StartServerHotkeyEnabled, vm.StartServerHotkey,
            () => vm.Profile.ToggleServerCommand.Execute(null));
        UpdateHotkey("start-client", vm.StartClientHotkeyEnabled && !vm.Profile.ChainClientAfterServerLaunch, vm.StartClientHotkey,
            () => vm.Profile.ToggleClientCommand.Execute(null));
    }

    private void UpdateHotkey(string slot, bool enabled, string hotkeyText, Action action)
    {
        _hotkeyManager.Unregister(slot);
        if (!enabled) return;
        if (!HotkeyFormat.TryParse(hotkeyText, out var modifiers, out var key)) return;

        _hotkeyManager.Register(slot, modifiers, key, action);
    }

    private void ApplyTitleBarTheme()
    {
        if (DataContext is MainViewModel vm)
            DarkTitleBar.Apply(this, vm.Theme == AppTheme.Gray);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel { MinimizeToTray: true, CloseToTray: true })
        {
            HideToTray();
            return;
        }
        Close();
    }

    private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        // Reused for the whole app session (see LogWindow.OnClosing) rather than recreated every
        // time - Show() on an already-created-but-hidden window just makes it visible again.
        _logWindow ??= new LogWindow(vm) { Owner = this };
        _logWindow.Show();
        _logWindow.Activate();
    }

    /// <summary>Opens the "⋮" dropdown - a plain Click handler rather than relying on right-click,
    /// since this is meant to be the normal, discoverable way to reach it. ContextMenu is a
    /// disconnected popup root: it never inherits DataContext from the button that owns it, and an
    /// ElementName binding on PlacementTarget silently fails to resolve too (same root cause) - that
    /// second one is why the menu was opening at the screen's top-left instead of under the button.
    /// Set both explicitly here rather than fighting it with XAML bindings. The button sits in the
    /// top-left corner now, so the XAML-declared Placement="Bottom" is enough on its own - the menu
    /// grows right/down from the button, no custom placement math needed.</summary>
    private void SettingsMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { ContextMenu: { } menu } button)
        {
            menu.DataContext = button.DataContext;
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }

    private void OpenSettingsMenuItem_Click(object sender, RoutedEventArgs e) => OpenSettingsPanel();

    /// <summary>Settings used to be a separate Window; it's now a UserControl (SettingsPanel) living
    /// right inside MainWindow's own tree, sliding out from the left edge over a dimming scrim
    /// instead of opening a second window. SettingsOverlay is Collapsed while closed so it can never
    /// intercept clicks meant for the toolbar/tabs underneath; it's made Visible for the duration of
    /// the open animation and Collapsed again only after the close animation finishes.</summary>
    private void OpenSettingsPanel()
    {
        if (_settingsPanelOpen) return;
        _settingsPanelOpen = true;

        SettingsOverlay.Visibility = Visibility.Visible;
        var animation = new DoubleAnimation(-SettingsPanelWidth, 0, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        SettingsPanelTransform.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    private void CloseSettingsPanel()
    {
        if (!_settingsPanelOpen) return;
        _settingsPanelOpen = false;

        var animation = new DoubleAnimation(0, -SettingsPanelWidth, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        animation.Completed += (_, _) => SettingsOverlay.Visibility = Visibility.Collapsed;
        SettingsPanelTransform.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    private void SettingsScrim_MouseDown(object sender, MouseButtonEventArgs e) => CloseSettingsPanel();

    /// <summary>The "no keys folder" warning - a small centered dialog behind the same full-coverage
    /// scrim as the settings drawer (see SettingsOverlay above), but with no slide animation and no
    /// click-outside-to-dismiss: only the OK button (or Escape) closes it, since it's meant to be
    /// acknowledged rather than brushed past.</summary>
    private void OpenMissingKeysDialog() => MissingKeysOverlay.Visibility = Visibility.Visible;

    private void CloseMissingKeysDialog() => MissingKeysOverlay.Visibility = Visibility.Collapsed;

    private void MissingKeysOkButton_Click(object sender, RoutedEventArgs e) => CloseMissingKeysDialog();

    /// <summary>The "already added" warning - same pattern as OpenMissingKeysDialog above.</summary>
    private void OpenDuplicateModDialog() => DuplicateModOverlay.Visibility = Visibility.Visible;

    private void CloseDuplicateModDialog() => DuplicateModOverlay.Visibility = Visibility.Collapsed;

    private void DuplicateModOkButton_Click(object sender, RoutedEventArgs e) => CloseDuplicateModDialog();

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e) => OpenAboutDialog();

    /// <summary>The "О программе" dialog - same pattern as OpenMissingKeysDialog above.</summary>
    private void OpenAboutDialog() => AboutOverlay.Visibility = Visibility.Visible;

    private void CloseAboutDialog() => AboutOverlay.Visibility = Visibility.Collapsed;

    private void AboutOkButton_Click(object sender, RoutedEventArgs e) => CloseAboutDialog();

    /// <summary>First-launch prompt - same dialog pattern as OpenMissingKeysDialog above, shown once
    /// right after the window loads when settings.json didn't exist yet (see MainWindow(bool) and
    /// App.xaml.cs). "Да" runs the same Steam auto-detection as each branch's own "Найти (Steam)"
    /// button, just for both branches' client and server in one go.</summary>
    private void OpenFirstLaunchDialog() => FirstLaunchOverlay.Visibility = Visibility.Visible;

    private void CloseFirstLaunchDialog() => FirstLaunchOverlay.Visibility = Visibility.Collapsed;

    private void FirstLaunchYesButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.StableProfile.DetectSteamCommand.Execute(null);
            vm.StableProfile.DetectServerSteamCommand.Execute(null);
            vm.ExperimentalProfile.DetectSteamCommand.Execute(null);
            vm.ExperimentalProfile.DetectServerSteamCommand.Execute(null);
        }
        CloseFirstLaunchDialog();
    }

    private void FirstLaunchNoButton_Click(object sender, RoutedEventArgs e) => CloseFirstLaunchDialog();

    private void BoostyLink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch { /* best effort */ }
        e.Handled = true;
    }

    private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        if (MissingKeysOverlay.Visibility == Visibility.Visible) { CloseMissingKeysDialog(); return; }
        if (DuplicateModOverlay.Visibility == Visibility.Visible) { CloseDuplicateModDialog(); return; }
        if (AboutOverlay.Visibility == Visibility.Visible) { CloseAboutDialog(); return; }
        if (FirstLaunchOverlay.Visibility == Visibility.Visible) { CloseFirstLaunchDialog(); return; }
        if (_settingsPanelOpen) CloseSettingsPanel();
    }
}
