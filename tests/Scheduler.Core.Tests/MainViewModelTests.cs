using Scheduler.Core.Services;
using Scheduler.Core.ViewModels;

namespace Scheduler.Core.Tests;

public class MainViewModelTests
{
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static MainViewModel Create() =>
        new(new ScheduleGenerator(new Random(9)), new FixedTime(new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero)));

    [Fact]
    public async Task Generate_CreatesScheduleFromInputs()
    {
        var vm = Create();
        vm.LayersInput = "12";
        vm.EventsInput = "1 500";

        await vm.GenerateCommand.ExecuteAsync(null);

        Assert.Equal(12, vm.Schedule.LayerCount);
        Assert.Equal(1500, vm.Schedule.EventCount);
        Assert.Equal(new DateTime(2026, 10, 1), vm.Schedule.Origin);
        Assert.Contains("1,500 events", vm.GenerationText);
        Assert.False(vm.IsGenerating);
        Assert.Equal("October 3rd, Saturday", vm.DateText);
    }

    [Theory]
    [InlineData("abc", "100", "whole numbers")]
    [InlineData("0", "100", "Layers")]
    [InlineData("10", "2000000", "Events")]
    public void InvalidInput_DisablesGenerate(string layers, string events, string expected)
    {
        var vm = Create();

        vm.LayersInput = layers;
        vm.EventsInput = events;

        Assert.Contains(expected, vm.InputError);
        Assert.False(vm.GenerateCommand.CanExecute(null));
    }

    [Fact]
    public async Task Advance_MovesPlayheadOnlyWhilePlaying()
    {
        var vm = Create();
        await vm.GenerateCommand.ExecuteAsync(null);
        vm.Speed = 30;

        vm.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(MainViewModel.InitialNow, vm.Now);

        vm.TogglePlayCommand.Execute(null);
        vm.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(MainViewModel.InitialNow + 60, vm.Now);
        Assert.Equal("Pause", vm.PlayLabel);
        Assert.Equal("Sat 09:00", vm.NowText);
    }

    [Fact]
    public async Task Advance_WrapsAroundAtEndOfSchedule()
    {
        var vm = Create();
        await vm.GenerateCommand.ExecuteAsync(null);
        vm.TogglePlayCommand.Execute(null);
        vm.Now = vm.Schedule.Length - 1;

        vm.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(0, vm.Now);
    }

    [Fact]
    public void Zoom_IsClamped()
    {
        var vm = Create();

        vm.Zoom = 100;
        Assert.Equal(MainViewModel.MaxZoom, vm.Zoom);

        vm.Zoom = 0;
        Assert.Equal(MainViewModel.MinZoom, vm.Zoom);
    }

    [Fact]
    public async Task ReportHover_DescribesEvent()
    {
        var vm = Create();
        await vm.GenerateCommand.ExecuteAsync(null);
        var e = vm.Schedule.GetLayer(0)[0];

        vm.ReportHover(e);

        Assert.StartsWith(e.Title, vm.HoverText);
        Assert.Contains("Layer 1", vm.HoverText);
    }
}
