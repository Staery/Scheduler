using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Scheduler.Controls;
using Scheduler.Core.Models;
using Scheduler.Core.ViewModels;

namespace Scheduler;

/// <summary>Main window: forwards timeline feedback to the view model and drives the playhead with a frame timer.</summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _frameTimer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Stopwatch _frameClock = new();

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        Timeline.Rendered += OnTimelineRendered;
        Timeline.HoveredEventChanged += OnHoveredEventChanged;
        _frameTimer.Tick += OnFrame;
        Loaded += OnLoaded;
        Closed += (_, _) => _frameTimer.Stop();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.GenerateCommand.ExecuteAsync(null);
        Timeline.CenterOn(_viewModel.Now);
        _frameClock.Start();
        _frameTimer.Start();
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var elapsed = _frameClock.Elapsed;
        _frameClock.Restart();
        _viewModel.Advance(elapsed);
    }

    private void OnTimelineRendered(object? sender, TimelineRenderedEventArgs e) => _viewModel.ReportRender(e.DrawnEvents, e.Elapsed);

    private void OnHoveredEventChanged(object? sender, ScheduleEvent? e) => _viewModel.ReportHover(e);

    private void OnJumpToNow(object sender, RoutedEventArgs e) => Timeline.CenterOn(_viewModel.Now);
}
