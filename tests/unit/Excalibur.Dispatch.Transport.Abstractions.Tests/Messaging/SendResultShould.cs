using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Messaging;

public class SendResultShould
{
    [Fact]
    public void Success_Should_Create_Successful_Result()
    {
        var result = SendResult.Success("msg-123");

        result.IsSuccess.ShouldBeTrue();
        result.MessageId.ShouldBe("msg-123");
        result.AcceptedAt.ShouldNotBeNull();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void Success_Should_Set_AcceptedAt_To_Now()
    {
        var before = DateTimeOffset.UtcNow;
        var result = SendResult.Success("msg-1");
        var after = DateTimeOffset.UtcNow;

        result.AcceptedAt!.Value.ShouldBeGreaterThanOrEqualTo(before);
        result.AcceptedAt!.Value.ShouldBeLessThanOrEqualTo(after);
    }

    [Fact]
    public void Failure_Should_Create_Failed_Result()
    {
        var error = new SendError { Code = "TIMEOUT", Message = "Request timed out" };

        var result = SendResult.Failure(error);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(error);
        result.MessageId.ShouldBeNull();
    }

    [Fact]
    public void Should_Support_Init_Properties()
    {
        var result = new SendResult
        {
            IsSuccess = true,
            MessageId = "abc",
            SequenceNumber = 42L,
            Partition = "p-0",
            AcceptedAt = DateTimeOffset.UtcNow,
        };

        result.IsSuccess.ShouldBeTrue();
        result.MessageId.ShouldBe("abc");
        result.SequenceNumber.ShouldBe(42L);
        result.Partition.ShouldBe("p-0");
    }
}
