using Excalibur.Compliance;


using Excalibur.AuditLogging;namespace Excalibur.AuditLogging.Tests;

/// <summary>
/// Tests for multi-tenant isolation in InMemoryAuditStore.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class InMemoryAuditStoreMultiTenantShould : IDisposable
{
    private readonly InMemoryAuditStore _sut = new(AuditIntegrityTestStrategy.Create());
    public void Dispose() => _sut.Dispose();

    private static AuditEvent CreateEvent(string eventId, string? tenantId = null) =>
        new()
        {
            EventId = eventId,
            EventType = AuditEventType.DataAccess,
            Action = "Read",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "user-1",
            TenantId = tenantId
        };

    [Fact]
    public async Task Store_events_for_different_tenants_independently()
    {
        await _sut.StoreAsync(CreateEvent("evt-t1-1", "tenant-1"), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-t1-2", "tenant-1"), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-t2-1", "tenant-2"), CancellationToken.None);

        var t1Results = await _sut.QueryAsync(
            new AuditQuery { TenantId = "tenant-1" }, CancellationToken.None);
        var t2Results = await _sut.QueryAsync(
            new AuditQuery { TenantId = "tenant-2" }, CancellationToken.None);

        t1Results.Count.ShouldBe(2);
        t2Results.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Return_last_event_per_tenant()
    {
        await _sut.StoreAsync(CreateEvent("evt-t1-first", "tenant-1"), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-t1-last", "tenant-1"), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-t2-only", "tenant-2"), CancellationToken.None);

        var t1Last = await _sut.GetLastEventAsync("tenant-1", CancellationToken.None);
        var t2Last = await _sut.GetLastEventAsync("tenant-2", CancellationToken.None);

        t1Last.ShouldNotBeNull();
        t1Last.EventId.ShouldBe("evt-t1-last");
        t2Last.ShouldNotBeNull();
        t2Last.EventId.ShouldBe("evt-t2-only");
    }

    [Fact]
    public async Task Use_default_tenant_when_tenant_id_is_null()
    {
        await _sut.StoreAsync(CreateEvent("evt-def-1"), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-def-2"), CancellationToken.None);

        var last = await _sut.GetLastEventAsync(null, CancellationToken.None);

        last.ShouldNotBeNull();
        last.EventId.ShouldBe("evt-def-2");
    }

    [Fact]
    public async Task Maintain_separate_hash_chains_per_tenant()
    {
        await _sut.StoreAsync(CreateEvent("evt-t1-1", "tenant-1"), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-t2-1", "tenant-2"), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-t1-2", "tenant-1"), CancellationToken.None);

        var t1Evt1 = await _sut.GetByIdAsync("evt-t1-1", CancellationToken.None);
        var t1Evt2 = await _sut.GetByIdAsync("evt-t1-2", CancellationToken.None);
        var t2Evt1 = await _sut.GetByIdAsync("evt-t2-1", CancellationToken.None);

        // Tenant 1's second event should chain to tenant 1's first event
        t1Evt2!.PreviousEventHash.ShouldBe(t1Evt1!.EventHash);

        // Tenant 2's first event is the genesis of its OWN chain → null prior tag (qa71t5: keyed-MAC
        // chains start at a null prior; the tenant is bound inside the canonical content + MAC, so tenant-2
        // never chains onto tenant-1's events).
        t2Evt1!.PreviousEventHash.ShouldBeNull();
    }

    [Fact]
    public async Task Clear_removes_all_tenant_data()
    {
        await _sut.StoreAsync(CreateEvent("evt-t1", "tenant-1"), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-t2", "tenant-2"), CancellationToken.None);

        _sut.Clear();

        _sut.Count.ShouldBe(0);
        var t1Last = await _sut.GetLastEventAsync("tenant-1", CancellationToken.None);
        var t2Last = await _sut.GetLastEventAsync("tenant-2", CancellationToken.None);
        t1Last.ShouldBeNull();
        t2Last.ShouldBeNull();
    }

    [Fact]
    public async Task Total_count_includes_all_tenants()
    {
        await _sut.StoreAsync(CreateEvent("evt-t1", "tenant-1"), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-t2", "tenant-2"), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-def"), CancellationToken.None);

        _sut.Count.ShouldBe(3);
    }
}
