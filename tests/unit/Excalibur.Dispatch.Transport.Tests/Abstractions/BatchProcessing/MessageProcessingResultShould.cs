// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.BatchProcessing;

[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public class MessageProcessingResultShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var result = new MessageProcessingResult();

        result.MessageId.ShouldBe(string.Empty);
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBeNull();
        result.Exception.ShouldBeNull();
        result.ProcessingDuration.ShouldBe(TimeSpan.Zero);
        result.ShouldRetry.ShouldBeFalse();
        result.MovedToDeadLetter.ShouldBeFalse();
    }

    [Fact]
    public void AllowSettingMessageId()
    {
        var result = new MessageProcessingResult { MessageId = "msg-12345" };

        result.MessageId.ShouldBe("msg-12345");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowSettingIsSuccess(bool isSuccess)
    {
        var result = new MessageProcessingResult { IsSuccess = isSuccess };

        result.IsSuccess.ShouldBe(isSuccess);
    }

    [Fact]
    public void AllowSettingErrorMessage()
    {
        var result = new MessageProcessingResult { ErrorMessage = "Processing failed" };

        result.ErrorMessage.ShouldBe("Processing failed");
    }

    [Fact]
    public void AllowSettingException()
    {
        var ex = new InvalidOperationException("Test error");
        var result = new MessageProcessingResult { Exception = ex };

        result.Exception.ShouldBe(ex);
        result.Exception.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void AllowSettingProcessingDuration()
    {
        var duration = TimeSpan.FromMilliseconds(500);
        var result = new MessageProcessingResult { ProcessingDuration = duration };

        result.ProcessingDuration.ShouldBe(duration);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowSettingShouldRetry(bool shouldRetry)
    {
        var result = new MessageProcessingResult { ShouldRetry = shouldRetry };

        result.ShouldRetry.ShouldBe(shouldRetry);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowSettingMovedToDeadLetter(bool movedToDeadLetter)
    {
        var result = new MessageProcessingResult { MovedToDeadLetter = movedToDeadLetter };

        result.MovedToDeadLetter.ShouldBe(movedToDeadLetter);
    }

    [Fact]
    public void AllowSettingAllPropertiesForSuccessCase()
    {
        var result = new MessageProcessingResult
        {
            MessageId = "msg-success",
            IsSuccess = true,
            ProcessingDuration = TimeSpan.FromMilliseconds(100)
        };

        result.MessageId.ShouldBe("msg-success");
        result.IsSuccess.ShouldBeTrue();
        result.ProcessingDuration.ShouldBe(TimeSpan.FromMilliseconds(100));
        result.ErrorMessage.ShouldBeNull();
        result.Exception.ShouldBeNull();
        result.ShouldRetry.ShouldBeFalse();
        result.MovedToDeadLetter.ShouldBeFalse();
    }

    [Fact]
    public void AllowSettingAllPropertiesForFailureCase()
    {
        var ex = new TimeoutException("Request timed out");
        var result = new MessageProcessingResult
        {
            MessageId = "msg-failed",
            IsSuccess = false,
            ErrorMessage = "Processing timed out after 30 seconds",
            Exception = ex,
            ProcessingDuration = TimeSpan.FromSeconds(30),
            ShouldRetry = true,
            MovedToDeadLetter = false
        };

        result.MessageId.ShouldBe("msg-failed");
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Processing timed out after 30 seconds");
        result.Exception.ShouldBe(ex);
        result.ProcessingDuration.ShouldBe(TimeSpan.FromSeconds(30));
        result.ShouldRetry.ShouldBeTrue();
        result.MovedToDeadLetter.ShouldBeFalse();
    }

    [Fact]
    public void AllowSettingAllPropertiesForDeadLetterCase()
    {
        var ex = new InvalidOperationException("Unrecoverable error");
        var result = new MessageProcessingResult
        {
            MessageId = "msg-dlq",
            IsSuccess = false,
            ErrorMessage = "Message could not be processed after max retries",
            Exception = ex,
            ProcessingDuration = TimeSpan.FromSeconds(5),
            ShouldRetry = false,
            MovedToDeadLetter = true
        };

        result.MessageId.ShouldBe("msg-dlq");
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Message could not be processed after max retries");
        result.Exception.ShouldBe(ex);
        result.ShouldRetry.ShouldBeFalse();
        result.MovedToDeadLetter.ShouldBeTrue();
    }
}
