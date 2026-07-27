using System.Windows;
using System.Windows.Threading;

namespace CspMultiplexer.App;

public partial class App : Application
{
    private TrayHost? tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        tray = new TrayHost();
        if (!tray.IsFirstInstance)
        {
            tray.ActivateExistingInstance();
            tray.Dispose();
            tray = null;

            // The suite's only sanctioned Application.Shutdown(). It raises Closing and
            // ignores e.Cancel, so the tray branch is disarmed first — here that is a
            // no-op because no window exists yet, and it is written anyway so the
            // invariant does not depend on the order this method happens to have.
            if (MainWindow is MainWindow existing)
            {
                existing.MarkExitRequested();
            }

            Shutdown();
            return;
        }

        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        var window = new MainWindow(tray);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        tray?.Dispose();
        base.OnExit(e);
    }

    private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        // The window's OnClosing cannot cancel a session-ending shutdown, so the tray
        // branch must be disarmed here or teardown is skipped entirely — on the one exit
        // path where the Mux is most likely to be sharing.
        if (MainWindow is MainWindow window)
        {
            window.MarkExitRequested();
        }

        MuxSessionHandoff.TryDeleteOwn();
    }

    private void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        // The icon must not outlive the process. e.Handled stays false: the crash still
        // surfaces and the process still faults.
        tray?.Dispose();
    }

    private void OnProcessExit(object? sender, EventArgs e) => MuxSessionHandoff.TryDeleteOwn();
}
