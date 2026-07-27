using Excalibur.Compliance;

using Excalibur.Dispatch;

using Excalibur.AuditLogging;

namespace Excalibur.AuditLogging.Tests;

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
        // The partition is resolved from the AMBIENT scope, never from the query. The previous version set
        // AuditQuery.TenantId and asserted the result reflected it, which is the caller-supplied-tenant
        // shape of the impersonation defect: had the store been "fixed" to satisfy it, any caller could
        // name another tenant and read that tenant's audit events. The store never reads that field, so
        // the assertion below moves to the ambient context and the counts are unchanged.
        var ambient = new SwitchableTenantContext("tenant-1");
        using var sut = new InMemoryAuditStore(AuditIntegrityTestStrategy.Create(), ambient);

        await sut.StoreAsync(CreateEvent("evt-t1-1", "tenant-1"), CancellationToken.None);
        await sut.StoreAsync(CreateEvent("evt-t1-2", "tenant-1"), CancellationToken.None);
        await sut.StoreAsync(CreateEvent("evt-t2-1", "tenant-2"), CancellationToken.None);

        // No TenantId on either query -- a caller cannot name a partition, and these must not start.
        var t1Results = await sut.QueryAsync(new AuditQuery(), CancellationToken.None);

        ambient.TenantId = "tenant-2";
        var t2Results = await sut.QueryAsync(new AuditQuery(), CancellationToken.None);

        t1Results.Count.ShouldBe(2);
        t2Results.Count.ShouldBe(1);

        // The counts alone would also be satisfied by a store that sliced the data some other way. Bind
        // the identities so "two events" has to mean "tenant-1's two events".
        t1Results.ShouldAllBe(e => e.TenantId == "tenant-1");
        t2Results.ShouldAllBe(e => e.TenantId == "tenant-2");
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
        // Reads happen under the ambient tenant that owns the event, because GetByIdAsync is a
        // tenant-scoped read. The previous version of this test used it as cross-tenant introspection --
        // one ambient-less caller fetching all three events regardless of owner -- which only worked
        // because the store leaked. That made the test a certificate for the vulnerability: scoping the
        // store correctly turned this assertion red, so the test would have argued against its own fix.
        // The chain assertions below are UNCHANGED; only the mechanism for reaching the events moved.
        var ambient = new SwitchableTenantContext("tenant-1");
        using var sut = new InMemoryAuditStore(AuditIntegrityTestStrategy.Create(), ambient);

        await sut.StoreAsync(CreateEvent("evt-t1-1", "tenant-1"), CancellationToken.None);
        await sut.StoreAsync(CreateEvent("evt-t2-1", "tenant-2"), CancellationToken.None);
        await sut.StoreAsync(CreateEvent("evt-t1-2", "tenant-1"), CancellationToken.None);

        ambient.TenantId = "tenant-1";
        var t1Evt1 = await sut.GetByIdAsync("evt-t1-1", CancellationToken.None);
        var t1Evt2 = await sut.GetByIdAsync("evt-t1-2", CancellationToken.None);

        ambient.TenantId = "tenant-2";
        var t2Evt1 = await sut.GetByIdAsync("evt-t2-1", CancellationToken.None);

        // Tenant 1's second event should chain to tenant 1's first event
        t1Evt2!.PreviousEventHash.ShouldBe(t1Evt1!.EventHash);

        // Tenant 2's first event is the genesis of its OWN chain → null prior tag (keyed-MAC chains start
        // at a null prior; the tenant is bound inside the canonical content + MAC, so tenant-2 never
        // chains onto tenant-1's events).
        t2Evt1!.PreviousEventHash.ShouldBeNull();

        // STRENGTHENED, not relaxed: separate chains are only meaningful if the partition is also
        // enforced on the way out. Under tenant-2's ambient scope, tenant-1's event must be unreachable
        // by its own identifier -- the property whose absence made the old mechanism work at all.
        var leaked = await sut.GetByIdAsync("evt-t1-1", CancellationToken.None);
        leaked.ShouldBeNull(
            "tenant-2 must not read tenant-1's audit event by identifier; separate hash chains are not "
            + "isolation on their own if the read path returns the other tenant's row");
    }

    /// <summary>
    /// An ambient tenant scope the test can move between reads, standing in for the per-request context a
    /// host would supply.
    /// </summary>
    /// <remarks>
    /// It takes its first tenant in the constructor and there is no null state to forget. A sibling
    /// implementation in the conformance package yields a null tenant until an explicit switch is called,
    /// which fails closed on the first read if that call is ever missed -- the ambient ruling makes a
    /// present-but-unresolved context throw. Requiring the tenant up front makes that mistake
    /// inexpressible here rather than merely absent today.
    /// </remarks>
    private sealed class SwitchableTenantContext(string initialTenantId) : ITenantContext
    {
        private string _tenantId = !string.IsNullOrWhiteSpace(initialTenantId)
            ? initialTenantId
            : throw new ArgumentException("An ambient tenant is required.", nameof(initialTenantId));

        public string TenantId
        {
            get => _tenantId;
            set => _tenantId = !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new ArgumentException("An ambient tenant is required.", nameof(value));
        }

        string? ITenantContext.TenantId => _tenantId;

        public bool HasTenant => true;
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
