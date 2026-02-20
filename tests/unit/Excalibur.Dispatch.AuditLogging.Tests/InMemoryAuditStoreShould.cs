using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.AuditLogging.Tests;

public class InMemoryAuditStoreShould
{
    private readonly InMemoryAuditStore _sut = new();

    private static AuditEvent CreateEvent(
        string eventId = "evt-1",
        AuditEventType eventType = AuditEventType.DataAccess,
        string action = "Read",
        string actorId = "user-1",
        string? tenantId = null,
        DateTimeOffset? timestamp = null) =>
        new()
        {
            EventId = eventId,
            EventType = eventType,
            Action = action,
            Outcome = AuditOutcome.Success,
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
            ActorId = actorId,
            TenantId = tenantId
        };

    [Fact]
    public async Task Store_event_and_return_event_id()
    {
        var auditEvent = CreateEvent();

        var result = await _sut.StoreAsync(auditEvent, CancellationToken.None);

        result.EventId.ShouldBe("evt-1");
        result.EventHash.ShouldNotBeNullOrWhiteSpace();
        result.SequenceNumber.ShouldBe(1);
    }

    [Fact]
    public async Task Assign_incrementing_sequence_numbers()
    {
        var result1 = await _sut.StoreAsync(CreateEvent("evt-1"), CancellationToken.None);
        var result2 = await _sut.StoreAsync(CreateEvent("evt-2"), CancellationToken.None);
        var result3 = await _sut.StoreAsync(CreateEvent("evt-3"), CancellationToken.None);

        result1.SequenceNumber.ShouldBe(1);
        result2.SequenceNumber.ShouldBe(2);
        result3.SequenceNumber.ShouldBe(3);
    }

    [Fact]
    public async Task Throw_for_duplicate_event_id()
    {
        await _sut.StoreAsync(CreateEvent("evt-dup"), CancellationToken.None);

        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.StoreAsync(CreateEvent("evt-dup"), CancellationToken.None));
    }

    [Fact]
    public async Task Get_by_id_returns_stored_event()
    {
        await _sut.StoreAsync(CreateEvent("evt-find"), CancellationToken.None);

        var result = await _sut.GetByIdAsync("evt-find", CancellationToken.None);

        result.ShouldNotBeNull();
        result.EventId.ShouldBe("evt-find");
        result.EventHash.ShouldNotBeNull();
    }

    [Fact]
    public async Task Get_by_id_returns_null_for_unknown_id()
    {
        var result = await _sut.GetByIdAsync("nonexistent", CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Throw_for_null_event_id_in_get_by_id()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.GetByIdAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Query_all_events()
    {
        await _sut.StoreAsync(CreateEvent("evt-q1"), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-q2"), CancellationToken.None);

        var results = await _sut.QueryAsync(new AuditQuery(), CancellationToken.None);

        results.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Query_by_event_type()
    {
        await _sut.StoreAsync(CreateEvent("evt-auth", eventType: AuditEventType.Authentication), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-data", eventType: AuditEventType.DataAccess), CancellationToken.None);

        var query = new AuditQuery { EventTypes = [AuditEventType.Authentication] };
        var results = await _sut.QueryAsync(query, CancellationToken.None);

        results.Count.ShouldBe(1);
        results[0].EventType.ShouldBe(AuditEventType.Authentication);
    }

    [Fact]
    public async Task Query_by_actor_id()
    {
        await _sut.StoreAsync(CreateEvent("evt-a1", actorId: "alice"), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-b1", actorId: "bob"), CancellationToken.None);

        var query = new AuditQuery { ActorId = "alice" };
        var results = await _sut.QueryAsync(query, CancellationToken.None);

        results.Count.ShouldBe(1);
        results[0].ActorId.ShouldBe("alice");
    }

    [Fact]
    public async Task Query_by_date_range()
    {
        var now = DateTimeOffset.UtcNow;
        await _sut.StoreAsync(CreateEvent("evt-old", timestamp: now.AddDays(-10)), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-new", timestamp: now), CancellationToken.None);

        var query = new AuditQuery
        {
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(1)
        };
        var results = await _sut.QueryAsync(query, CancellationToken.None);

        results.Count.ShouldBe(1);
        results[0].EventId.ShouldBe("evt-new");
    }

    [Fact]
    public async Task Query_by_tenant_id()
    {
        await _sut.StoreAsync(CreateEvent("evt-t1", tenantId: "tenant-a"), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-t2", tenantId: "tenant-b"), CancellationToken.None);

        var query = new AuditQuery { TenantId = "tenant-a" };
        var results = await _sut.QueryAsync(query, CancellationToken.None);

        results.Count.ShouldBe(1);
        results[0].TenantId.ShouldBe("tenant-a");
    }

    [Fact]
    public async Task Query_returns_empty_for_nonexistent_tenant()
    {
        var query = new AuditQuery { TenantId = "nonexistent" };
        var results = await _sut.QueryAsync(query, CancellationToken.None);

        results.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Query_supports_pagination()
    {
        for (var i = 0; i < 5; i++)
        {
            await _sut.StoreAsync(CreateEvent($"evt-page-{i}", timestamp: DateTimeOffset.UtcNow.AddMinutes(i)), CancellationToken.None);
        }

        var query = new AuditQuery { MaxResults = 2, Skip = 1, OrderByDescending = false };
        var results = await _sut.QueryAsync(query, CancellationToken.None);

        results.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Query_orders_by_descending_by_default()
    {
        var now = DateTimeOffset.UtcNow;
        await _sut.StoreAsync(CreateEvent("evt-old", timestamp: now.AddMinutes(-5)), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-new", timestamp: now), CancellationToken.None);

        var query = new AuditQuery { OrderByDescending = true };
        var results = await _sut.QueryAsync(query, CancellationToken.None);

        results[0].EventId.ShouldBe("evt-new");
    }

    [Fact]
    public async Task Count_returns_total_events()
    {
        await _sut.StoreAsync(CreateEvent("evt-c1"), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-c2"), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-c3"), CancellationToken.None);

        var count = await _sut.CountAsync(new AuditQuery(), CancellationToken.None);

        count.ShouldBe(3);
    }

    [Fact]
    public async Task Count_with_filter()
    {
        await _sut.StoreAsync(CreateEvent("evt-cf1", eventType: AuditEventType.Authentication), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-cf2", eventType: AuditEventType.DataAccess), CancellationToken.None);

        var count = await _sut.CountAsync(
            new AuditQuery { EventTypes = [AuditEventType.Authentication] },
            CancellationToken.None);

        count.ShouldBe(1);
    }

    [Fact]
    public async Task Count_returns_zero_for_nonexistent_tenant()
    {
        var count = await _sut.CountAsync(
            new AuditQuery { TenantId = "nonexistent" },
            CancellationToken.None);

        count.ShouldBe(0);
    }

    [Fact]
    public async Task Verify_chain_integrity_for_valid_chain()
    {
        var now = DateTimeOffset.UtcNow;
        await _sut.StoreAsync(CreateEvent("evt-v1", timestamp: now), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-v2", timestamp: now.AddSeconds(1)), CancellationToken.None);

        var result = await _sut.VerifyChainIntegrityAsync(
            now.AddMinutes(-1), now.AddMinutes(1), CancellationToken.None);

        result.IsValid.ShouldBeTrue();
        result.EventsVerified.ShouldBe(2);
    }

    [Fact]
    public async Task Verify_chain_integrity_returns_valid_for_empty_range()
    {
        var now = DateTimeOffset.UtcNow;

        var result = await _sut.VerifyChainIntegrityAsync(
            now.AddDays(-1), now.AddDays(1), CancellationToken.None);

        result.IsValid.ShouldBeTrue();
        result.EventsVerified.ShouldBe(0);
    }

    [Fact]
    public async Task Get_last_event_returns_most_recent_for_tenant()
    {
        await _sut.StoreAsync(CreateEvent("evt-first", tenantId: "t1"), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-last", tenantId: "t1"), CancellationToken.None);

        var result = await _sut.GetLastEventAsync("t1", CancellationToken.None);

        result.ShouldNotBeNull();
        result.EventId.ShouldBe("evt-last");
    }

    [Fact]
    public async Task Get_last_event_returns_null_for_nonexistent_tenant()
    {
        var result = await _sut.GetLastEventAsync("nonexistent", CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Get_last_event_uses_default_tenant_for_null()
    {
        await _sut.StoreAsync(CreateEvent("evt-def"), CancellationToken.None);

        var result = await _sut.GetLastEventAsync(null, CancellationToken.None);

        result.ShouldNotBeNull();
        result.EventId.ShouldBe("evt-def");
    }

    [Fact]
    public async Task Clear_removes_all_events()
    {
        await _sut.StoreAsync(CreateEvent("evt-clr1"), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-clr2"), CancellationToken.None);

        _sut.Clear();

        _sut.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Count_property_reflects_stored_events()
    {
        await _sut.StoreAsync(CreateEvent("evt-p1"), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-p2"), CancellationToken.None);

        _sut.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Throw_argument_null_for_null_event_in_store()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.StoreAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Throw_argument_null_for_null_query()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.QueryAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Throw_argument_null_for_null_count_query()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.CountAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Respect_cancellation_token_in_store()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.StoreAsync(CreateEvent(), cts.Token));
    }

    [Fact]
    public async Task Query_by_outcome()
    {
        await _sut.StoreAsync(new AuditEvent
        {
            EventId = "evt-s1",
            EventType = AuditEventType.Authentication,
            Action = "Login",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "user-1"
        }, CancellationToken.None);

        await _sut.StoreAsync(new AuditEvent
        {
            EventId = "evt-f1",
            EventType = AuditEventType.Authentication,
            Action = "Login",
            Outcome = AuditOutcome.Failure,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "user-2"
        }, CancellationToken.None);

        var query = new AuditQuery { Outcomes = [AuditOutcome.Failure] };
        var results = await _sut.QueryAsync(query, CancellationToken.None);

        results.Count.ShouldBe(1);
        results[0].Outcome.ShouldBe(AuditOutcome.Failure);
    }

    [Fact]
    public async Task Query_by_correlation_id()
    {
        await _sut.StoreAsync(new AuditEvent
        {
            EventId = "evt-corr1",
            EventType = AuditEventType.DataAccess,
            Action = "Read",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "user-1",
            CorrelationId = "corr-abc"
        }, CancellationToken.None);

        await _sut.StoreAsync(CreateEvent("evt-corr2"), CancellationToken.None);

        var query = new AuditQuery { CorrelationId = "corr-abc" };
        var results = await _sut.QueryAsync(query, CancellationToken.None);

        results.Count.ShouldBe(1);
        results[0].CorrelationId.ShouldBe("corr-abc");
    }

    [Fact]
    public async Task Query_by_action()
    {
        await _sut.StoreAsync(CreateEvent("evt-act1", action: "Create"), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-act2", action: "Delete"), CancellationToken.None);

        var query = new AuditQuery { Action = "Delete" };
        var results = await _sut.QueryAsync(query, CancellationToken.None);

        results.Count.ShouldBe(1);
        results[0].Action.ShouldBe("Delete");
    }

    [Fact]
    public async Task Store_computes_hash_chain()
    {
        var result1 = await _sut.StoreAsync(CreateEvent("evt-chain1"), CancellationToken.None);
        var result2 = await _sut.StoreAsync(CreateEvent("evt-chain2"), CancellationToken.None);

        var stored1 = await _sut.GetByIdAsync("evt-chain1", CancellationToken.None);
        var stored2 = await _sut.GetByIdAsync("evt-chain2", CancellationToken.None);

        stored1!.EventHash.ShouldNotBeNull();
        stored2!.EventHash.ShouldNotBeNull();
        stored2.PreviousEventHash.ShouldBe(stored1.EventHash);

        result1.EventHash.ShouldNotBe(result2.EventHash);
    }
}
