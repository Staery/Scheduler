using Scheduler.Core.Services;

namespace Scheduler.Core.Tests;

public class FormattingTests
{
    private static readonly DateTime Origin = new(2026, 10, 3);

    [Theory]
    [InlineData(1, "st")]
    [InlineData(2, "nd")]
    [InlineData(3, "rd")]
    [InlineData(4, "th")]
    [InlineData(11, "th")]
    [InlineData(12, "th")]
    [InlineData(13, "th")]
    [InlineData(21, "st")]
    [InlineData(22, "nd")]
    [InlineData(23, "rd")]
    [InlineData(31, "st")]
    public void OrdinalSuffix(int day, string expected) => Assert.Equal(expected, OrdinalDate.Suffix(day));

    [Fact]
    public void OrdinalDate_Format() => Assert.Equal("October 3rd, Saturday", OrdinalDate.Format(Origin));

    [Theory]
    [InlineData(4.0, 30)]
    [InlineData(1.5, 60)]
    [InlineData(0.5, 180)]
    [InlineData(0.06, 1440)]
    [InlineData(0.05, 2880)]
    [InlineData(0.0001, 10080)]
    public void Step_KeepsLabelsApart(double pixelsPerMinute, int expected) =>
        Assert.Equal(expected, TimeAxis.StepFor(pixelsPerMinute));

    [Fact]
    public void Ticks_AreAlignedToStep() =>
        Assert.Equal([0, 60, 120, 180], TimeAxis.Ticks(45, 200, 60));

    [Theory]
    [InlineData(510, 60, "08:30")]
    [InlineData(1440, 60, "Sun 4 · 00:00")]
    [InlineData(2880, 1440, "Mon 5 Oct")]
    public void Labels(int minute, int step, string expected) =>
        Assert.Equal(expected, TimeAxis.Label(Origin, minute, step));
}
