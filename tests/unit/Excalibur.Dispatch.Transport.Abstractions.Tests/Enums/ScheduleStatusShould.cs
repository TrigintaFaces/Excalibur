using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Enums;

public class ScheduleStatusShould
{
    [Theory]
    [InlineData(ScheduleStatus.Scheduled, 0)]
    [InlineData(ScheduleStatus.InProgress, 1)]
    [InlineData(ScheduleStatus.Completed, 2)]
    [InlineData(ScheduleStatus.Failed, 3)]
    [InlineData(ScheduleStatus.Cancelled, 4)]
    public void Should_Have_Correct_Values(ScheduleStatus status, int expected)
    {
        ((int)status).ShouldBe(expected);
    }

    [Fact]
    public void Should_Have_Five_Values()
    {
        Enum.GetValues<ScheduleStatus>().Length.ShouldBe(5);
    }
}
