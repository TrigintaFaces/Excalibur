using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Enums;

public class DestinationTypeShould
{
    [Theory]
    [InlineData(DestinationType.Queue, 0)]
    [InlineData(DestinationType.Topic, 1)]
    [InlineData(DestinationType.Subscription, 2)]
    public void Should_Have_Correct_Values(DestinationType type, int expected)
    {
        ((int)type).ShouldBe(expected);
    }

    [Fact]
    public void Should_Have_Three_Values()
    {
        Enum.GetValues<DestinationType>().Length.ShouldBe(3);
    }
}
