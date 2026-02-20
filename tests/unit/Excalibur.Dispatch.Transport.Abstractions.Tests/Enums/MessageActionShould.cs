using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Enums;

public class MessageActionShould
{
    [Fact]
    public void Should_Have_Acknowledge_Value()
    {
        MessageAction.Acknowledge.ShouldBe((MessageAction)0);
    }

    [Fact]
    public void Should_Have_Reject_Value()
    {
        MessageAction.Reject.ShouldBe((MessageAction)1);
    }

    [Fact]
    public void Should_Have_Requeue_Value()
    {
        MessageAction.Requeue.ShouldBe((MessageAction)2);
    }

    [Fact]
    public void Should_Have_Three_Values()
    {
        Enum.GetValues<MessageAction>().Length.ShouldBe(3);
    }
}
