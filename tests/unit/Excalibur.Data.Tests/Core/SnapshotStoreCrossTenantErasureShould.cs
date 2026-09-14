using Excalibur.Data.InMemory.Snapshots;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.Core;

/// <summary>
/// The bead's close condition, at the store rather than at the encoder: one tenant must not be able to
/// address — or destroy — another tenant's snapshot.
/// </summary>
/// <remarks>
/// The stores key on <c>tenant</c>, <c>aggregateType</c>, <c>aggregateId</c>. Joined with a bare
/// separator these two tuples render the identical key <c>t:a:b:c:d</c>:
/// <code>
/// tenant "a"    type "b:c"  id "d"
/// tenant "a:b"  type "c"    id "d"
/// </code>
/// Erasure is irreversible, so the consequence of that collision is not a wrong read — it is one
/// tenant deleting another tenant's history by passing ordinary strings.
/// </remarks>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class SnapshotStoreCrossTenantErasureShould : IAsyncDisposable
{
	private const string AttackerTenant = "a";
	private const string AttackerType = "b:c";
	private const string VictimTenant = "a:b";
	private const string VictimType = "c";
	private const string SharedId = "d";

	private readonly MutableTenantContext _tenant = new();
	private readonly InMemorySnapshotStore _store;

	public SnapshotStoreCrossTenantErasureShould() =>
		_store = new InMemorySnapshotStore(
			Microsoft.Extensions.Options.Options.Create(new InMemorySnapshotOptions()),
			NullLogger<InMemorySnapshotStore>.Instance,
			_tenant);

	[Fact]
	public async Task Not_let_one_tenant_erase_another_tenants_snapshot()
	{
		_tenant.TenantId = VictimTenant;
		await _store.SaveSnapshotAsync(SnapshotFor(VictimType, version: 7), CancellationToken.None);

		_tenant.TenantId = AttackerTenant;
		await _store.DeleteSnapshotsAsync(SharedId, AttackerType, CancellationToken.None);

		_tenant.TenantId = VictimTenant;
		var survivor = await _store.GetLatestSnapshotAsync(SharedId, VictimType, CancellationToken.None);

		survivor.ShouldNotBeNull(
			"a tenant deleting its own aggregate must not destroy a different tenant's history; "
			+ "erasure is irreversible, so this is the worst reachable consequence of an ambiguous key.");
		survivor.Version.ShouldBe(7);
	}

	[Fact]
	public async Task Not_let_one_tenant_read_another_tenants_snapshot()
	{
		_tenant.TenantId = VictimTenant;
		await _store.SaveSnapshotAsync(SnapshotFor(VictimType, version: 7), CancellationToken.None);

		_tenant.TenantId = AttackerTenant;
		var leaked = await _store.GetLatestSnapshotAsync(SharedId, AttackerType, CancellationToken.None);

		leaked.ShouldBeNull("the two tenants' keys must not coincide, so neither can read the other's.");
	}

	[Fact]
	public async Task Not_let_one_tenant_overwrite_another_tenants_snapshot()
	{
		_tenant.TenantId = VictimTenant;
		await _store.SaveSnapshotAsync(SnapshotFor(VictimType, version: 7), CancellationToken.None);

		_tenant.TenantId = AttackerTenant;
		await _store.SaveSnapshotAsync(SnapshotFor(AttackerType, version: 99), CancellationToken.None);

		_tenant.TenantId = VictimTenant;
		var mine = await _store.GetLatestSnapshotAsync(SharedId, VictimType, CancellationToken.None);

		mine.ShouldNotBeNull();
		mine.Version.ShouldBe(7, "the other tenant's write must not land on this tenant's key.");
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		await _store.DisposeAsync();
		GC.SuppressFinalize(this);
	}

	private static Snapshot SnapshotFor(string aggregateType, long version) =>
		new()
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = SharedId,
			AggregateType = aggregateType,
			Version = version,
			CreatedAt = DateTimeOffset.UtcNow,
			Data = new ReadOnlyMemory<byte>([1, 2, 3])
		};

	private sealed class MutableTenantContext : ITenantContext
	{
		public string? TenantId { get; set; }

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}
}
