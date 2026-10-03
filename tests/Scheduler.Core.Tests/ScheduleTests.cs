using Scheduler.Core.Models;
using Scheduler.Core.Services;

namespace Scheduler.Core.Tests;

public class ScheduleTests
{
    private static readonly DateTime Origin = new(2026, 10, 3);

    private static ScheduleEvent E(int id, int layer, int start, int duration, EventStatus status = EventStatus.Pending) =>
        new(id, layer, start, duration, status, $"Job {id}");

    [Fact]
    public void Query_ReturnsEventsOverlappingWindow()
    {
        var schedule = new Schedule([[E(1, 0, 0, 10), E(2, 0, 10, 10), E(3, 0, 30, 10)]], Origin);

        Assert.Equal([2], schedule.Query(0, 15, 25).Select(e => e.Id));
        Assert.Equal([1, 2], schedule.Query(0, 5, 15).Select(e => e.Id));
        Assert.Equal([2, 3], schedule.Query(0, 19.5, 31).Select(e => e.Id));
        Assert.Empty(schedule.Query(0, 20, 30));
        Assert.Empty(schedule.Query(5, 0, 100));
    }

    [Fact]
    public void FindAt_ReturnsEventUnderMinute()
    {
        var schedule = new Schedule([[E(1, 0, 0, 10), E(2, 0, 20, 10)]], Origin);

        Assert.Equal(1, schedule.FindAt(0, 0)?.Id);
        Assert.Equal(1, schedule.FindAt(0, 9.9)?.Id);
        Assert.Null(schedule.FindAt(0, 10));
        Assert.Equal(2, schedule.FindAt(0, 25)?.Id);
        Assert.Null(schedule.FindAt(1, 25));
    }

    [Fact]
    public void Statistics_CountEveryEvent()
    {
        var schedule = new Schedule(
        [
            [E(1, 0, 0, 10, EventStatus.Completed), E(2, 0, 10, 5, EventStatus.Jeopardy)],
            [E(3, 1, 0, 50, EventStatus.Pending)],
        ], Origin);

        Assert.Equal(3, schedule.EventCount);
        Assert.Equal(2, schedule.LayerCount);
        Assert.Equal(50, schedule.Length);
        Assert.Equal((1, 1, 1), (schedule.PendingCount, schedule.JeopardyCount, schedule.CompletedCount));
    }

    [Fact]
    public void Query_KeepsEventsWithEqualStartAndOverlaps()
    {
        // Regression: the first version used an AVL tree keyed by start time only, which dropped duplicates.
        var schedule = new Schedule([[E(1, 0, 0, 10), E(2, 0, 0, 30), E(3, 0, 5, 10)]], Origin);

        Assert.Equal(3, schedule.EventCount);
        Assert.Equal([1, 2, 3], schedule.Query(0, 0, 100).Select(e => e.Id));
        Assert.Equal([2], schedule.Query(0, 20, 25).Select(e => e.Id));
        Assert.Equal(3, schedule.FindAt(0, 7)?.Id);
    }

    [Fact]
    public void Constructor_SortsEventsByStart()
    {
        var schedule = new Schedule([[E(2, 0, 20, 5), E(1, 0, 0, 5)]], Origin);

        Assert.Equal([1, 2], schedule.GetLayer(0).Select(e => e.Id));
    }

    [Fact]
    public void Generator_CreatesExactlyTheRequestedNumberOfEvents()
    {
        // Regression: the first version kept events in an AVL tree keyed by start time, which silently dropped
        // every event whose start collided with another one, and generated one extra event per layer.
        var schedule = new ScheduleGenerator(new Random(1)).Generate(7, 1000, Origin, 480);

        Assert.Equal(1000, schedule.EventCount);
        Assert.Equal(7, schedule.LayerCount);
        Assert.Equal(1000, schedule.PendingCount + schedule.JeopardyCount + schedule.CompletedCount);
        Assert.Equal(1000, Enumerable.Range(0, 7).SelectMany(schedule.GetLayer).Select(e => e.Id).Distinct().Count());
    }

    [Fact]
    public void Generator_MarksPastJobsMostlyCompletedAndFutureJobsNeverCompleted()
    {
        var schedule = new ScheduleGenerator(new Random(2)).Generate(10, 2000, Origin, now: 600);
        var events = Enumerable.Range(0, 10).SelectMany(schedule.GetLayer).ToList();

        Assert.DoesNotContain(events, e => e.Start >= 600 && e.Status == EventStatus.Completed);
        Assert.Contains(events, e => e.End <= 600 && e.Status == EventStatus.Completed);
    }

    [Fact]
    public void Generator_IsReproducibleWithSeed()
    {
        var a = new ScheduleGenerator(new Random(5)).Generate(3, 50, Origin, 0);
        var b = new ScheduleGenerator(new Random(5)).Generate(3, 50, Origin, 0);

        Assert.Equal(a.GetLayer(2), b.GetLayer(2));
    }

    [Theory]
    [InlineData(0, 10, "Layers")]
    [InlineData(201, 10, "Layers")]
    [InlineData(5, 0, "Events")]
    [InlineData(5, 1_000_001, "Events")]
    public void Generator_ValidatesParameters(int layers, int events, string expected) =>
        Assert.Contains(expected, ScheduleGenerator.Validate(layers, events));

    [Fact]
    public void Query_MatchesBruteForceOnRandomSchedule()
    {
        var schedule = new ScheduleGenerator(new Random(3)).Generate(5, 5000, Origin, 0);
        var random = new Random(4);

        for (var i = 0; i < 200; i++)
        {
            var layer = random.Next(5);
            double from = random.Next(schedule.Length);
            var to = from + random.Next(1, 600);

            var expected = schedule.GetLayer(layer).Where(e => e.Overlaps(from, to)).Select(e => e.Id);
            Assert.Equal(expected, schedule.Query(layer, from, to).Select(e => e.Id));
        }
    }
}
