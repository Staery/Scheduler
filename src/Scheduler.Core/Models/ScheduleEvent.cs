namespace Scheduler.Core.Models;

public enum EventStatus
{
    Pending,
    Jeopardy,
    Completed,
}

/// <summary>A job on the timeline. Times are whole minutes from the start of the schedule.</summary>
public sealed record ScheduleEvent(int Id, int Layer, int Start, int Duration, EventStatus Status, string Title)
{
    public int End => Start + Duration;

    public bool Overlaps(double from, double to) => Start < to && End > from;
}
