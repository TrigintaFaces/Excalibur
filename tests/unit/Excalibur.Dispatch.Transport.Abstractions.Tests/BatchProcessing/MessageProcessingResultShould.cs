using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.BatchProcessing;

public class MessageProcessingResultShould
{
    [Fact]
    public void Should_Default_MessageId_To_Empty()
    {
        var result = new MessageProcessingResult();

        result.MessageId.ShouldBe(string.Empty);
    }

    [Fact]
    public void Should_Default_IsSuccess_To_False()
    {
        var result = new MessageProcessingResult();

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Should_Default_ErrorMessage_To_Null()
    {
        var result = new MessageProcessingResult();

        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Should_Default_Exception_To_Null()
    {
        var result = new MessageProcessingResult();

        result.Exception.ShouldBeNull();
    }

    [Fact]
    public void Should_Default_ShouldRetry_To_False()
    {
        var result = new MessageProcessingResult();

        result.ShouldRetry.ShouldBeFalse();
    }

    [Fact]
    public void Should_Default_MovedToDeadLetter_To_False()
    {
        var result = new MessageProcessingResult();

        result.MovedToDeadLetter.ShouldBeFalse();
    }

    [Fact]
    public void Should_Allow_Setting_All_Properties()
    {
        var ex = new InvalidOperationException("test");
        var result = new MessageProcessingResult
        {
            MessageId = "msg-1",
            IsSuccess = true,
            ErrorMessage = "partial",
            Exception = ex,
            ProcessingDuration = TimeSpan.FromMilliseconds(50),
            ShouldRetry = true,
            MovedToDeadLetter = true,
        };

        result.MessageId.ShouldBe("msg-1");
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("partial");
        result.Exception.ShouldBe(ex);
        result.ProcessingDuration.ShouldBe(TimeSpan.FromMilliseconds(50));
        result.ShouldRetry.ShouldBeTrue();
        result.MovedToDeadLetter.ShouldBeTrue();
    }
}
