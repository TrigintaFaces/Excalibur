// Two tenants, one aggregate identifier, one store. Proven non-vacuous: removing the tenant from
// GetKey turns this RED (the second tenant's save overwrites the first and both reads return the
// same snapshot). It is deliberately written against the SINGLETON shape the DI registration
// actually uses -- see the comment on AmbientTenant for why a per-tenant store instance cannot
// detect this defect at all.
using Excalibur.Data.InMemory.Snapshots;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.InMemory.Tests.Snapshots;

public sealed class SnapshotTenantIsolationShould
{
    // AMBIENT, not fixed. ITenantContext is registered as a SINGLETON and AddMultiTenancy replaces it
    // with an ambient implementation, so ONE store instance serves every tenant and the context varies
    // per request. A fixture with one context per store would create one dictionary per tenant and could
    // never observe a key collision -- which is exactly how my first attempt at this test passed against
    // the unfixed code.
    private sealed class AmbientTenant : ITenantContext
    {
        public string? TenantId { get; set; }
        public bool HasTenant => TenantId is not null;
    }

    private static Snapshot Snap(string data) => new()
    {
        SnapshotId = Guid.NewGuid().ToString(),
        AggregateId = "SAME-ID",
        AggregateType = "Order",
        Version = 1,
        CreatedAt = DateTimeOffset.UtcNow,
        Data = System.Text.Encoding.UTF8.GetBytes(data),
    };

    [Fact]
    public async Task TwoTenantsSameAggregateId_BothSurvive_AndEachReadsItsOwn()
    {
        // ONE store (the singleton), one backing dictionary, tenant varying per operation.
        var ambient = new AmbientTenant();
        var store = new InMemorySnapshotStore(
            Options.Create(new InMemorySnapshotOptions()),
            NullLogger<InMemorySnapshotStore>.Instance,
            ambient);

        ambient.TenantId = "tenant-A";
        await store.SaveSnapshotAsync(Snap("A-DATA"), CancellationToken.None);

        ambient.TenantId = "tenant-B";
        await store.SaveSnapshotAsync(Snap("B-DATA"), CancellationToken.None);

        ambient.TenantId = "tenant-A";
        var fromA = await store.GetLatestSnapshotAsync("SAME-ID", "Order", CancellationToken.None);

        ambient.TenantId = "tenant-B";
        var fromB = await store.GetLatestSnapshotAsync("SAME-ID", "Order", CancellationToken.None);

        fromA.ShouldNotBeNull();
        fromB.ShouldNotBeNull();
        System.Text.Encoding.UTF8.GetString(fromA!.Data.ToArray()).ShouldBe("A-DATA");
        System.Text.Encoding.UTF8.GetString(fromB!.Data.ToArray()).ShouldBe("B-DATA");
    }
}
