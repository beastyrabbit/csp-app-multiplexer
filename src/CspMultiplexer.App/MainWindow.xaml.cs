using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using CspMultiplexer.Broker;
using CspMultiplexer.Protocol;

namespace CspMultiplexer.App;

/// <summary>
/// The one axis the window's appearance turns on. State used to be inferred from
/// <c>multiplexer is not null</c> plus a colour-struct comparison, which does not survive
/// the move to WPF: brushes resolved from a ResourceDictionary compare by reference.
/// </summary>
internal enum ConnectionState
{
    Idle,
    Scanning,
    Connecting,
    Online,
    QrHidden,
    Failed,
}

internal enum StatusTone
{
    Neutral,
    Busy,
    Good,
    Bad,
}

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const string FailureStatusText = "Connection failed";

    private readonly TrayHost tray;
    private readonly SemaphoreSlim stopGate = new(1, 1);

    private AppPreferences preferences;
    private IPAddress selectedAddress;
    private bool hideQrAfterFirstConnection;
    private CancellationTokenSource? operationCancellation;
    private UpstreamCompanionClient? upstream;
    private CompanionMultiplexer? multiplexer;
    private ConnectionState state;
    private string readyActivityDetail = string.Empty;
    private string failureDetail = string.Empty;
    private string failureInstruction = string.Empty;
    private Exception? upstreamDisconnect;
    private int clientCount;
    private bool isBusy;
    private bool loadingSettings;
    private bool stopping;
    private bool closingAfterCleanup;
    private bool closeInProgress;
    private bool exitRequested;
    private bool hiddenToTray;

    internal MainWindow(TrayHost trayHost)
    {
        ArgumentNullException.ThrowIfNull(trayHost);

        tray = trayHost;
        InitializeComponent();
        DataContext = this;
        preferences = AppPreferences.Load();
        selectedAddress = preferences.ParseAddress();
        hideQrAfterFirstConnection = preferences.HideQrAfterFirstConnection;
        AutoHideQrToggle.IsChecked = hideQrAfterFirstConnection;
        TrayModeToggle.IsChecked = preferences.RunInTray;
        ShowAboutText();
        ShowSettingsPath();

        tray.ShowRequested += (_, _) => ShowFromTray();
        tray.HideRequested += (_, _) => HideToTray();
        tray.SettingsRequested += OnTraySettingsRequested;
        tray.ExitRequested += (_, _) => RequestExit();

        // ApplyState is the only UI mutation entry point, and calling it here is what
        // deletes the launch-versus-idle divergence the old app booted with.
        ApplyState(ConnectionState.Idle);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Drives the client-count pill's zero-state trigger.</summary>
    public int ClientCount
    {
        get => clientCount;
        private set
        {
            if (clientCount == value)
            {
                return;
            }

            clientCount = value;
            ClientCountText.Text = $"{value} {(value == 1 ? "app" : "apps")}";
            OnPropertyChanged(nameof(ClientCount));
        }
    }

    /// <summary>
    /// The listen address is only read when <see cref="CompanionMultiplexerOptions"/> is
    /// constructed, so a scope change during a session would silently do nothing.
    /// </summary>
    public bool IsStopped => multiplexer is null;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        NativeMethods.ApplyRoundedCorners(new WindowInteropHelper(this).Handle);

        // Before Loaded, so a second launch fired during startup is received rather than
        // dropped; TrayHost buffers it until Attach.
        tray.ReattachActivationHook(this);
        RestoreWindowPosition();
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        if (closingAfterCleanup)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;

        // All four conditions are load-bearing. !exitRequested is what keeps a log-off or
        // a tray Exit out of this branch: those close paths ignore e.Cancel, so returning
        // here would abandon the multiplexer and the upstream CSP link every connected
        // app shares.
        if (preferences.RunInTray && !exitRequested && IsVisible && !closeInProgress)
        {
            HideToTray();
            return;
        }

        // IsEnabled greys the chrome buttons but blocks neither Alt+F4 nor SC_CLOSE.
        if (closeInProgress)
        {
            return;
        }

        closeInProgress = true;
        IsEnabled = false;

        SaveWindowPosition();
        await StopAsync().ConfigureAwait(true);

        // Before the dispatcher hop, never after Close(): a disposal that runs after the
        // window is gone can leave the icon behind.
        tray.Dispose();
        closingAfterCleanup = true;

        // StopAsync contains no awaits at all when nothing was ever connected, so the
        // await above resumes synchronously and Close() would re-enter OnClosing. WinForms
        // tolerated that; WPF throws. The dispatcher hop is what breaks the recursion.
        await Dispatcher.Yield(DispatcherPriority.Background);
        Close();
    }

    /// <summary>
    /// Disarms the tray branch in <see cref="OnClosing"/>. Called by every close that is
    /// not a user closing a visible window: log-off, and the second-instance shutdown.
    /// </summary>
    internal void MarkExitRequested() => exitRequested = true;

    private static SolidColorBrush Brush(string key) =>
        (SolidColorBrush)Application.Current.Resources[key];

    private static string AuthenticationReason(string message)
    {
        var separator = message.IndexOf(':', StringComparison.Ordinal);
        return separator < 0 ? message : message[(separator + 1)..].Trim();
    }

    private void RequestExit()
    {
        exitRequested = true;
        Close();
    }

    private void HideToTray()
    {
        hiddenToTray = true;
        Hide();

        if (preferences.TrayHintShown)
        {
            return;
        }

        tray.ShowHint();
        preferences = preferences with { TrayHintShown = true };
        SavePreferences();
    }

    private void ShowFromTray()
    {
        hiddenToTray = false;
        Show();

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        ReassertTopmost();
    }

    private void ReassertTopmost()
    {
        // Show() re-enters the Z-order at the bottom of the topmost band, which puts the
        // window under CSP's floating palettes even though Topmost never changed.
        if (!Topmost)
        {
            return;
        }

        Topmost = false;
        Topmost = true;
    }

    private void ApplyHostMode()
    {
        var wantTaskbar = !preferences.RunInTray;
        if (ShowInTaskbar != wantTaskbar)
        {
            ShowInTaskbar = wantTaskbar;

            // Belt and braces. Verified not to be needed on Win11 26200 / .NET 8 — the
            // handle, the HwndSource, its hooks and the DWM corner preference all survive
            // the assignment — but both calls are idempotent and are the only thing
            // standing between a future WPF change and a square window with a dead
            // activation receiver.
            var handle = new WindowInteropHelper(this).Handle;
            if (handle != nint.Zero)
            {
                NativeMethods.ApplyRoundedCorners(handle);
                tray.ReattachActivationHook(this);
            }
        }

        tray.SetIconVisible(preferences.RunInTray);

        var closeAffordance = preferences.RunInTray ? "Hide to tray" : "Close";
        CloseButton.ToolTip = closeAffordance;
        AutomationProperties.SetName(CloseButton, closeAffordance);

        // Turning tray mode off while the window is hidden would leave it unreachable: no
        // taskbar button existed while it was hidden, and the icon is about to go.
        if (!preferences.RunInTray && hiddenToTray)
        {
            ShowFromTray();
        }
    }

    private void OnTraySettingsRequested(object? sender, EventArgs e)
    {
        ShowFromTray();
        SettingsButton_Click(SettingsButton, new RoutedEventArgs());
    }

    private void TrayModeToggle_Click(object sender, RoutedEventArgs e)
    {
        if (loadingSettings)
        {
            return;
        }

        preferences = preferences with { RunInTray = TrayModeToggle.IsChecked == true };
        ApplyHostMode();
        SavePreferences();
    }

    private void ApplyState(ConnectionState next)
    {
        state = next;

        ConnectionDot.Fill = Brush(next switch
        {
            ConnectionState.Idle => "SubtleBrush",
            ConnectionState.Scanning or ConnectionState.Connecting => "WarningBrush",
            ConnectionState.Online or ConnectionState.QrHidden => "AccentBrush",
            _ => "ErrorBrush",
        });

        ConnectionText.Text = next switch
        {
            ConnectionState.Idle => "Offline",
            ConnectionState.Scanning => "Scanning",
            ConnectionState.Connecting => "Connecting",
            ConnectionState.Online or ConnectionState.QrHidden => "Connected",
            _ => "Failed",
        };
        tray.SetConnectionWord(ConnectionText.Text);

        InstructionText.Text = next switch
        {
            ConnectionState.Idle => "Open CSP Companion Mode, then scan its QR.",
            ConnectionState.Scanning or ConnectionState.Connecting => "Leave CSP's QR visible.",
            ConnectionState.Online => "Scan this code from each app you want to connect.",
            ConnectionState.QrHidden => "Show the QR to connect another app.",
            _ => failureInstruction,
        };

        StatusText.Text = next switch
        {
            ConnectionState.Idle => "Not sharing",
            ConnectionState.Scanning => "Scanning displays",
            ConnectionState.Connecting => "Authenticating",
            ConnectionState.Online or ConnectionState.QrHidden => "Sharing",
            _ => FailureStatusText,
        };

        var detail = next switch
        {
            ConnectionState.Online or ConnectionState.QrHidden => readyActivityDetail,
            ConnectionState.Failed => failureDetail,
            _ => string.Empty,
        };
        DetailText.Text = detail;
        DetailText.ToolTip = string.IsNullOrEmpty(detail) ? null : detail;

        ApplyStatusTone(next switch
        {
            ConnectionState.Scanning or ConnectionState.Connecting => StatusTone.Busy,
            ConnectionState.Online or ConnectionState.QrHidden => StatusTone.Good,
            ConnectionState.Failed => StatusTone.Bad,
            _ => StatusTone.Neutral,
        });

        BusyIndicator.Visibility = next is ConnectionState.Scanning or ConnectionState.Connecting
            ? Visibility.Visible
            : Visibility.Collapsed;

        ShowQrCode(
            next is ConnectionState.Online or ConnectionState.QrHidden,
            blurred: next == ConnectionState.QrHidden);

        if (next is not (ConnectionState.Online or ConnectionState.QrHidden))
        {
            ClientCount = 0;
        }

        PrimaryButton.Content = next switch
        {
            ConnectionState.Scanning or ConnectionState.Connecting => "Scanning…",
            ConnectionState.Online => "Hide QR",
            ConnectionState.QrHidden => "Show QR",
            _ => "Scan CSP QR",
        };
        PrimaryButton.IsEnabled = next is not (ConnectionState.Scanning or ConnectionState.Connecting);

        var hasSecondary = next is ConnectionState.Scanning or ConnectionState.Connecting
            or ConnectionState.Online or ConnectionState.QrHidden;
        SecondaryButton.Content = next is ConnectionState.Online or ConnectionState.QrHidden
            ? "Stop"
            : "Cancel";
        SecondaryButton.Visibility = hasSecondary ? Visibility.Visible : Visibility.Collapsed;
        SecondaryActionColumn.Width = hasSecondary
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
        Grid.SetColumn(PrimaryButton, hasSecondary ? 2 : 0);
        Grid.SetColumnSpan(PrimaryButton, hasSecondary ? 1 : 3);

        OnPropertyChanged(nameof(IsStopped));
    }

    private void ApplyStatusTone(StatusTone tone)
    {
        var (dot, border, background) = tone switch
        {
            StatusTone.Busy => ("WarningBrush", "WarningBrush", "WarningStatusBrush"),
            StatusTone.Good => ("AccentBrush", "AccentBrush", "AccentStatusBrush"),
            StatusTone.Bad => ("ErrorBrush", "ErrorBrush", "ErrorStatusBrush"),
            _ => ("SubtleBrush", "BorderBrush", "PanelBrush"),
        };

        StatusDot.Fill = Brush(dot);
        StatusPanel.BorderBrush = Brush(border);
        StatusPanel.Background = Brush(background);
    }

    private void ShowQrCode(bool show, bool blurred)
    {
        if (show && multiplexer?.PairingUrl is { } pairingUrl)
        {
            var source = ProxyQrRenderer.Render(pairingUrl);
            QrImage.Source = source;

            // Frame 300 - Padding 18*2 = 264 available. Snap to a whole number of device
            // pixels per module so no module edge ever lands mid-pixel.
            int modules = source.PixelWidth;                 // includes the 4-module quiet zone
            int scale = Math.Max(3, 264 / modules);
            QrImage.Width = QrImage.Height = modules * scale;

            // Hiding blurs rather than clears: the frame keeps its size and paper, so the
            // layout never shifts and it stays obvious that a code is there to be shown
            // again. The radius is derived from the module pitch, not fixed, so a denser
            // payload cannot end up under-blurred — three modules of Gaussian spread puts
            // the module-level signal well below 8-bit quantisation noise.
            QrImage.Effect = blurred
                ? new BlurEffect { Radius = scale * 3.0, KernelType = KernelType.Gaussian }
                : null;

            QrFrame.Background = Brush("QrPaperBrush");
            QrFrame.BorderThickness = new Thickness(0);
            return;
        }

        QrImage.Source = null;
        QrImage.Effect = null;
        QrFrame.Background = Brushes.Transparent;
        QrFrame.BorderThickness = new Thickness(1);
    }

    private async void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        // IsDefault means Enter anywhere in the window fires this, settings page included.
        if (MainView.Visibility != Visibility.Visible || isBusy)
        {
            return;
        }

        switch (state)
        {
            case ConnectionState.Online:
                ApplyState(ConnectionState.QrHidden);
                return;
            case ConnectionState.QrHidden:
                ApplyState(ConnectionState.Online);
                return;
            default:
                await StartAsync().ConfigureAwait(true);
                return;
        }
    }

    private async void SecondaryButton_Click(object sender, RoutedEventArgs e) =>
        await StopAsync().ConfigureAwait(true);

    private async Task StartAsync()
    {
        await StopAsync().ConfigureAwait(true);
        isBusy = true;
        operationCancellation = new CancellationTokenSource();

        // Captured once: StopAsync disposes and nulls the field, and a scan completing on
        // the same tick Cancel is pressed used to read it back as null.
        var token = operationCancellation.Token;
        ApplyState(ConnectionState.Scanning);

        try
        {
            upstreamDisconnect = null;
            var scanner = new CompanionQrScanner(uri =>
                CompanionPairingCodec.TryDecode(uri.AbsoluteUri, out _));
            // Keep full-display capture bounded. A missing QR should return control to the
            // user instead of consuming a full-resolution screenshot every retry forever.
            var uri = await scanner.ScanAsync(token).ConfigureAwait(true);
            ApplyState(ConnectionState.Connecting);

            var pairing = CompanionPairingCodec.Decode(uri.AbsoluteUri);
            upstream = await UpstreamCompanionClient.ConnectAndAuthenticateAsync(pairing, token)
                .ConfigureAwait(true);
            upstream.Disconnected += UpstreamOnDisconnected;
            if (!upstream.IsAuthenticated)
            {
                throw new IOException("The CSP connection closed immediately after authentication.");
            }

            multiplexer = new CompanionMultiplexer(
                upstream,
                pairing.Generation,
                new CompanionMultiplexerOptions
                {
                    ListenAddress = selectedAddress,
                    AllowLan = !IPAddress.IsLoopback(selectedAddress),
                });
            multiplexer.ClientCountChanged += MultiplexerOnClientCountChanged;
            await multiplexer.StartAsync(token).ConfigureAwait(true);

            // Exactly here: StartAsync opens the listen backlog synchronously before it
            // returns, so a Companion connecting in this instant is queued rather than
            // refused. One statement earlier would publish a URL to a port that does not
            // exist; after ApplyState there is a window where the UI says Sharing and the
            // file is absent. One write per session — neither the invitation password nor
            // the pairing URL rotates mid-session.
            if (IPAddress.IsLoopback(selectedAddress))
            {
                MuxSessionHandoff.TryPublish(multiplexer.PairingUrl!);
            }
            else
            {
                MuxSessionHandoff.TryDeleteStale();
            }

            // Computed once so the Hide/Show toggle can restore it.
            readyActivityDetail = IPAddress.IsLoopback(selectedAddress)
                ? "This computer only."
                : $"{selectedAddress} · same Wi-Fi";
            if (upstreamDisconnect is not null || !upstream.IsAuthenticated)
            {
                throw new IOException("The CSP connection was lost while sharing started.", upstreamDisconnect);
            }

            ApplyState(ConnectionState.Online);
        }
        catch (OperationCanceledException)
        {
            // SendRawAsync links a 15-second source into every request, so an unresponsive
            // CSP arrives here too and must not be mistaken for a user cancel.
            if (token.IsCancellationRequested)
            {
                await StopAsync().ConfigureAwait(true);
            }
            else
            {
                await FailAsync("CSP did not respond.", "Check CSP is still running, then scan again.")
                    .ConfigureAwait(true);
            }
        }
        catch (Exception exception)
        {
            if (TryMapFailure(exception, out var detail, out var instruction))
            {
                await FailAsync(detail, instruction).ConfigureAwait(true);
            }
            else
            {
                await StopAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task StopAsync()
    {
        await stopGate.WaitAsync().ConfigureAwait(true);
        stopping = true;
        try
        {
            // FIRST. Deleting ahead of the teardown makes the file's absence the conservative
            // error: a Companion that misses the window falls back to manual connect, where
            // the reverse order would leave a file naming a dead port for the whole teardown.
            MuxSessionHandoff.TryDeleteOwn();
            operationCancellation?.Cancel();
            if (upstream is not null)
            {
                upstream.Disconnected -= UpstreamOnDisconnected;
            }

            if (multiplexer is not null)
            {
                // Disposing sessions raises ClientCountChanged; unsubscribing first is what
                // prevents a marshal onto a shutting-down dispatcher. Do not reorder.
                multiplexer.ClientCountChanged -= MultiplexerOnClientCountChanged;
                await multiplexer.DisposeAsync().ConfigureAwait(true);
                multiplexer = null;
            }

            if (upstream is not null)
            {
                await upstream.DisposeAsync().ConfigureAwait(true);
                upstream = null;
            }

            operationCancellation?.Dispose();
            operationCancellation = null;
            upstreamDisconnect = null;
            readyActivityDetail = string.Empty;
            ApplyState(ConnectionState.Idle);
        }
        finally
        {
            stopping = false;
            stopGate.Release();
        }
    }

    private async Task FailAsync(string detail, string instruction)
    {
        await StopAsync().ConfigureAwait(true);
        failureDetail = detail;
        failureInstruction = instruction;
        ApplyState(ConnectionState.Failed);
    }

    private bool TryMapFailure(Exception exception, out string detail, out string instruction)
    {
        switch (exception)
        {
            case IOException:
                detail = "Could not reach CSP.";
                instruction = "Check CSP's QR is visible, then scan again.";
                return true;

            case UnauthorizedAccessException:
                // The reason is the only actionable payload the upstream carries.
                detail = $"CSP refused authentication. {AuthenticationReason(exception.Message)}";
                instruction = "Reopen Connect to smartphone in CSP, then scan again.";
                return true;

            case SocketException:
                detail = $"Could not open a port on {selectedAddress}.";
                instruction = "Choose a different network in Settings.";
                return true;

            case TimeoutException:
                detail = "No CSP QR was found within 12 seconds.";
                instruction = "Make CSP's QR fully visible, then scan again.";
                return true;

            case InvalidOperationException when exception.Message.StartsWith(
                "LAN listening requires",
                StringComparison.Ordinal):
                detail = "That network is not usable for sharing.";
                instruction = "Choose a different network in Settings.";
                return true;

            case InvalidOperationException when exception.Message.StartsWith(
                "Windows did not report",
                StringComparison.Ordinal):
                detail = "No display was available to scan.";
                instruction = "Reconnect a monitor, then scan again.";
                return true;

            // MaximumClients is never overridden, so this one really is unreachable and is
            // given no sentence. Every OTHER InvalidOperationException now falls through to
            // default: there is no logging anywhere in this repo, so returning false here
            // put the user back on Idle with the failure reported nowhere at all.
            case ArgumentOutOfRangeException:
                detail = string.Empty;
                instruction = string.Empty;
                return false;

            default:
                detail = "Sharing could not start.";
                instruction = "Check CSP's QR and the selected network.";
                return true;
        }
    }

    private void MultiplexerOnClientCountChanged(
        object? sender,
        CompanionClientCountChangedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            // Posting to a dispatcher that has begun shutdown faults the returned
            // operation, and a late callback during window teardown is entirely possible.
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            {
                return;
            }

            Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                () => MultiplexerOnClientCountChanged(sender, e));
            return;
        }

        ClientCount = e.AuthenticatedClientCount;
        if (e.AuthenticatedClientCount > 0 &&
            hideQrAfterFirstConnection &&
            state == ConnectionState.Online)
        {
            ApplyState(ConnectionState.QrHidden);
        }
    }

    private async void UpstreamOnDisconnected(
        object? sender,
        CompanionDisconnectedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
            {
                _ = Dispatcher.BeginInvoke(
                    DispatcherPriority.Send,
                    () => UpstreamOnDisconnected(sender, e));
            }

            return;
        }

        if (!ReferenceEquals(sender, upstream))
        {
            return;
        }

        upstreamDisconnect ??= e.Exception;
        if (state is not (ConnectionState.Online or ConnectionState.QrHidden) ||
            closeInProgress ||
            stopping)
        {
            return;
        }

        await FailAsync(
                "The connection to CSP was lost.",
                "Reopen Connect to smartphone in CSP, then scan again.")
            .ConfigureAwait(true);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Before the await: the icon has to exist before ApplyHostMode can show it, and a
        // second launch arriving during the adapter walk must find a window to adopt.
        tray.Attach(this);
        ApplyHostMode();

        // GetChoices walks every adapter, and a flaky VPN stalls it; it used to run
        // synchronously on the UI thread from two call sites.
        var choices = await Task.Run(NetworkDiscovery.GetChoices).ConfigureAwait(true);
        PopulateNetworkChoices(choices);
    }

    private void PopulateNetworkChoices(IReadOnlyList<NetworkChoice> choices)
    {
        loadingSettings = true;
        try
        {
            NetworkScopePicker.Items.Clear();
            ComboBoxItem? selected = null;
            foreach (var choice in choices)
            {
                var item = new ComboBoxItem { Content = choice.Label, Tag = choice.Address };
                NetworkScopePicker.Items.Add(item);
                if (choice.Address.Equals(selectedAddress))
                {
                    selected = item;
                }
            }

            if (selected is null &&
                IPAddress.TryParse(preferences.ListenAddress, out var saved) &&
                !IPAddress.IsLoopback(saved))
            {
                // A saved adapter that is down, renamed, or on another subnet used to
                // vanish and revert to loopback with no feedback anywhere.
                selected = new ComboBoxItem
                {
                    Content = $"{saved} · unavailable",
                    Tag = saved,
                    IsEnabled = false,
                };
                NetworkScopePicker.Items.Add(selected);
            }

            NetworkScopePicker.SelectedItem = selected ?? NetworkScopePicker.Items[0];
        }
        finally
        {
            loadingSettings = false;
        }
    }

    private void NetworkScopePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loadingSettings ||
            NetworkScopePicker.SelectedItem is not ComboBoxItem { Tag: IPAddress address })
        {
            return;
        }

        selectedAddress = address;
        preferences = preferences with { ListenAddress = address.ToString() };
        SavePreferences();
    }

    private void AutoHideQrToggle_Click(object sender, RoutedEventArgs e)
    {
        hideQrAfterFirstConnection = AutoHideQrToggle.IsChecked == true;
        preferences = preferences with { HideQrAfterFirstConnection = hideQrAfterFirstConnection };
        SavePreferences();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        MainView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Visible;

        // Hidden rather than Collapsed: a ghosted icon beside a Back button reads as a
        // broken control, and the 28px column has to stay so the caption buttons hold.
        SettingsButton.Visibility = Visibility.Hidden;
        BackButton.Focus();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsView.Visibility = Visibility.Collapsed;
        MainView.Visibility = Visibility.Visible;
        SettingsButton.Visibility = Visibility.Visible;
        SettingsButton.Focus();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && SettingsView.Visibility == Visibility.Visible)
        {
            BackButton_Click(BackButton, e);
            e.Handled = true;
        }
    }

    private void PinButton_Toggled(object sender, RoutedEventArgs e)
    {
        var pinned = PinButton.IsChecked == true;
        Topmost = pinned;
        PinButton.ToolTip = pinned ? "Always on top" : "Always on top (off)";
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ShowSettingsFileButton_Click(object sender, RoutedEventArgs e)
    {
        var path = AppPreferences.SettingsPath;
        if (File.Exists(path))
        {
            var select = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
            select.ArgumentList.Add($"/select,{path}");
            Process.Start(select)?.Dispose();
            return;
        }

        // Nothing has been saved yet: show the folder the file will land in.
        var directory = Path.GetDirectoryName(path);
        if (directory is null)
        {
            return;
        }

        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true })?.Dispose();
    }

    private void ShowAboutText()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        var shortVersion = version is null
            ? string.Empty
            : $"Version {version.Major}.{version.Minor}.{version.Build} · ";
        AboutText.Text = $"{shortVersion}GPL-3.0";
    }

    private void ShowSettingsPath()
    {
        // The path is trimmed to one line in the card, so the full value has to stay
        // reachable somewhere.
        SettingsPathText.Text = AppPreferences.SettingsPath;
        SettingsPathText.ToolTip = AppPreferences.SettingsPath;
    }

    private void RestoreWindowPosition()
    {
        // The probe point is inside the title bar, so a window whose saved monitor has
        // been unplugged, or which was left mostly off-screen, falls back to centre.
        if (preferences.WindowLeft is { } left &&
            preferences.WindowTop is { } top &&
            System.Windows.Forms.Screen.AllScreens.Any(screen =>
                screen.WorkingArea.Contains((int)left + 40, (int)top + 20)))
        {
            Left = left;
            Top = top;
            return;
        }

        // CenterScreen cannot be requested from here, so the centre is computed.
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + ((workArea.Width - Width) / 2);
        Top = workArea.Top + ((workArea.Height - Height) / 2);
    }

    private void SaveWindowPosition()
    {
        preferences = preferences with { WindowLeft = Left, WindowTop = Top };
        SavePreferences();
    }

    private void SavePreferences() => ShowSettingsNotice(AppPreferences.Save(preferences));

    private void ShowSettingsNotice(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            SettingsNotice.Visibility = Visibility.Collapsed;
            SettingsNoticeText.Text = string.Empty;
            return;
        }

        SettingsNoticeText.Text = text;
        SettingsNotice.Visibility = Visibility.Visible;
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
