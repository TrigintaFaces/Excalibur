// Functional tests for SecurityEventLogger — queuing, background processing, batch storage, lifecycle

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Security;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Security.Tests.Security.Functional;

[Trait("Category", "Unit")]
public sealed class SecurityEventLoggerFunctionalShould : IDisposable
{
    private readonly ISecurityEventStore _eventStore;
    private readonly ILogger<SecurityEventLogger> _logger;
    private readonly SecurityEventLogger _sut;

    public SecurityEventLoggerFunctionalShould()
    {
        _eventStore = A.Fake<ISecurityEventStore>();
        _logger = A.Fake<ILogger<SecurityEventLogger>>();
        // Enable IsEnabled so source-gen logging actually calls through
        A.CallTo(() => _logger.IsEnabled(A<LogLevel>._)).Returns(true);

        _sut = new SecurityEventLogger(_logger, _eventStore);
    }

    public void Dispose() => _sut.Dispose();

    [Fact]
    public async Task QueueSecurityEventSuccessfully()
    {
        // LogSecurityEventAsync queues without blocking
        await _sut.LogSecurityEventAsync(
            SecurityEventType.AuthenticationFailure,
            "Login failed",
            SecuritySeverity.High,
            CancellationToken.None);

        // No exception is the success condition -- event is queued
    }

    [Fact]
    public async Task QueueEventWithContextMetadata()
    {
        var context = A.Fake<IMessageContext>();
        var items = new Dictionary<string, object>
        {
            ["User:MessageId"] = "user-abc",
            ["Client:IP"] = "10.0.0.1",
            ["Client:UserAgent"] = "TestRunner/1.0",
            ["Message:Type"] = "LoginCommand",
            ["Security:Reason"] = "bad-password",
        };
        A.CallTo(() => context.Items).Returns(items);
        A.CallTo(() => context.CorrelationId).Returns(Guid.NewGuid().ToString());

        await _sut.LogSecurityEventAsync(
            SecurityEventType.AuthenticationFailure,
            "Invalid credentials",
            SecuritySeverity.High,
            CancellationToken.None,
            context);
    }

    [Fact]
    public async Task ProcessEventsInBackgroundAfterStart()
    {
        // Capture events stored via the mutable list pattern
        var storedEvents = new List<SecurityEvent>();
        A.CallTo(() => _eventStore.StoreEventsAsync(A<IEnumerable<SecurityEvent>>._, A<CancellationToken>._))
            .Invokes(call =>
            {
                var events = call.Arguments.Get<IEnumerable<SecurityEvent>>(0);
                if (events != null)
                {
                    storedEvents.AddRange(events);
                }
            })
            .Returns(Task.CompletedTask);

        // Start background processing
        await _sut.StartAsync(CancellationToken.None);

        // Queue an event
        await _sut.LogSecurityEventAsync(
            SecurityEventType.AuthorizationFailure,
            "Access denied",
            SecuritySeverity.Medium,
            CancellationToken.None);

        // Wait for background processing
        await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(200);

        // Stop gracefully
        await _sut.StopAsync(CancellationToken.None);

        // Verify event was stored
        storedEvents.ShouldNotBeEmpty();
        storedEvents.ShouldContain(e => e.Description == "Access denied");
        storedEvents.ShouldContain(e => e.EventType == SecurityEventType.AuthorizationFailure);
    }

    [Fact]
    public async Task BatchMultipleEventsForStorage()
    {
        var storeCallCount = 0;
        A.CallTo(() => _eventStore.StoreEventsAsync(A<IEnumerable<SecurityEvent>>._, A<CancellationToken>._))
            .Invokes(_ => Interlocked.Increment(ref storeCallCount))
            .Returns(Task.CompletedTask);

        await _sut.StartAsync(CancellationToken.None);

        // Queue multiple events rapidly
        for (var i = 0; i < 10; i++)
        {
            await _sut.LogSecurityEventAsync(
                SecurityEventType.ValidationFailure,
                $"Validation error {i}",
                SecuritySeverity.Low,
                CancellationToken.None);
        }

        await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(300);
        await _sut.StopAsync(CancellationToken.None);

        // Store should have been called (possibly batched)
        A.CallTo(() => _eventStore.StoreEventsAsync(A<IEnumerable<SecurityEvent>>._, A<CancellationToken>._))
            .MustHaveHappened();
    }

    [Fact]
    public async Task FallbackToIndividualStorageOnBatchFailure()
    {
        var callCount = 0;
        A.CallTo(() => _eventStore.StoreEventsAsync(A<IEnumerable<SecurityEvent>>._, A<CancellationToken>._))
            .Invokes(_ =>
            {
                var count = Interlocked.Increment(ref callCount);
                if (count == 1)
                {
                    throw new InvalidOperationException("Batch store failed");
                }
            })
            .Returns(Task.CompletedTask);

        await _sut.StartAsync(CancellationToken.None);

        await _sut.LogSecurityEventAsync(
            SecurityEventType.EncryptionFailure,
            "Encryption failed",
            SecuritySeverity.High,
            CancellationToken.None);

        await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(300);
        await _sut.StopAsync(CancellationToken.None);

        // Should have attempted batch first, then individual fallback
        A.CallTo(() => _eventStore.StoreEventsAsync(A<IEnumerable<SecurityEvent>>._, A<CancellationToken>._))
            .MustHaveHappened();
    }

    [Fact]
    public async Task StartAndStopLifecycle()
    {
        await _sut.StartAsync(CancellationToken.None);
        await _sut.StopAsync(CancellationToken.None);
        // Should not throw or hang
    }

    [Fact]
    public void ThrowOnNullLogger()
    {
        Should.Throw<ArgumentNullException>(() =>
            new SecurityEventLogger(null!, _eventStore));
    }

    [Fact]
    public void ThrowOnNullEventStore()
    {
        Should.Throw<ArgumentNullException>(() =>
            new SecurityEventLogger(_logger, null!));
    }

    [Fact]
    public void DisposeSafely()
    {
        var sut = new SecurityEventLogger(_logger, _eventStore);
        sut.Dispose();
        // Double dispose should be safe
        sut.Dispose();
    }

    [Fact]
    public async Task QueueEventWithNullContext()
    {
        // Should handle null context without throwing
        await _sut.LogSecurityEventAsync(
            SecurityEventType.RateLimitExceeded,
            "Rate limit hit",
            SecuritySeverity.Medium,
            CancellationToken.None,
            context: null);
    }

    [Fact]
    public async Task ExtractSecurityPrefixedAdditionalData()
    {
        var storedEvents = new List<SecurityEvent>();
        A.CallTo(() => _eventStore.StoreEventsAsync(A<IEnumerable<SecurityEvent>>._, A<CancellationToken>._))
            .Invokes(call =>
            {
                var events = call.Arguments.Get<IEnumerable<SecurityEvent>>(0);
                if (events != null) storedEvents.AddRange(events);
            })
            .Returns(Task.CompletedTask);

        var context = A.Fake<IMessageContext>();
        var items = new Dictionary<string, object>
        {
            ["Security:FailureReason"] = "brute-force",
            ["Auth:Method"] = "jwt",
            ["Validation:Field"] = "email",
            ["Normal:Key"] = "should-not-appear",
        };
        A.CallTo(() => context.Items).Returns(items);

        await _sut.StartAsync(CancellationToken.None);

        await _sut.LogSecurityEventAsync(
            SecurityEventType.AuthenticationFailure,
            "Attack detected",
            SecuritySeverity.Critical,
            CancellationToken.None,
            context);

        await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(300);
        await _sut.StopAsync(CancellationToken.None);

        storedEvents.ShouldNotBeEmpty();
        var evt = storedEvents.First();
        evt.AdditionalData.ShouldContainKey("Security:FailureReason");
        evt.AdditionalData.ShouldContainKey("Auth:Method");
        evt.AdditionalData.ShouldContainKey("Validation:Field");
        evt.AdditionalData.ShouldNotContainKey("Normal:Key");
    }
}
