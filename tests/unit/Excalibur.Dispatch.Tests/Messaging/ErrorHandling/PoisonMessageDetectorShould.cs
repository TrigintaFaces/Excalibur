// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Options.ErrorHandling;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.ErrorHandling;

[Trait("Category", "Unit")]
public sealed class PoisonMessageDetectorShould
{
    private static MessageProcessingInfo CreateProcessingInfo(int attemptCount = 1) => new()
    {
        AttemptCount = attemptCount,
        FirstAttemptTime = DateTimeOffset.UtcNow.AddMinutes(-5),
        CurrentAttemptTime = DateTimeOffset.UtcNow
    };

    // --- RetryCountPoisonDetector tests ---

    [Fact]
    public async Task DetectPoisonWhenRetryCountExceeded()
    {
        var options = new PoisonMessageOptions { MaxRetryAttempts = 3 };
        var detector = new RetryCountPoisonDetector(Microsoft.Extensions.Options.Options.Create(options));
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        var info = CreateProcessingInfo(attemptCount: 5);

        var result = await detector.IsPoisonMessageAsync(message, context, info);

        result.IsPoison.ShouldBeTrue();
        result.DetectorName.ShouldBe(nameof(RetryCountPoisonDetector));
    }

    [Fact]
    public async Task NotDetectPoisonWhenRetryCountWithinLimit()
    {
        var options = new PoisonMessageOptions { MaxRetryAttempts = 5 };
        var detector = new RetryCountPoisonDetector(Microsoft.Extensions.Options.Options.Create(options));
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        var info = CreateProcessingInfo(attemptCount: 2);

        var result = await detector.IsPoisonMessageAsync(message, context, info);

        result.IsPoison.ShouldBeFalse();
    }

    [Fact]
    public async Task DetectPoisonAtExactThreshold()
    {
        var options = new PoisonMessageOptions { MaxRetryAttempts = 3 };
        var detector = new RetryCountPoisonDetector(Microsoft.Extensions.Options.Options.Create(options));
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        var info = CreateProcessingInfo(attemptCount: 3);

        var result = await detector.IsPoisonMessageAsync(message, context, info);

        result.IsPoison.ShouldBeTrue();
    }

    // --- ExceptionTypePoisonDetector tests ---

    [Fact]
    public async Task DetectPoisonForConfiguredExceptionType()
    {
        var options = new PoisonMessageOptions();
        options.PoisonExceptionTypes.Add(typeof(FormatException));
        var detector = new ExceptionTypePoisonDetector(Microsoft.Extensions.Options.Options.Create(options));
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        var info = CreateProcessingInfo();

        var result = await detector.IsPoisonMessageAsync(message, context, info, new FormatException("bad format"));

        result.IsPoison.ShouldBeTrue();
        result.DetectorName.ShouldBe(nameof(ExceptionTypePoisonDetector));
    }

    [Fact]
    public async Task NotDetectPoisonForTransientException()
    {
        var options = new PoisonMessageOptions();
        options.TransientExceptionTypes.Add(typeof(TimeoutException));
        options.PoisonExceptionTypes.Add(typeof(TimeoutException));
        var detector = new ExceptionTypePoisonDetector(Microsoft.Extensions.Options.Options.Create(options));
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        var info = CreateProcessingInfo();

        var result = await detector.IsPoisonMessageAsync(message, context, info, new TimeoutException());

        result.IsPoison.ShouldBeFalse();
    }

    [Fact]
    public async Task NotDetectPoisonWhenNoException()
    {
        var options = new PoisonMessageOptions();
        options.PoisonExceptionTypes.Add(typeof(FormatException));
        var detector = new ExceptionTypePoisonDetector(Microsoft.Extensions.Options.Options.Create(options));
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        var info = CreateProcessingInfo();

        var result = await detector.IsPoisonMessageAsync(message, context, info, exception: null);

        result.IsPoison.ShouldBeFalse();
    }

    // --- CompositePoisonDetector tests ---

    [Fact]
    public async Task ReturnPoisonFromFirstDetectingDetector()
    {
        var detector1 = A.Fake<IPoisonMessageDetector>();
        A.CallTo(() => detector1.IsPoisonMessageAsync(
                A<IDispatchMessage>._, A<IMessageContext>._, A<MessageProcessingInfo>._, A<Exception?>._))
            .Returns(Task.FromResult(PoisonDetectionResult.NotPoison()));

        var detector2 = A.Fake<IPoisonMessageDetector>();
        A.CallTo(() => detector2.IsPoisonMessageAsync(
                A<IDispatchMessage>._, A<IMessageContext>._, A<MessageProcessingInfo>._, A<Exception?>._))
            .Returns(Task.FromResult(PoisonDetectionResult.Poison("Too many retries", "RetryDetector")));

        var composite = new CompositePoisonDetector(
            new[] { detector1, detector2 },
            NullLogger<CompositePoisonDetector>.Instance);

        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        var info = CreateProcessingInfo();

        var result = await composite.IsPoisonMessageAsync(message, context, info);

        result.IsPoison.ShouldBeTrue();
        result.DetectorName.ShouldBe("RetryDetector");
    }

    [Fact]
    public async Task ReturnNotPoisonWhenNoDetectorFlags()
    {
        var detector1 = A.Fake<IPoisonMessageDetector>();
        A.CallTo(() => detector1.IsPoisonMessageAsync(
                A<IDispatchMessage>._, A<IMessageContext>._, A<MessageProcessingInfo>._, A<Exception?>._))
            .Returns(Task.FromResult(PoisonDetectionResult.NotPoison()));

        var composite = new CompositePoisonDetector(
            new[] { detector1 },
            NullLogger<CompositePoisonDetector>.Instance);

        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        var info = CreateProcessingInfo();

        var result = await composite.IsPoisonMessageAsync(message, context, info);

        result.IsPoison.ShouldBeFalse();
    }

    [Fact]
    public async Task ContinueWhenDetectorThrows()
    {
        var detector1 = A.Fake<IPoisonMessageDetector>();
        A.CallTo(() => detector1.IsPoisonMessageAsync(
                A<IDispatchMessage>._, A<IMessageContext>._, A<MessageProcessingInfo>._, A<Exception?>._))
            .ThrowsAsync(new InvalidOperationException("Detector broke"));

        var detector2 = A.Fake<IPoisonMessageDetector>();
        A.CallTo(() => detector2.IsPoisonMessageAsync(
                A<IDispatchMessage>._, A<IMessageContext>._, A<MessageProcessingInfo>._, A<Exception?>._))
            .Returns(Task.FromResult(PoisonDetectionResult.Poison("Flagged", "Detector2")));

        var composite = new CompositePoisonDetector(
            new[] { detector1, detector2 },
            NullLogger<CompositePoisonDetector>.Instance);

        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        var info = CreateProcessingInfo();

        var result = await composite.IsPoisonMessageAsync(message, context, info);

        result.IsPoison.ShouldBeTrue();
    }

    [Fact]
    public async Task FilterOutSelfFromCompositeDetectors()
    {
        // CompositePoisonDetector filters out itself to avoid circular references
        var composite1 = new CompositePoisonDetector(
            Enumerable.Empty<IPoisonMessageDetector>(),
            NullLogger<CompositePoisonDetector>.Instance);

        var composite2 = new CompositePoisonDetector(
            new IPoisonMessageDetector[] { composite1 },
            NullLogger<CompositePoisonDetector>.Instance);

        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        var info = CreateProcessingInfo();

        var result = await composite2.IsPoisonMessageAsync(message, context, info);

        result.IsPoison.ShouldBeFalse();
    }
}
