using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Enums;

public class MessagingEnumsShould
{
    [Theory]
    [InlineData(AcknowledgmentMode.OnSuccess, 0)]
    [InlineData(AcknowledgmentMode.Immediate, 1)]
    [InlineData(AcknowledgmentMode.Manual, 2)]
    public void AcknowledgmentMode_Should_Have_Correct_Values(AcknowledgmentMode mode, int expected)
    {
        ((int)mode).ShouldBe(expected);
    }

    [Theory]
    [InlineData(DeadLetterStrategy.Drop, 0)]
    [InlineData(DeadLetterStrategy.MoveToDeadLetterQueue, 1)]
    [InlineData(DeadLetterStrategy.RetryIndefinitely, 2)]
    [InlineData(DeadLetterStrategy.CustomHandler, 3)]
    public void DeadLetterStrategy_Should_Have_Correct_Values(DeadLetterStrategy strategy, int expected)
    {
        ((int)strategy).ShouldBe(expected);
    }

    [Theory]
    [InlineData(ErrorHandlingStrategy.Retry, 0)]
    [InlineData(ErrorHandlingStrategy.DeadLetter, 1)]
    [InlineData(ErrorHandlingStrategy.Ignore, 2)]
    [InlineData(ErrorHandlingStrategy.Throw, 3)]
    public void ErrorHandlingStrategy_Should_Have_Correct_Values(ErrorHandlingStrategy strategy, int expected)
    {
        ((int)strategy).ShouldBe(expected);
    }

    [Theory]
    [InlineData(OversizedMessageBehavior.SendSeparately, 0)]
    [InlineData(OversizedMessageBehavior.Skip, 1)]
    [InlineData(OversizedMessageBehavior.ThrowException, 2)]
    public void OversizedMessageBehavior_Should_Have_Correct_Values(OversizedMessageBehavior behavior, int expected)
    {
        ((int)behavior).ShouldBe(expected);
    }

    [Theory]
    [InlineData(RetryDelayStrategy.Fixed, 0)]
    [InlineData(RetryDelayStrategy.Exponential, 1)]
    [InlineData(RetryDelayStrategy.Linear, 2)]
    public void RetryDelayStrategy_Should_Have_Correct_Values(RetryDelayStrategy strategy, int expected)
    {
        ((int)strategy).ShouldBe(expected);
    }

    [Theory]
    [InlineData(MessagePriority.Low, 0)]
    [InlineData(MessagePriority.Normal, 1)]
    [InlineData(MessagePriority.High, 2)]
    [InlineData(MessagePriority.Critical, 3)]
    public void MessagePriority_Should_Have_Correct_Values(MessagePriority priority, int expected)
    {
        ((int)priority).ShouldBe(expected);
    }

    [Theory]
    [InlineData(EntityStatus.Active, 0)]
    [InlineData(EntityStatus.Disabled, 1)]
    [InlineData(EntityStatus.ReceiveDisabled, 2)]
    [InlineData(EntityStatus.SendDisabled, 3)]
    public void EntityStatus_Should_Have_Correct_Values(EntityStatus status, int expected)
    {
        ((int)status).ShouldBe(expected);
    }
}
