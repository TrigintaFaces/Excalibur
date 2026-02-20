using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.BatchProcessing;

public class ProcessedMessageShould
{
    [Fact]
    public void Should_Default_MessageId_To_Empty()
    {
        var msg = new ProcessedMessage();

        msg.MessageId.ShouldBe(string.Empty);
    }

    [Fact]
    public void Should_Set_ProcessedAt_To_Now()
    {
        var before = DateTimeOffset.UtcNow;
        var msg = new ProcessedMessage();
        var after = DateTimeOffset.UtcNow;

        msg.ProcessedAt.ShouldBeGreaterThanOrEqualTo(before);
        msg.ProcessedAt.ShouldBeLessThanOrEqualTo(after);
    }

    [Fact]
    public void Should_Default_Success_To_False()
    {
        var msg = new ProcessedMessage();

        msg.Success.ShouldBeFalse();
    }

    [Fact]
    public void Should_Default_ErrorMessage_To_Null()
    {
        var msg = new ProcessedMessage();

        msg.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Should_Allow_Setting_All_Properties()
    {
        var msg = new ProcessedMessage
        {
            MessageId = "msg-1",
            Duration = TimeSpan.FromMilliseconds(100),
            Success = true,
            ErrorMessage = null,
        };

        msg.MessageId.ShouldBe("msg-1");
        msg.Duration.ShouldBe(TimeSpan.FromMilliseconds(100));
        msg.Success.ShouldBeTrue();
    }
}
