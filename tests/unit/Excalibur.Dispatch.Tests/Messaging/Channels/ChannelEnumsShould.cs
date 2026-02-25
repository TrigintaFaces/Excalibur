using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ChannelEnumsShould
{
    [Theory]
    [InlineData(ChannelMode.Unbounded, 0)]
    [InlineData(ChannelMode.Bounded, 1)]
    public void HaveCorrectChannelModeValues(ChannelMode mode, int expected)
    {
        ((int)mode).ShouldBe(expected);
    }

    [Theory]
    [InlineData(ChannelMessagePumpStatus.NotStarted, 0)]
    [InlineData(ChannelMessagePumpStatus.Starting, 1)]
    [InlineData(ChannelMessagePumpStatus.Running, 2)]
    [InlineData(ChannelMessagePumpStatus.Stopping, 3)]
    [InlineData(ChannelMessagePumpStatus.Stopped, 4)]
    [InlineData(ChannelMessagePumpStatus.Faulted, 5)]
    public void HaveCorrectChannelMessagePumpStatusValues(ChannelMessagePumpStatus status, int expected)
    {
        ((int)status).ShouldBe(expected);
    }

    [Fact]
    public void HaveAllChannelModeValues()
    {
        var values = Enum.GetValues<ChannelMode>();
        values.Length.ShouldBe(2);
    }

    [Fact]
    public void HaveAllChannelMessagePumpStatusValues()
    {
        var values = Enum.GetValues<ChannelMessagePumpStatus>();
        values.Length.ShouldBe(6);
    }
}
