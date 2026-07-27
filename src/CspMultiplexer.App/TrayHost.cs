// ═══ CSP SUITE SHARED FILE ══════════════════════════════════════════════════
// Reconcile with tools/suite-sync.ps1 (spec §0.1). Tier 2.
//   Companion : src/CspPaletteCompanion.App/TrayHost.cs
//   Mux       : src/CspMultiplexer.App/TrayHost.cs
// ════════════════════════════════════════════════════════════════════════════
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;

// ── SYNC-LOCAL BEGIN ──
namespace CspMultiplexer.App;

/// <summary>The only values that differ between the two apps' copies of this file.</summary>
file static class TrayIdentity
{
    internal const string Wordmark = "CSP Mux";
    internal const string MutexName = @"Local\CspSuite.CspMux.SingleInstance";
    internal const string ActivationMessageName = "CspSuite.CspMux.Activate";
    internal const string IconResourceUri = "pack://application:,,,/Assets/csp-mux.ico";
    internal const string HintText = "Still sharing. Exit from the tray icon.";
}
// ── SYNC-LOCAL END ──

/// <summary>
/// Everything that outlives the window: the single-instance mutex, the notification-area
/// icon and its menu, and the receiver a second launch posts to. The window subscribes to
/// the four events and owns every decision; this type owns no state the user can name.
/// </summary>
internal sealed class TrayHost : IDisposable
{
    private const int BalloonTimeoutMilliseconds = 10000;

    private readonly System.Windows.Forms.NotifyIcon notifyIcon = new();
    private readonly ContextMenu menu = new();
    private readonly MenuItem visibilityItem = new();
    private readonly Mutex instanceMutex;
    private readonly bool isFirstInstance;
    private readonly uint activationMessage;

    private Window? window;
    private HwndSource? activationSource;
    private System.Drawing.Icon? currentIcon;
    private int currentFrame;
    private bool pendingShow;
    private bool disposed;

    internal TrayHost()
    {
        // Ownership is deliberately never taken. The handle's existence is the whole
        // signal, and ReleaseMutex is thread-affine while Dispose can run from
        // ProcessExit on a pool thread. A killed process closes its handle, so the next
        // launch is a normal first instance with no self-heal code.
        instanceMutex = new Mutex(initiallyOwned: false, TrayIdentity.MutexName, out isFirstInstance);
        activationMessage = NativeMethods.RegisterWindowMessage(TrayIdentity.ActivationMessageName);

        // Here and not in Attach: the window writes the connection word from its own
        // constructor, which runs first, and Attach would overwrite it with the bare
        // wordmark for the rest of the session.
        notifyIcon.Text = TrayIdentity.Wordmark;
    }

    internal event EventHandler? ShowRequested;

    internal event EventHandler? HideRequested;

    internal event EventHandler? SettingsRequested;

    internal event EventHandler? ExitRequested;

    internal bool IsFirstInstance => isFirstInstance;

    /// <summary>Hands the activation to the instance that already holds the mutex.</summary>
    internal void ActivateExistingInstance()
    {
        if (activationMessage == 0)
        {
            return;
        }

        NativeMethods.EnumWindows(PostActivationToMarkedWindow, nint.Zero);
    }

    /// <summary>
    /// Installs the activation receiver. Idempotent by <see cref="HwndSource"/> identity,
    /// so <c>ApplyHostMode</c> can call it after every <c>ShowInTaskbar</c> assignment
    /// without unhooking a source that never changed.
    /// </summary>
    internal void ReattachActivationHook(Window target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (PresentationSource.FromVisual(target) is not HwndSource source ||
            ReferenceEquals(source, activationSource))
        {
            return;
        }

        activationSource?.RemoveHook(OnWindowMessage);
        activationSource = source;
        activationSource.AddHook(OnWindowMessage);

        // How the next launch finds this window. The property outlives Hide() and survives
        // the owner change ShowInTaskbar makes, neither of which is true of anything the
        // window itself advertises.
        NativeMethods.SetProp(source.Handle, TrayIdentity.ActivationMessageName, 1);
    }

    /// <summary>Creates the icon and its menu, and flushes an activation that arrived first.</summary>
    internal void Attach(Window target)
    {
        ArgumentNullException.ThrowIfNull(target);

        window = target;
        ReattachActivationHook(target);
        BuildMenu();

        notifyIcon.MouseUp += OnIconMouseUp;
        notifyIcon.MouseDoubleClick += OnIconDoubleClick;
        target.DpiChanged += OnDpiChanged;
        UpdateIconFrame();

        if (!pendingShow)
        {
            return;
        }

        pendingShow = false;
        ShowRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Tracks the tray-mode setting only — never window visibility, or the icon flickers
    /// every time the window is shown and hidden.
    /// </summary>
    internal void SetIconVisible(bool visible) => notifyIcon.Visible = visible && !disposed;

    internal void SetConnectionWord(string connectionWord) =>
        notifyIcon.Text = $"{TrayIdentity.Wordmark} · {connectionWord}";

    /// <summary>
    /// The suite's one OS-drawn surface, and it is earned: the window is hidden at the
    /// moment this has to speak, so no in-app surface exists to say it on.
    /// </summary>
    internal void ShowHint()
    {
        if (!notifyIcon.Visible)
        {
            return;
        }

        notifyIcon.ShowBalloonTip(
            BalloonTimeoutMilliseconds,
            TrayIdentity.Wordmark,
            TrayIdentity.HintText,
            System.Windows.Forms.ToolTipIcon.None);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (activationSource is not null)
        {
            activationSource.RemoveHook(OnWindowMessage);
            NativeMethods.RemoveProp(activationSource.Handle, TrayIdentity.ActivationMessageName);
            activationSource = null;
        }

        if (window is not null)
        {
            window.DpiChanged -= OnDpiChanged;
            window = null;
        }

        menu.IsOpen = false;

        // Visible = false issues NIM_DELETE once; Dispose on an already-hidden icon does
        // not issue a second one. This is the ghost-icon guarantee.
        notifyIcon.Visible = false;
        notifyIcon.Dispose();

        currentIcon?.Dispose();
        currentIcon = null;

        instanceMutex.Dispose();
    }

    private bool PostActivationToMarkedWindow(nint windowHandle, nint parameter)
    {
        if (NativeMethods.GetProp(windowHandle, TrayIdentity.ActivationMessageName) == nint.Zero)
        {
            return true;
        }

        NativeMethods.PostMessage(windowHandle, activationMessage, nuint.Zero, nint.Zero);
        return false;
    }

    private void BuildMenu()
    {
        visibilityItem.Click += OnVisibilityItemClick;

        var settingsItem = new MenuItem { Header = "Settings" };
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        menu.Items.Add(visibilityItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(exitItem);

        // Recomputed at every opening, never cached: the window can be minimised, hidden
        // or shown between two openings by routes this type never sees.
        menu.Opened += (_, _) =>
            visibilityItem.Header = IsWindowShowing() ? "Hide window" : "Show window";
    }

    private void OnVisibilityItemClick(object sender, RoutedEventArgs e) =>
        (IsWindowShowing() ? HideRequested : ShowRequested)?.Invoke(this, EventArgs.Empty);

    private bool IsWindowShowing() =>
        window is { IsVisible: true, WindowState: not WindowState.Minimized };

    private nint OnWindowMessage(nint handle, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (activationMessage == 0 || (uint)message != activationMessage)
        {
            return nint.Zero;
        }

        handled = true;

        // The hook is installed at OnSourceInitialized but the window is not adopted
        // until Loaded. A second launch inside that gap must not be dropped.
        if (window is null)
        {
            pendingShow = true;
            return nint.Zero;
        }

        ShowRequested?.Invoke(this, EventArgs.Empty);
        return nint.Zero;
    }

    private void OnIconMouseUp(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        if (e.Button != System.Windows.Forms.MouseButtons.Right)
        {
            return;
        }

        // Without foreground ownership the first click outside the popup only activates
        // the app instead of dismissing the menu.
        if (window is not null)
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle != nint.Zero)
            {
                NativeMethods.SetForegroundWindow(handle);
            }
        }

        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        menu.Focus();
    }

    private void OnIconDoubleClick(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        // Always show, never toggle: a double-click that hid the window would make the
        // icon the one way back to a window the user has just asked to see.
        if (e.Button == System.Windows.Forms.MouseButtons.Left)
        {
            ShowRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnDpiChanged(object sender, DpiChangedEventArgs e) => UpdateIconFrame();

    private void UpdateIconFrame()
    {
        var scale = window is null ? 1.0 : VisualTreeHelper.GetDpi(window).DpiScaleX;
        var frame = scale switch
        {
            >= 2.0 => 32,
            >= 1.5 => 24,
            >= 1.25 => 20,
            _ => 16,
        };

        if (frame == currentFrame)
        {
            return;
        }

        if (Application.GetResourceStream(new Uri(TrayIdentity.IconResourceUri)) is not { } resource)
        {
            return;
        }

        System.Drawing.Icon next;
        using (resource.Stream)
        {
            // The size argument selects a stored frame; it never resamples a larger one,
            // which is why the .ico carries hand-drawn 16 and 20 constructions.
            next = new System.Drawing.Icon(resource.Stream, new System.Drawing.Size(frame, frame));
        }

        // Assign before disposing: Shell_NotifyIcon copies the handle during NIM_MODIFY,
        // so the outgoing icon has to outlive the assignment.
        var previous = currentIcon;
        notifyIcon.Icon = next;
        currentIcon = next;
        currentFrame = frame;
        previous?.Dispose();
    }
}
