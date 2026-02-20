using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Messaging;

public class BatchSendResultShould
{
    [Fact]
    public void Should_Default_Results_To_Empty()
    {
        var result = new BatchSendResult();

        result.Results.ShouldNotBeNull();
        result.Results.Count.ShouldBe(0);
    }

    [Fact]
    public void IsCompleteSuccess_Should_Be_True_When_All_Succeed()
    {
        var result = new BatchSendResult
        {
            TotalMessages = 5,
            SuccessCount = 5,
            FailureCount = 0,
        };

        result.IsCompleteSuccess.ShouldBeTrue();
    }

    [Fact]
    public void IsCompleteSuccess_Should_Be_False_When_Any_Fail()
    {
        var result = new BatchSendResult
        {
            TotalMessages = 5,
            SuccessCount = 4,
            FailureCount = 1,
        };

        result.IsCompleteSuccess.ShouldBeFalse();
    }

    [Fact]
    public void IsCompleteSuccess_Should_Be_False_When_Success_Less_Than_Total()
    {
        var result = new BatchSendResult
        {
            TotalMessages = 5,
            SuccessCount = 3,
            FailureCount = 0,
        };

        result.IsCompleteSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Should_Allow_Setting_Duration()
    {
        var duration = TimeSpan.FromMilliseconds(150);
        var result = new BatchSendResult { Duration = duration };

        result.Duration.ShouldBe(duration);
    }

    [Fact]
    public void Should_Default_Duration_To_Null()
    {
        var result = new BatchSendResult();

        result.Duration.ShouldBeNull();
    }
}
