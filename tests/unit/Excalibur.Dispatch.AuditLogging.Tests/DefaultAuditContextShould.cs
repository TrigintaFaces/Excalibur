// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Dispatch.AuditLogging.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class DefaultAuditContextShould
{
    private readonly IAuditLogger _fakeAuditLogger;
    private readonly FakeTimeProvider _timeProvider;
    private readonly IOptions<AuditContextOptions> _defaultOptions;
    private readonly ILogger<DefaultAuditContext> _logger;
    private readonly DefaultAuditContext _sut;

    public DefaultAuditContextShould()
    {
        _fakeAuditLogger = A.Fake<IAuditLogger>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 4, 11, 12, 0, 0, TimeSpan.Zero));
        _defaultOptions = Microsoft.Extensions.Options.Options.Create(new AuditContextOptions());
        _logger = NullLogger<DefaultAuditContext>.Instance;

        A.CallTo(() => _fakeAuditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
            .Returns(CreateAuditEventId("evt-1"));

        _sut = new DefaultAuditContext(_fakeAuditLogger, _timeProvider, _defaultOptions, _logger);
        _sut.Initialize("corr-1", "actor-1", "tenant-1", "PlaceOrderCommand");
    }

    // ========================================
    // Constructor validation
    // ========================================

    [Fact]
    public void Throw_when_audit_logger_is_null()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DefaultAuditContext(null!, _timeProvider, _defaultOptions, _logger));
    }

    [Fact]
    public void Throw_when_time_provider_is_null()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DefaultAuditContext(_fakeAuditLogger, null!, _defaultOptions, _logger));
    }

    [Fact]
    public void Throw_when_options_is_null()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DefaultAuditContext(_fakeAuditLogger, _timeProvider, null!, _logger));
    }

    [Fact]
    public void Throw_when_logger_is_null()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DefaultAuditContext(_fakeAuditLogger, _timeProvider, _defaultOptions, null!));
    }

    // ========================================
    // AssertAsync: false condition = no-op
    // ========================================

    [Fact]
    public async Task Assert_async_returns_null_when_condition_is_false()
    {
        var result = await _sut.AssertAsync(false, "never recorded", AuditEventType.Compliance, CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Assert_async_does_not_call_logger_when_condition_is_false()
    {
        await _sut.AssertAsync(false, "never recorded", AuditEventType.Compliance, CancellationToken.None);

        A.CallTo(() => _fakeAuditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    // ========================================
    // AssertAsync: true condition = records
    // ========================================

    [Fact]
    public async Task Assert_async_records_event_when_condition_is_true()
    {
        var result = await _sut.AssertAsync(true, "Threshold exceeded", AuditEventType.Compliance, CancellationToken.None);

        result.ShouldNotBeNull();
        result.Value.EventId.ShouldBe("evt-1");
    }

    [Fact]
    public async Task Assert_async_delegates_to_audit_logger()
    {
        await _sut.AssertAsync(true, "Test assertion", AuditEventType.Compliance, CancellationToken.None);

        A.CallTo(() => _fakeAuditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Assert_async_builds_event_with_correct_fields()
    {
        AuditEvent? captured = null;
        A.CallTo(() => _fakeAuditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
            .Invokes((AuditEvent e, CancellationToken _) => captured = e)
            .Returns(CreateAuditEventId("evt-1"));

        await _sut.AssertAsync(true, "Order total exceeds threshold", AuditEventType.Compliance, CancellationToken.None);

        captured.ShouldNotBeNull();
        captured.EventType.ShouldBe(AuditEventType.Compliance);
        captured.Action.ShouldBe("AuditContext.Assert");
        captured.Outcome.ShouldBe(AuditOutcome.Success);
        captured.Reason.ShouldBe("Order total exceeds threshold");
        captured.ActorId.ShouldBe("actor-1");
        captured.CorrelationId.ShouldBe("corr-1");
        captured.TenantId.ShouldBe("tenant-1");
        captured.Timestamp.ShouldBe(_timeProvider.GetUtcNow());
    }

    [Fact]
    public async Task Assert_async_includes_message_type_in_metadata_by_default()
    {
        AuditEvent? captured = null;
        A.CallTo(() => _fakeAuditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
            .Invokes((AuditEvent e, CancellationToken _) => captured = e)
            .Returns(CreateAuditEventId("evt-1"));

        await _sut.AssertAsync(true, "test", AuditEventType.Compliance, CancellationToken.None);

        captured.ShouldNotBeNull();
        captured.Metadata.ShouldNotBeNull();
        captured.Metadata.ShouldContainKeyAndValue("MessageType", "PlaceOrderCommand");
    }

    [Fact]
    public async Task Assert_async_excludes_message_type_when_option_disabled()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AuditContextOptions { IncludeMessageTypeName = false });
        var sut = new DefaultAuditContext(_fakeAuditLogger, _timeProvider, options, _logger);
        sut.Initialize("corr-1", "actor-1", null, "SomeCommand");

        AuditEvent? captured = null;
        A.CallTo(() => _fakeAuditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
            .Invokes((AuditEvent e, CancellationToken _) => captured = e)
            .Returns(CreateAuditEventId("evt-1"));

        await sut.AssertAsync(true, "test", AuditEventType.Compliance, CancellationToken.None);

        captured.ShouldNotBeNull();
        if (captured.Metadata is not null)
        {
            captured.Metadata.ShouldNotContainKey("MessageType");
        }
    }

    [Fact]
    public async Task Assert_async_returns_null_when_logger_throws()
    {
        A.CallTo(() => _fakeAuditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
            .Throws(new InvalidOperationException("Store unavailable"));

        var result = await _sut.AssertAsync(true, "test", AuditEventType.Compliance, CancellationToken.None);

        // Per spec: log+drop, never throw
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Assert_async_rethrows_operation_canceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        A.CallTo(() => _fakeAuditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
            .Throws(new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.AssertAsync(true, "test", AuditEventType.Compliance, cts.Token));
    }

    // ========================================
    // ObserveAsync: unconditional recording
    // ========================================

    [Fact]
    public async Task Observe_async_unconditionally_records_event()
    {
        var result = await _sut.ObserveAsync("Order placed", AuditEventType.DataModification, AuditOutcome.Success, CancellationToken.None);

        result.EventId.ShouldBe("evt-1");
        A.CallTo(() => _fakeAuditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Observe_async_builds_event_with_correct_outcome()
    {
        AuditEvent? captured = null;
        A.CallTo(() => _fakeAuditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
            .Invokes((AuditEvent e, CancellationToken _) => captured = e)
            .Returns(CreateAuditEventId("evt-1"));

        await _sut.ObserveAsync("Failed operation", AuditEventType.DataModification, AuditOutcome.Failure, CancellationToken.None);

        captured.ShouldNotBeNull();
        captured.Action.ShouldBe("AuditContext.Observe");
        captured.Outcome.ShouldBe(AuditOutcome.Failure);
        captured.Reason.ShouldBe("Failed operation");
    }

    [Fact]
    public async Task Observe_async_returns_sentinel_when_logger_throws()
    {
        A.CallTo(() => _fakeAuditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
            .Throws(new InvalidOperationException("Store unavailable"));

        var result = await _sut.ObserveAsync("test", AuditEventType.Compliance, AuditOutcome.Success, CancellationToken.None);

        // Sentinel: empty EventId, SequenceNumber = -1
        result.EventId.ShouldBe(string.Empty);
        result.SequenceNumber.ShouldBe(-1);
    }

    // ========================================
    // MaxAssertionsPerScope enforcement
    // ========================================

    [Fact]
    public async Task Assert_async_drops_assertions_after_max_exceeded()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AuditContextOptions { MaxAssertionsPerScope = 2 });
        var sut = new DefaultAuditContext(_fakeAuditLogger, _timeProvider, options, _logger);
        sut.Initialize("corr-1", "actor-1", null, null);

        // First 2 should succeed
        var r1 = await sut.AssertAsync(true, "First", AuditEventType.Compliance, CancellationToken.None);
        var r2 = await sut.AssertAsync(true, "Second", AuditEventType.Compliance, CancellationToken.None);
        // Third should be dropped
        var r3 = await sut.AssertAsync(true, "Dropped", AuditEventType.Compliance, CancellationToken.None);

        r1.ShouldNotBeNull();
        r2.ShouldNotBeNull();
        r3.ShouldBeNull(); // Dropped, not thrown

        A.CallTo(() => _fakeAuditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
            .MustHaveHappened(2, Times.Exactly);
    }

    [Fact]
    public async Task Observe_async_returns_sentinel_after_max_exceeded()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AuditContextOptions { MaxAssertionsPerScope = 1 });
        var sut = new DefaultAuditContext(_fakeAuditLogger, _timeProvider, options, _logger);
        sut.Initialize("corr-1", "actor-1", null, null);

        await sut.ObserveAsync("First", AuditEventType.Compliance, AuditOutcome.Success, CancellationToken.None);
        var result = await sut.ObserveAsync("Dropped", AuditEventType.Compliance, AuditOutcome.Success, CancellationToken.None);

        result.EventId.ShouldBe(string.Empty);
        result.SequenceNumber.ShouldBe(-1);
    }

    [Fact]
    public async Task Max_assertions_counts_both_assert_and_observe()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AuditContextOptions { MaxAssertionsPerScope = 2 });
        var sut = new DefaultAuditContext(_fakeAuditLogger, _timeProvider, options, _logger);
        sut.Initialize("corr-1", "actor-1", null, null);

        await sut.AssertAsync(true, "First", AuditEventType.Compliance, CancellationToken.None);
        await sut.ObserveAsync("Second", AuditEventType.Compliance, AuditOutcome.Success, CancellationToken.None);
        var r3 = await sut.AssertAsync(true, "Dropped", AuditEventType.Compliance, CancellationToken.None);

        r3.ShouldBeNull();
        A.CallTo(() => _fakeAuditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
            .MustHaveHappened(2, Times.Exactly);
    }

    // ========================================
    // WithMetadata
    // ========================================

    [Fact]
    public async Task With_metadata_adds_to_event_metadata()
    {
        AuditEvent? captured = null;
        A.CallTo(() => _fakeAuditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
            .Invokes((AuditEvent e, CancellationToken _) => captured = e)
            .Returns(CreateAuditEventId("evt-1"));

        _sut.WithMetadata("OrderId", "ORD-123");
        await _sut.AssertAsync(true, "test", AuditEventType.Compliance, CancellationToken.None);

        captured.ShouldNotBeNull();
        captured.Metadata.ShouldNotBeNull();
        captured.Metadata.ShouldContainKeyAndValue("OrderId", "ORD-123");
    }

    [Fact]
    public void With_metadata_returns_same_context_for_fluent_chaining()
    {
        var result = _sut.WithMetadata("key", "value");
        result.ShouldBeSameAs(_sut);
    }

    [Fact]
    public void With_metadata_throws_when_key_is_null()
    {
        Should.Throw<ArgumentException>(() => _sut.WithMetadata(null!, "value"));
    }

    [Fact]
    public void With_metadata_throws_when_value_is_null()
    {
        Should.Throw<ArgumentNullException>(() => _sut.WithMetadata("key", null!));
    }

    [Fact]
    public async Task With_metadata_overwrites_existing_key()
    {
        AuditEvent? captured = null;
        A.CallTo(() => _fakeAuditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
            .Invokes((AuditEvent e, CancellationToken _) => captured = e)
            .Returns(CreateAuditEventId("evt-1"));

        _sut.WithMetadata("key", "first");
        _sut.WithMetadata("key", "second");
        await _sut.AssertAsync(true, "test", AuditEventType.Compliance, CancellationToken.None);

        captured.ShouldNotBeNull();
        captured.Metadata.ShouldNotBeNull();
        captured.Metadata.ShouldContainKeyAndValue("key", "second");
    }

    // ========================================
    // ForResource
    // ========================================

    [Fact]
    public async Task For_resource_sets_resource_on_event()
    {
        AuditEvent? captured = null;
        A.CallTo(() => _fakeAuditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
            .Invokes((AuditEvent e, CancellationToken _) => captured = e)
            .Returns(CreateAuditEventId("evt-1"));

        _sut.ForResource("ORD-123", "Order");
        await _sut.AssertAsync(true, "test", AuditEventType.Compliance, CancellationToken.None);

        captured.ShouldNotBeNull();
        captured.ResourceId.ShouldBe("ORD-123");
        captured.ResourceType.ShouldBe("Order");
    }

    [Fact]
    public void For_resource_returns_same_context_for_fluent_chaining()
    {
        var result = _sut.ForResource("id", "type");
        result.ShouldBeSameAs(_sut);
    }

    [Fact]
    public void For_resource_throws_when_resource_id_is_null()
    {
        Should.Throw<ArgumentException>(() => _sut.ForResource(null!, "type"));
    }

    [Fact]
    public void For_resource_throws_when_resource_type_is_null()
    {
        Should.Throw<ArgumentException>(() => _sut.ForResource("id", null!));
    }

    // ========================================
    // Scoped isolation: Initialize resets state
    // ========================================

    [Fact]
    public async Task Initialize_resets_assertion_count()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AuditContextOptions { MaxAssertionsPerScope = 1 });
        var sut = new DefaultAuditContext(_fakeAuditLogger, _timeProvider, options, _logger);
        sut.Initialize("corr-1", "actor-1", null, null);

        await sut.AssertAsync(true, "First scope", AuditEventType.Compliance, CancellationToken.None);
        // Max reached
        var dropped = await sut.AssertAsync(true, "Dropped", AuditEventType.Compliance, CancellationToken.None);
        dropped.ShouldBeNull();

        // Re-initialize (simulates new handler scope)
        sut.Initialize("corr-2", "actor-2", null, null);
        var afterReset = await sut.AssertAsync(true, "After reset", AuditEventType.Compliance, CancellationToken.None);
        afterReset.ShouldNotBeNull();
    }

    [Fact]
    public async Task Initialize_resets_metadata_and_resource()
    {
        AuditEvent? captured = null;
        A.CallTo(() => _fakeAuditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
            .Invokes((AuditEvent e, CancellationToken _) => captured = e)
            .Returns(CreateAuditEventId("evt-1"));

        _sut.WithMetadata("key", "value");
        _sut.ForResource("res-1", "Order");

        // Re-initialize clears state
        _sut.Initialize("corr-2", "actor-2", null, null);
        await _sut.AssertAsync(true, "test", AuditEventType.Compliance, CancellationToken.None);

        captured.ShouldNotBeNull();
        captured.ResourceId.ShouldBeNull();
        captured.ResourceType.ShouldBeNull();
        captured.ActorId.ShouldBe("actor-2");
        captured.CorrelationId.ShouldBe("corr-2");
    }

    // ========================================
    // Default actor when not initialized
    // ========================================

    [Fact]
    public async Task Uses_unknown_actor_when_not_initialized()
    {
        AuditEvent? captured = null;
        A.CallTo(() => _fakeAuditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
            .Invokes((AuditEvent e, CancellationToken _) => captured = e)
            .Returns(CreateAuditEventId("evt-1"));

        var sut = new DefaultAuditContext(_fakeAuditLogger, _timeProvider, _defaultOptions, _logger);
        // Not calling Initialize
        await sut.AssertAsync(true, "test", AuditEventType.Compliance, CancellationToken.None);

        captured.ShouldNotBeNull();
        captured.ActorId.ShouldBe("unknown");
    }

    // ========================================
    // Helpers
    // ========================================

    private static AuditEventId CreateAuditEventId(string eventId) => new()
    {
        EventId = eventId,
        EventHash = $"hash-{eventId}",
        SequenceNumber = 1,
        RecordedAt = DateTimeOffset.UtcNow
    };
}
