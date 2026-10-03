using System.Windows;
using System.Windows.Threading;
using Scheduler.Core.Services;
using Scheduler.Core.ViewModels;

namespace Scheduler;

/// <summary>Composition root: wires services and view models together and shows the main window.</summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        MainWindow = new MainWindow(new MainViewModel(new ScheduleGenerator(), TimeProvider.System));
        MainWindow.Show();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"Something went wrong:\n\n{e.Exception.Message}", "Scheduler", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
