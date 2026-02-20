using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.AuditLogging.Tests;

/// <summary>
/// Tests for the InMemoryAuditStore query filter logic (resource, classification, ip).
/// </summary>
public class InMemoryAuditStoreFilterShould
{
    private readonly InMemoryAuditStore _sut = new();

    private static AuditEvent CreateEvent(
        string eventId,
        string? resourceId = null,
        string? resourceType = null,
        DataClassification? classification = null,
        string? ipAddress = null) =>
        new()
        {
            EventId = eventId,
            EventType = AuditEventType.DataAccess,
            Action = "Read",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "user-1",
            ResourceId = resourceId,
            ResourceType = resourceType,
            ResourceClassification = classification,
            IpAddress = ipAddress
        };

    [Fact]
    public async Task Filter_by_resource_id()
    {
        await _sut.StoreAsync(CreateEvent("evt-1", resourceId: "res-abc"), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-2", resourceId: "res-xyz"), CancellationToken.None);

        var query = new AuditQuery { ResourceId = "res-abc" };
        var results = await _sut.QueryAsync(query, CancellationToken.None);

        results.Count.ShouldBe(1);
        results[0].ResourceId.ShouldBe("res-abc");
    }

    [Fact]
    public async Task Filter_by_resource_type()
    {
        await _sut.StoreAsync(CreateEvent("evt-1", resourceType: "Customer"), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-2", resourceType: "Order"), CancellationToken.None);

        var query = new AuditQuery { ResourceType = "Customer" };
        var results = await _sut.QueryAsync(query, CancellationToken.None);

        results.Count.ShouldBe(1);
        results[0].ResourceType.ShouldBe("Customer");
    }

    [Fact]
    public async Task Filter_by_minimum_classification()
    {
        await _sut.StoreAsync(CreateEvent("evt-pub", classification: DataClassification.Public), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-int", classification: DataClassification.Internal), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-conf", classification: DataClassification.Confidential), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-rest", classification: DataClassification.Restricted), CancellationToken.None);

        var query = new AuditQuery { MinimumClassification = DataClassification.Confidential };
        var results = await _sut.QueryAsync(query, CancellationToken.None);

        results.Count.ShouldBe(2);
        results.ShouldAllBe(e => e.ResourceClassification >= DataClassification.Confidential);
    }

    [Fact]
    public async Task Filter_by_ip_address()
    {
        await _sut.StoreAsync(CreateEvent("evt-1", ipAddress: "192.168.1.1"), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-2", ipAddress: "10.0.0.1"), CancellationToken.None);

        var query = new AuditQuery { IpAddress = "10.0.0.1" };
        var results = await _sut.QueryAsync(query, CancellationToken.None);

        results.Count.ShouldBe(1);
        results[0].IpAddress.ShouldBe("10.0.0.1");
    }

    [Fact]
    public async Task Combine_multiple_filters()
    {
        await _sut.StoreAsync(new AuditEvent
        {
            EventId = "evt-match",
            EventType = AuditEventType.DataAccess,
            Action = "Read",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "alice",
            ResourceType = "Customer",
            IpAddress = "10.0.0.1"
        }, CancellationToken.None);

        await _sut.StoreAsync(new AuditEvent
        {
            EventId = "evt-miss",
            EventType = AuditEventType.DataAccess,
            Action = "Read",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "bob",
            ResourceType = "Customer",
            IpAddress = "10.0.0.1"
        }, CancellationToken.None);

        var query = new AuditQuery
        {
            ActorId = "alice",
            ResourceType = "Customer",
            IpAddress = "10.0.0.1"
        };
        var results = await _sut.QueryAsync(query, CancellationToken.None);

        results.Count.ShouldBe(1);
        results[0].EventId.ShouldBe("evt-match");
    }

    [Fact]
    public async Task Count_with_resource_filter()
    {
        await _sut.StoreAsync(CreateEvent("evt-1", resourceType: "Customer"), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-2", resourceType: "Order"), CancellationToken.None);
        await _sut.StoreAsync(CreateEvent("evt-3", resourceType: "Customer"), CancellationToken.None);

        var count = await _sut.CountAsync(
            new AuditQuery { ResourceType = "Customer" },
            CancellationToken.None);

        count.ShouldBe(2);
    }

    [Fact]
    public async Task Count_by_tenant_with_filters()
    {
        await _sut.StoreAsync(new AuditEvent
        {
            EventId = "evt-t1-a",
            EventType = AuditEventType.Authentication,
            Action = "Login",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "user-1",
            TenantId = "tenant-1"
        }, CancellationToken.None);

        await _sut.StoreAsync(new AuditEvent
        {
            EventId = "evt-t1-b",
            EventType = AuditEventType.DataAccess,
            Action = "Read",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "user-1",
            TenantId = "tenant-1"
        }, CancellationToken.None);

        var count = await _sut.CountAsync(
            new AuditQuery
            {
                TenantId = "tenant-1",
                EventTypes = [AuditEventType.Authentication]
            },
            CancellationToken.None);

        count.ShouldBe(1);
    }

    [Fact]
    public async Task Query_with_ascending_order()
    {
        var now = DateTimeOffset.UtcNow;
        await _sut.StoreAsync(new AuditEvent
        {
            EventId = "evt-first",
            EventType = AuditEventType.DataAccess,
            Action = "Read",
            Outcome = AuditOutcome.Success,
            Timestamp = now.AddMinutes(-5),
            ActorId = "user-1"
        }, CancellationToken.None);

        await _sut.StoreAsync(new AuditEvent
        {
            EventId = "evt-second",
            EventType = AuditEventType.DataAccess,
            Action = "Read",
            Outcome = AuditOutcome.Success,
            Timestamp = now,
            ActorId = "user-1"
        }, CancellationToken.None);

        var results = await _sut.QueryAsync(
            new AuditQuery { OrderByDescending = false },
            CancellationToken.None);

        results[0].EventId.ShouldBe("evt-first");
        results[1].EventId.ShouldBe("evt-second");
    }
}
