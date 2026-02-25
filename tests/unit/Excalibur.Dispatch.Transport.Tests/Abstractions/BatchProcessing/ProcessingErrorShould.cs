// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.BatchProcessing;

[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public class ProcessingErrorShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var error = new ProcessingError();

        error.Code.ShouldBe(string.Empty);
        error.Message.ShouldBe(string.Empty);
        error.Severity.ShouldBe(default(ErrorSeverity));
        error.OccurredAt.ShouldNotBe(default);
        error.MessageId.ShouldBeNull();
        error.Exception.ShouldBeNull();
    }

    [Fact]
    public void SetOccurredAtToUtcNowByDefault()
    {
        var before = DateTimeOffset.UtcNow;
        var error = new ProcessingError();
        var after = DateTimeOffset.UtcNow;

        error.OccurredAt.ShouldBeGreaterThanOrEqualTo(before);
        error.OccurredAt.ShouldBeLessThanOrEqualTo(after);
    }

    [Theory]
    [InlineData("ERR001")]
    [InlineData("TIMEOUT")]
    [InlineData("VALIDATION_FAILED")]
    public void AllowSettingCode(string code)
    {
        var error = new ProcessingError { Code = code };

        error.Code.ShouldBe(code);
    }

    [Theory]
    [InlineData("Connection timed out")]
    [InlineData("Invalid message format")]
    [InlineData("Authentication failed")]
    public void AllowSettingMessage(string message)
    {
        var error = new ProcessingError { Message = message };

        error.Message.ShouldBe(message);
    }

    [Theory]
    [InlineData(ErrorSeverity.Info)]
    [InlineData(ErrorSeverity.Warning)]
    [InlineData(ErrorSeverity.Error)]
    [InlineData(ErrorSeverity.Critical)]
    public void AllowSettingSeverity(ErrorSeverity severity)
    {
        var error = new ProcessingError { Severity = severity };

        error.Severity.ShouldBe(severity);
    }

    [Fact]
    public void AllowSettingOccurredAt()
    {
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-5);
        var error = new ProcessingError { OccurredAt = timestamp };

        error.OccurredAt.ShouldBe(timestamp);
    }

    [Fact]
    public void AllowSettingMessageId()
    {
        var error = new ProcessingError { MessageId = "msg-12345" };

        error.MessageId.ShouldBe("msg-12345");
    }

    [Fact]
    public void AllowSettingException()
    {
        var ex = new InvalidOperationException("Test exception");
        var error = new ProcessingError { Exception = ex };

        error.Exception.ShouldBe(ex);
        error.Exception.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void AllowSettingAllPropertiesForWarning()
    {
        var error = new ProcessingError
        {
            Code = "RATE_LIMIT",
            Message = "Rate limit exceeded for message processing",
            Severity = ErrorSeverity.Warning,
            MessageId = "msg-456"
        };

        error.Code.ShouldBe("RATE_LIMIT");
        error.Message.ShouldBe("Rate limit exceeded for message processing");
        error.Severity.ShouldBe(ErrorSeverity.Warning);
        error.MessageId.ShouldBe("msg-456");
        error.Exception.ShouldBeNull();
    }

    [Fact]
    public void AllowSettingAllPropertiesForCriticalError()
    {
        var ex = new InvalidOperationException("System encountered a critical failure");
        var timestamp = DateTimeOffset.UtcNow;

        var error = new ProcessingError
        {
            Code = "CRITICAL_FAILURE",
            Message = "System encountered a critical error during batch processing",
            Severity = ErrorSeverity.Critical,
            OccurredAt = timestamp,
            MessageId = "msg-789",
            Exception = ex
        };

        error.Code.ShouldBe("CRITICAL_FAILURE");
        error.Message.ShouldBe("System encountered a critical error during batch processing");
        error.Severity.ShouldBe(ErrorSeverity.Critical);
        error.OccurredAt.ShouldBe(timestamp);
        error.MessageId.ShouldBe("msg-789");
        error.Exception.ShouldBe(ex);
    }

    [Fact]
    public void AllowSettingBatchLevelError()
    {
        var error = new ProcessingError
        {
            Code = "BATCH_TIMEOUT",
            Message = "Batch processing exceeded maximum time limit",
            Severity = ErrorSeverity.Error
        };

        error.Code.ShouldBe("BATCH_TIMEOUT");
        error.Message.ShouldBe("Batch processing exceeded maximum time limit");
        error.Severity.ShouldBe(ErrorSeverity.Error);
        error.MessageId.ShouldBeNull();
    }
}
