using Scheduler.Core.Models;

namespace Scheduler.Core.Services;

/// <summary>Builds realistic random schedules: jobs of 15 min – 3 h with gaps, finished in the past and planned ahead.</summary>
public sealed class ScheduleGenerator(Random random)
{
    public const int MinLayers = 1;
    public const int MaxLayers = 200;
    public const int MinEvents = 1;
    public const int MaxEvents = 1_000_000;

    private static readonly string[] Jobs =
    [
        "Inspection", "Delivery", "Maintenance", "Installation", "Survey", "Repair",
        "Calibration", "Pickup", "Audit", "Training", "Setup", "Testing",
    ];

    public ScheduleGenerator()
        : this(Random.Shared)
    {
    }

    /// <summary>Returns an error message, or <see langword="null"/> if the parameters are valid.</summary>
    public static string? Validate(int layers, int events)
    {
        if (layers is < MinLayers or > MaxLayers)
        {
            return $"Layers must be between {MinLayers} and {MaxLayers}.";
        }

        if (events is < MinEvents or > MaxEvents)
        {
            return $"Events must be between {MinEvents} and {MaxEvents:N0}.";
        }

        return null;
    }

    /// <param name="now">Minute that separates the past (mostly completed jobs) from the future (pending jobs).</param>
    public Schedule Generate(int layers, int events, DateTime origin, int now)
    {
        if (Validate(layers, events) is { } error)
        {
            throw new ArgumentOutOfRangeException(nameof(events), error);
        }

        var result = new List<ScheduleEvent>[layers];
        var id = 0;

        for (var layer = 0; layer < layers; layer++)
        {
            // Spread the events evenly; the first layers take the remainder.
            var count = events / layers + (layer < events % layers ? 1 : 0);
            var list = new List<ScheduleEvent>(count);
            var time = random.Next(0, 90);

            for (var i = 0; i < count; i++)
            {
                var duration = random.Next(1, 13) * 15;
                var status = PickStatus(time + duration, now);
                list.Add(new ScheduleEvent(++id, layer, time, duration, status, $"{Jobs[random.Next(Jobs.Length)]} #{id}"));
                time += duration + random.Next(0, 9) * 15;
            }

            result[layer] = list;
        }

        return new Schedule(result, origin);
    }

    private EventStatus PickStatus(int end, int now)
    {
        var roll = random.NextDouble();
        return end <= now
            ? roll < 0.85 ? EventStatus.Completed : EventStatus.Jeopardy
            : roll < 0.8 ? EventStatus.Pending : EventStatus.Jeopardy;
    }
}
