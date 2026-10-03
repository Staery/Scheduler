using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Scheduler.Core.Models;
using Scheduler.Core.Services;

namespace Scheduler.Controls;

/// <summary>Raised after every frame with the number of events drawn and the time it took.</summary>
public sealed class TimelineRenderedEventArgs(int drawnEvents, TimeSpan elapsed) : EventArgs
{
    public int DrawnEvents { get; } = drawnEvents;

    public TimeSpan Elapsed { get; } = elapsed;
}

/// <summary>
/// A virtualized timeline. Instead of creating a UI element per event, it draws only the events inside the viewport
/// straight into a <see cref="DrawingContext"/>, so a million events scroll as smoothly as a hundred.
/// Mouse wheel scrolls rows, Shift+wheel scrolls time, Ctrl+wheel zooms around the cursor.
/// </summary>
public sealed class TimelineView : FrameworkElement
{
    public const double RulerHeight = 34;
    public const double LabelWidth = 92;
    public const double RowHeight = 36;

    public static readonly DependencyProperty ScheduleProperty = Register(nameof(Schedule), Schedule.Empty, OnLayoutChanged);
    public static readonly DependencyProperty PixelsPerMinuteProperty = Register(nameof(PixelsPerMinute), 1.5, OnZoomChanged);
    public static readonly DependencyProperty HorizontalOffsetProperty = Register(nameof(HorizontalOffset), 0.0, OnOffsetChanged);
    public static readonly DependencyProperty VerticalOffsetProperty = Register(nameof(VerticalOffset), 0.0, OnOffsetChanged);
    public static readonly DependencyProperty NowProperty = Register(nameof(Now), 0.0);
    public static readonly DependencyProperty ShowPendingProperty = Register(nameof(ShowPending), true);
    public static readonly DependencyProperty ShowJeopardyProperty = Register(nameof(ShowJeopardy), true);
    public static readonly DependencyProperty ShowCompletedProperty = Register(nameof(ShowCompleted), true);
    public static readonly DependencyProperty ScrollableWidthProperty = Register(nameof(ScrollableWidth), 0.0);
    public static readonly DependencyProperty ScrollableHeightProperty = Register(nameof(ScrollableHeight), 0.0);
    public static readonly DependencyProperty ViewportWidthProperty = Register(nameof(ViewportWidth), 0.0);
    public static readonly DependencyProperty ViewportHeightProperty = Register(nameof(ViewportHeight), 0.0);

    private readonly Stopwatch _stopwatch = new();
    private ScheduleEvent? _hovered;
    private FontFamily? _fontFamily;
    private Typeface _typeface = new("Segoe UI");
    private Typeface _boldTypeface = new("Segoe UI");

    public TimelineView()
    {
        ClipToBounds = true;
        Focusable = true;
        ToolTipService.SetInitialShowDelay(this, 250);
        ToolTipService.SetShowDuration(this, 20000);
    }

    public event EventHandler<TimelineRenderedEventArgs>? Rendered;

    public event EventHandler<ScheduleEvent?>? HoveredEventChanged;

    public Schedule Schedule { get => (Schedule)GetValue(ScheduleProperty); set => SetValue(ScheduleProperty, value); }

    public double PixelsPerMinute { get => (double)GetValue(PixelsPerMinuteProperty); set => SetValue(PixelsPerMinuteProperty, value); }

    public double HorizontalOffset { get => (double)GetValue(HorizontalOffsetProperty); set => SetValue(HorizontalOffsetProperty, value); }

    public double VerticalOffset { get => (double)GetValue(VerticalOffsetProperty); set => SetValue(VerticalOffsetProperty, value); }

    /// <summary>Position of the playhead, in minutes.</summary>
    public double Now { get => (double)GetValue(NowProperty); set => SetValue(NowProperty, value); }

    public bool ShowPending { get => (bool)GetValue(ShowPendingProperty); set => SetValue(ShowPendingProperty, value); }

    public bool ShowJeopardy { get => (bool)GetValue(ShowJeopardyProperty); set => SetValue(ShowJeopardyProperty, value); }

    public bool ShowCompleted { get => (bool)GetValue(ShowCompletedProperty); set => SetValue(ShowCompletedProperty, value); }

    public double ScrollableWidth { get => (double)GetValue(ScrollableWidthProperty); private set => SetValue(ScrollableWidthProperty, value); }

    public double ScrollableHeight { get => (double)GetValue(ScrollableHeightProperty); private set => SetValue(ScrollableHeightProperty, value); }

    public double ViewportWidth { get => (double)GetValue(ViewportWidthProperty); private set => SetValue(ViewportWidthProperty, value); }

    public double ViewportHeight { get => (double)GetValue(ViewportHeightProperty); private set => SetValue(ViewportHeightProperty, value); }

    /// <summary>Scrolls so that <paramref name="minute"/> is in the middle of the viewport.</summary>
    public void CenterOn(double minute) => HorizontalOffset = minute * PixelsPerMinute - ViewportWidth / 2;

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        UpdateScrollInfo();
    }

    protected override void OnRender(DrawingContext dc)
    {
        _stopwatch.Restart();

        var width = ActualWidth;
        var height = ActualHeight;
        var schedule = Schedule;
        var ppm = PixelsPerMinute;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        UpdateTypefaces();

        dc.DrawRectangle(Palette.Surface, null, new Rect(0, 0, width, height));

        var from = HorizontalOffset / ppm;
        var to = (HorizontalOffset + Math.Max(0, width - LabelWidth)) / ppm;
        var firstLayer = Math.Max(0, (int)(VerticalOffset / RowHeight));
        var lastLayer = Math.Min(schedule.LayerCount - 1, (int)((VerticalOffset + height - RulerHeight) / RowHeight));
        var step = TimeAxis.StepFor(ppm);

        // Rows and grid lines.
        dc.PushClip(new RectangleGeometry(new Rect(LabelWidth, RulerHeight, Math.Max(0, width - LabelWidth), Math.Max(0, height - RulerHeight))));

        for (var layer = firstLayer; layer <= lastLayer; layer++)
        {
            dc.DrawRectangle(layer % 2 == 0 ? Palette.RowEven : Palette.RowOdd, null, new Rect(LabelWidth, RowTop(layer), width, RowHeight));
        }

        foreach (var tick in TimeAxis.Ticks(from, to, step))
        {
            var x = MinuteToX(tick);
            dc.DrawLine(tick % 1440 == 0 ? Palette.DayLine : Palette.GridLine, new Point(x, RulerHeight), new Point(x, height));
        }

        // Events: only the ones overlapping the viewport, found by binary search in each visible row.
        var drawn = 0;
        for (var layer = firstLayer; layer <= lastLayer; layer++)
        {
            foreach (var scheduleEvent in schedule.Query(layer, from, to))
            {
                if (!IsVisible(scheduleEvent.Status))
                {
                    continue;
                }

                DrawEvent(dc, scheduleEvent, ppm, dpi);
                drawn++;
            }
        }

        // Playhead.
        var nowX = MinuteToX(Now);
        if (nowX >= LabelWidth && nowX <= width)
        {
            dc.DrawLine(Palette.NowPen, new Point(nowX, RulerHeight), new Point(nowX, height));
        }

        dc.Pop();

        DrawRuler(dc, schedule, from, to, step, width, nowX, dpi);
        DrawLabels(dc, firstLayer, lastLayer, height, dpi);

        _stopwatch.Stop();
        Rendered?.Invoke(this, new TimelineRenderedEventArgs(drawn, _stopwatch.Elapsed));
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            // Zoom around the cursor so the minute under it stays in place.
            var x = Math.Max(0, e.GetPosition(this).X - LabelWidth);
            var minute = (HorizontalOffset + x) / PixelsPerMinute;
            PixelsPerMinute = Math.Clamp(PixelsPerMinute * (e.Delta > 0 ? 1.25 : 0.8), 0.05, 8);
            HorizontalOffset = minute * PixelsPerMinute - x;
        }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            HorizontalOffset -= e.Delta;
        }
        else
        {
            VerticalOffset -= e.Delta / 120.0 * RowHeight * 2;
        }

        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var position = e.GetPosition(this);
        ScheduleEvent? hit = null;

        if (position.X > LabelWidth && position.Y > RulerHeight)
        {
            var layer = (int)((position.Y - RulerHeight + VerticalOffset) / RowHeight);
            var minute = (position.X - LabelWidth + HorizontalOffset) / PixelsPerMinute;
            hit = Schedule.FindAt(layer, minute);
            if (hit is not null && !IsVisible(hit.Status))
            {
                hit = null;
            }
        }

        SetHovered(hit);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        SetHovered(null);
    }

    private static DependencyProperty Register<T>(string name, T defaultValue, PropertyChangedCallback? changed = null) =>
        DependencyProperty.Register(
            name,
            typeof(T),
            typeof(TimelineView),
            new FrameworkPropertyMetadata(defaultValue, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, changed));

    private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (TimelineView)d;
        view._hovered = null;
        view.HorizontalOffset = 0;
        view.VerticalOffset = 0;
        view.UpdateScrollInfo();
    }

    private static void OnZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((TimelineView)d).UpdateScrollInfo();

    private static void OnOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((TimelineView)d).ClampOffsets();

    private void UpdateScrollInfo()
    {
        ViewportWidth = Math.Max(0, ActualWidth - LabelWidth);
        ViewportHeight = Math.Max(0, ActualHeight - RulerHeight);
        ScrollableWidth = Math.Max(0, (Schedule.Length + 60) * PixelsPerMinute - ViewportWidth);
        ScrollableHeight = Math.Max(0, Schedule.LayerCount * RowHeight - ViewportHeight);
        ClampOffsets();
    }

    private void ClampOffsets()
    {
        var horizontal = Math.Clamp(HorizontalOffset, 0, ScrollableWidth);
        if (horizontal != HorizontalOffset)
        {
            HorizontalOffset = horizontal;
        }

        var vertical = Math.Clamp(VerticalOffset, 0, ScrollableHeight);
        if (vertical != VerticalOffset)
        {
            VerticalOffset = vertical;
        }
    }

    private void SetHovered(ScheduleEvent? scheduleEvent)
    {
        if (ReferenceEquals(scheduleEvent, _hovered))
        {
            return;
        }

        _hovered = scheduleEvent;
        ToolTip = scheduleEvent is null ? null : Describe(scheduleEvent);
        HoveredEventChanged?.Invoke(this, scheduleEvent);
        InvalidateVisual();
    }

    private string Describe(ScheduleEvent scheduleEvent)
    {
        var start = Schedule.Origin.AddMinutes(scheduleEvent.Start);
        var end = Schedule.Origin.AddMinutes(scheduleEvent.End);
        return $"{scheduleEvent.Title}\n{start:ddd d MMM, HH:mm} – {end:HH:mm} ({scheduleEvent.Duration} min)\nLayer {scheduleEvent.Layer + 1} · {scheduleEvent.Status}";
    }

    /// <summary>Text uses the font inherited from the window, like the rest of the UI.</summary>
    private void UpdateTypefaces()
    {
        var family = TextElement.GetFontFamily(this);
        if (Equals(family, _fontFamily))
        {
            return;
        }

        _fontFamily = family;
        _typeface = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        _boldTypeface = new Typeface(family, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    }

    private bool IsVisible(EventStatus status) => status switch
    {
        EventStatus.Pending => ShowPending,
        EventStatus.Jeopardy => ShowJeopardy,
        _ => ShowCompleted,
    };

    private double MinuteToX(double minute) => LabelWidth + minute * PixelsPerMinute - HorizontalOffset;

    private double RowTop(int layer) => RulerHeight + layer * RowHeight - VerticalOffset;

    private void DrawEvent(DrawingContext dc, ScheduleEvent scheduleEvent, double ppm, double dpi)
    {
        var (fill, accent) = Palette.ForStatus(scheduleEvent.Status);
        var rect = new Rect(
            MinuteToX(scheduleEvent.Start) + 1,
            RowTop(scheduleEvent.Layer) + 5,
            Math.Max(2, scheduleEvent.Duration * ppm - 2),
            RowHeight - 10);

        var hovered = ReferenceEquals(scheduleEvent, _hovered);
        dc.DrawRoundedRectangle(fill, hovered ? Palette.HoverPen : null, rect, 4, 4);

        if (rect.Width < 6)
        {
            return;
        }

        dc.DrawRoundedRectangle(accent, null, new Rect(rect.X, rect.Y, 3, rect.Height), 1.5, 1.5);

        if (rect.Width > 44)
        {
            var text = new FormattedText(scheduleEvent.Title, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, _typeface, 11.5, Palette.EventText, dpi)
            {
                MaxTextWidth = rect.Width - 12,
                MaxLineCount = 1,
                Trimming = TextTrimming.CharacterEllipsis,
            };
            dc.DrawText(text, new Point(rect.X + 8, rect.Y + (rect.Height - text.Height) / 2));
        }
    }

    private void DrawRuler(DrawingContext dc, Schedule schedule, double from, double to, int step, double width, double nowX, double dpi)
    {
        dc.DrawRectangle(Palette.Header, null, new Rect(0, 0, width, RulerHeight));
        dc.PushClip(new RectangleGeometry(new Rect(LabelWidth, 0, Math.Max(0, width - LabelWidth), RulerHeight)));

        foreach (var tick in TimeAxis.Ticks(from, to, step))
        {
            var x = MinuteToX(tick);
            dc.DrawLine(Palette.TickPen, new Point(x, RulerHeight - 8), new Point(x, RulerHeight));
            var label = new FormattedText(TimeAxis.Label(schedule.Origin, tick, step), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tick % 1440 == 0 ? _boldTypeface : _typeface, 11, Palette.MutedText, dpi);
            dc.DrawText(label, new Point(x + 5, 9));
        }

        if (nowX >= LabelWidth && nowX <= width)
        {
            var tag = new FormattedText("NOW", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _boldTypeface, 10, Brushes.White, dpi);
            var box = new Rect(nowX - tag.Width / 2 - 6, RulerHeight - tag.Height - 6, tag.Width + 12, tag.Height + 4);
            dc.DrawRoundedRectangle(Palette.NowBrush, null, box, 4, 4);
            dc.DrawText(tag, new Point(box.X + 6, box.Y + 2));
        }

        dc.Pop();
        dc.DrawLine(Palette.BorderPen, new Point(0, RulerHeight - 0.5), new Point(width, RulerHeight - 0.5));
    }

    private void DrawLabels(DrawingContext dc, int firstLayer, int lastLayer, double height, double dpi)
    {
        dc.PushClip(new RectangleGeometry(new Rect(0, RulerHeight, LabelWidth, Math.Max(0, height - RulerHeight))));
        dc.DrawRectangle(Palette.Header, null, new Rect(0, RulerHeight, LabelWidth, height));

        for (var layer = firstLayer; layer <= lastLayer; layer++)
        {
            var text = new FormattedText($"Layer {layer + 1:00}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _boldTypeface, 11.5, Palette.SecondaryText, dpi);
            dc.DrawText(text, new Point(14, RowTop(layer) + (RowHeight - text.Height) / 2));
        }

        dc.Pop();
        dc.DrawLine(Palette.BorderPen, new Point(LabelWidth - 0.5, 0), new Point(LabelWidth - 0.5, height));
    }

    /// <summary>Frozen brushes and pens shared by every frame.</summary>
    private static class Palette
    {
        public static readonly Brush Surface = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)));
        public static readonly Brush Header = Freeze(new SolidColorBrush(Color.FromRgb(0xF9, 0xFA, 0xFB)));
        public static readonly Brush RowEven = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)));
        public static readonly Brush RowOdd = Freeze(new SolidColorBrush(Color.FromRgb(0xF9, 0xFA, 0xFB)));
        public static readonly Brush MutedText = Freeze(new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)));
        public static readonly Brush SecondaryText = Freeze(new SolidColorBrush(Color.FromRgb(0x37, 0x41, 0x51)));
        public static readonly Brush EventText = Freeze(new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37)));
        public static readonly Brush NowBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x4F, 0x46, 0xE5)));
        public static readonly Pen GridLine = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(0xEE, 0xF0, 0xF4))), 1));
        public static readonly Pen DayLine = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(0xC7, 0xD2, 0xFE))), 1.5));
        public static readonly Pen TickPen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB))), 1));
        public static readonly Pen BorderPen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB))), 1));
        public static readonly Pen HoverPen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(0x11, 0x18, 0x27))), 1.5));
        public static readonly Pen NowPen = Freeze(new Pen(NowBrush, 2));

        private static readonly (Brush Fill, Brush Accent) Pending = (Soft(0xFE, 0xF3, 0xC7), Strong(0xF5, 0x9E, 0x0B));
        private static readonly (Brush Fill, Brush Accent) Jeopardy = (Soft(0xFE, 0xE2, 0xE2), Strong(0xEF, 0x44, 0x44));
        private static readonly (Brush Fill, Brush Accent) Completed = (Soft(0xD1, 0xFA, 0xE5), Strong(0x10, 0xB9, 0x81));

        public static (Brush Fill, Brush Accent) ForStatus(EventStatus status) => status switch
        {
            EventStatus.Pending => Pending,
            EventStatus.Jeopardy => Jeopardy,
            _ => Completed,
        };

        private static Brush Soft(byte r, byte g, byte b) => Freeze(new SolidColorBrush(Color.FromRgb(r, g, b)));

        private static Brush Strong(byte r, byte g, byte b) => Freeze(new SolidColorBrush(Color.FromRgb(r, g, b)));

        private static T Freeze<T>(T freezable)
            where T : Freezable
        {
            freezable.Freeze();
            return freezable;
        }
    }
}
