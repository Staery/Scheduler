using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scheduler.Core.Models;
using Scheduler.Core.Services;

namespace Scheduler.Core.ViewModels;

/// <summary>Generation parameters, view settings, the playhead and statistics of the timeline window.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    public const double MinZoom = 0.05;
    public const double MaxZoom = 8;

    /// <summary>Days of history before today, so the timeline shows finished as well as planned work.</summary>
    public const int HistoryDays = 2;

    /// <summary>The playhead starts today at 08:00.</summary>
    public const int InitialNow = HistoryDays * 1440 + 8 * 60;

    private readonly ScheduleGenerator _generator;
    private readonly TimeProvider _time;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InputError))]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    private string _layersInput = "20";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InputError))]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    private string _eventsInput = "3000";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalText), nameof(NowText))]
    private Schedule _schedule = Schedule.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    private bool _isGenerating;

    [ObservableProperty]
    private double _zoom = 1.5;

    [ObservableProperty]
    private bool _showPending = true;

    [ObservableProperty]
    private bool _showJeopardy = true;

    [ObservableProperty]
    private bool _showCompleted = true;

    [ObservableProperty]
    private double _now = InitialNow;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayLabel))]
    private bool _isPlaying;

    /// <summary>Playback speed in timeline minutes per real second.</summary>
    [ObservableProperty]
    private double _speed = 10;

    [ObservableProperty]
    private string _generationText = string.Empty;

    [ObservableProperty]
    private string _renderText = string.Empty;

    [ObservableProperty]
    private string _hoverText = "Hover an event to see its details · Ctrl+wheel to zoom · Shift+wheel to scroll in time";

    public MainViewModel(ScheduleGenerator generator, TimeProvider time)
    {
        _generator = generator;
        _time = time;
    }

    public string DateText => OrdinalDate.Format(Today);

    public string? InputError => TryReadInputs(out _, out _, out var error) ? null : error;

    public string TotalText => $"{Schedule.EventCount:N0} events in {Schedule.LayerCount} layers";

    public string NowText => Schedule.Origin.AddMinutes(Now).ToString("ddd HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    public string PlayLabel => IsPlaying ? "Pause" : "Play";

    private DateTime Today => _time.GetLocalNow().Date;

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        if (!TryReadInputs(out var layers, out var events, out _))
        {
            return;
        }

        IsGenerating = true;
        GenerationText = $"Generating {events:N0} events…";

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var origin = Today.AddDays(-HistoryDays);

            // Generation is pure CPU work on immutable data, so it runs off the UI thread and the result is swapped in.
            var schedule = await Task.Run(() => _generator.Generate(layers, events, origin, InitialNow));

            Schedule = schedule;
            Now = InitialNow;
            GenerationText = $"Generated {schedule.EventCount:N0} events in {stopwatch.ElapsedMilliseconds:N0} ms";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private bool CanGenerate() => !IsGenerating && InputError is null;

    [RelayCommand]
    private void TogglePlay() => IsPlaying = !IsPlaying;

    [RelayCommand]
    private void ResetNow() => Now = InitialNow;

    /// <summary>Moves the playhead; called by the view's frame timer while playing.</summary>
    public void Advance(TimeSpan elapsed)
    {
        if (!IsPlaying)
        {
            return;
        }

        var next = Now + elapsed.TotalSeconds * Speed;
        Now = Schedule.Length > 0 && next > Schedule.Length ? 0 : next;
    }

    /// <summary>Called by the timeline after every frame.</summary>
    public void ReportRender(int drawnEvents, TimeSpan elapsed) =>
        RenderText = $"Drew {drawnEvents:N0} of {Schedule.EventCount:N0} events in {elapsed.TotalMilliseconds:0.0} ms";

    public void ReportHover(ScheduleEvent? scheduleEvent)
    {
        if (scheduleEvent is null)
        {
            return;
        }

        var start = Schedule.Origin.AddMinutes(scheduleEvent.Start);
        var end = Schedule.Origin.AddMinutes(scheduleEvent.End);
        HoverText = $"{scheduleEvent.Title} · Layer {scheduleEvent.Layer + 1} · {start:ddd HH:mm}–{end:HH:mm} · {scheduleEvent.Status}";
    }

    partial void OnZoomChanged(double value)
    {
        var clamped = Math.Clamp(value, MinZoom, MaxZoom);
        if (clamped != value)
        {
            Zoom = clamped;
        }
    }

    partial void OnNowChanged(double value) => OnPropertyChanged(nameof(NowText));

    private bool TryReadInputs(out int layers, out int events, out string? error)
    {
        events = 0;
        if (!int.TryParse(LayersInput?.Trim(), out layers) || !int.TryParse(EventsInput?.Trim().Replace(" ", string.Empty).Replace(",", string.Empty), out events))
        {
            error = "Layers and events must be whole numbers.";
            return false;
        }

        error = ScheduleGenerator.Validate(layers, events);
        return error is null;
    }
}
