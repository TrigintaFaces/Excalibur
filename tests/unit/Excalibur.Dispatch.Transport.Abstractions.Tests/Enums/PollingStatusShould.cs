using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Enums;

public class PollingStatusShould
{
    [Theory]
    [InlineData(PollingStatus.Idle, 0)]
    [InlineData(PollingStatus.Running, 1)]
    [InlineData(PollingStatus.Paused, 2)]
    [InlineData(PollingStatus.Stopped, 3)]
    [InlineData(PollingStatus.Error, 4)]
    public void Should_Have_Correct_Values(PollingStatus status, int expected)
    {
        ((int)status).ShouldBe(expected);
    }

    [Fact]
    public void Should_Have_Five_Values()
    {
        Enum.GetValues<PollingStatus>().Length.ShouldBe(5);
    }
}
