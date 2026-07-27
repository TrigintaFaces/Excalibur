// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0

using System.Text;

using Excalibur.Data.InMemory.Snapshots;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Conformance.Snapshot;

/// <summary>
/// Locks the three reachable tenant states of a snapshot store, so none of them can be removed by a
/// refactor without a test going red.
/// </summary>
/// <remarks>
/// <para>
/// <c>TenantScope.FromContext</c> has three outcomes, and every one is load-bearing:
/// </para>
/// <list type="table">
///   <item>
///     <term>no context</term>
///     <description><c>None</c> — the single-tenant path, no predicate emitted, no throw.</description>
///   </item>
///   <item>
///     <term>context, tenant resolved</term>
///     <description>scoped — the multi-tenant path.</description>
///   </item>
///   <item>
///     <term>context, NO tenant resolved</term>
///     <description>
///     <c>TenantRequiredException</c> — fails closed rather than emitting a predicate-less query.
///     </description>
///   </item>
/// </list>
/// <para>
/// The third is the one that disappears quietly. A test double whose tenant is never null — a fallback
/// such as <c>?? "default"</c>, or a mutable field initialised to a value — makes that state
/// <b>unreachable</b>, and once unreachable nothing fails when the guard itself is deleted. The suite
/// still passes and the store silently stops failing closed. These arms exist so that the reachability
/// of all three states is asserted rather than assumed.
/// </para>
/// <para>
/// Written against <c>InMemorySnapshotStore</c> because the property under test belongs to the shared
/// <c>TenantScope</c> seam every provider funnels through, so one store is sufficient to lock it and
/// no real infrastructure is required. The tenant context here implements <see cref="ITenantContext"/>
/// <b>directly</b> and reads the ambient holder, exactly as the shipped default does: a double that
/// carried its own mutable tenant would violate the interface's documented history constraint
/// ("may not silently switch the resolved tenant of a flow already in progress") and could therefore
/// pass while a conforming provider — one that legally caches the tenant per operation — failed.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SnapshotStoreTenantFailClosedShould
{
	private const string TenantA = "tenant-a";

	/// <summary>
	/// SAFETY. Multi-tenancy active with no resolved tenant must fail closed, never fall through to an
	/// unscoped write.
	/// </summary>
	[Fact]
	public async Task Throw_When_A_Tenant_Context_Is_Supplied_But_No_Tenant_Is_Resolved()
	{
		var store = CreateStore(new AmbientHolderTenantContext());

		// No ambient scope is established, so the context resolves no tenant.
		_ = await Should.ThrowAsync<TenantRequiredException>(
			async () => await store.SaveSnapshotAsync(
				CreateSnapshot("agg-fail-closed", 1, "data"),
				CancellationToken.None)).ConfigureAwait(false);
	}

	/// <summary>
	/// LIVENESS for the arm above. Without this, a store that threw on EVERY call — multi-tenancy
	/// permanently broken — would satisfy the safety arm and look correct.
	/// </summary>
	[Fact]
	public async Task Succeed_When_A_Tenant_Context_Is_Supplied_And_A_Tenant_Is_Resolved()
	{
		var store = CreateStore(new AmbientHolderTenantContext());
		var aggregateId = Guid.NewGuid().ToString();

		using (TenantContextHolder.BeginScope(TenantA))
		{
			await store.SaveSnapshotAsync(
				CreateSnapshot(aggregateId, 1, "scoped-data"),
				CancellationToken.None).ConfigureAwait(false);

			var loaded = await store.GetLatestSnapshotAsync(
				aggregateId,
				"TestAggregate",
				CancellationToken.None).ConfigureAwait(false);

			_ = loaded.ShouldNotBeNull(
				"a resolved tenant must produce a working scoped round-trip, or the fail-closed arm " +
				"above is satisfied by a store that simply never works");
			Encoding.UTF8.GetString(loaded.Data.ToArray()).ShouldBe("scoped-data");
		}
	}

	/// <summary>
	/// LIVENESS for the single-tenant path. A store built with NO context must remain fully usable and
	/// must not fail closed — this is a supported deployment shape, not a degraded one.
	/// </summary>
	[Fact]
	public async Task Operate_Unscoped_When_No_Tenant_Context_Is_Supplied()
	{
		var store = CreateStore(tenantContext: null);
		var aggregateId = Guid.NewGuid().ToString();

		await store.SaveSnapshotAsync(
			CreateSnapshot(aggregateId, 1, "unscoped-data"),
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.GetLatestSnapshotAsync(
			aggregateId,
			"TestAggregate",
			CancellationToken.None).ConfigureAwait(false);

		_ = loaded.ShouldNotBeNull("the single-tenant path must not require a tenant");
		Encoding.UTF8.GetString(loaded.Data.ToArray()).ShouldBe("unscoped-data");
	}

	/// <summary>
	/// SAFETY, the cross-boundary direction. An unscoped reader must not receive a row written under a
	/// tenant — the arm the whole tenant-isolation corpus is missing.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Measured across the tenant-isolation suites: 32 scoped-vs-scoped store constructions and zero
	/// unscoped ones. Every such test asks "does tenant A see tenant B?" and none asks "does a caller
	/// with NO tenant see a tenant's row?" — which is where the confirmed defects were found, in a
	/// different store, on a read path that looked identical to this one.
	/// </para>
	/// <para>
	/// Expected GREEN here, and that is the point: this store derives its key from
	/// <c>TenantScope.FromContext</c>, so an unscoped read composes a different key and cannot reach a
	/// tenant's entry. The arm exists so that stops being an accident of the current implementation.
	/// A key built from anything that ignores the scope makes it fail. The identical assertion against a
	/// SQL store whose read predicate was conditional went RED and returned another tenant's row, so the
	/// shape is proven capable of failing — it is not vacuous by construction, only by this store being
	/// correct.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Not_Serve_A_Tenants_Snapshot_To_An_Unscoped_Reader()
	{
		var aggregateId = Guid.NewGuid().ToString();

		var scopedStore = CreateStore(new AmbientHolderTenantContext());
		using (TenantContextHolder.BeginScope(TenantA))
		{
			await scopedStore.SaveSnapshotAsync(
				CreateSnapshot(aggregateId, 1, "tenant-a-data"),
				CancellationToken.None).ConfigureAwait(false);
		}

		// A single-tenant deployment reading the same aggregate id: no context, no ambient tenant.
		var unscopedStore = CreateStore(tenantContext: null);
		var leaked = await unscopedStore.GetLatestSnapshotAsync(
			aggregateId,
			"TestAggregate",
			CancellationToken.None).ConfigureAwait(false);

		leaked.ShouldBeNull(
			"an unscoped reader must not receive a row written under a tenant; if this fails, the key " +
			"or predicate has stopped depending on the resolved scope");
	}

	private static ISnapshotStore CreateStore(ITenantContext? tenantContext) =>
		new InMemorySnapshotStore(
			Microsoft.Extensions.Options.Options.Create(new InMemorySnapshotOptions()),
			NullLogger<InMemorySnapshotStore>.Instance,
			tenantContext);

	private static ISnapshot CreateSnapshot(string aggregateId, long version, string data) =>
		new FailClosedSnapshot(
			Guid.NewGuid().ToString(),
			aggregateId,
			"TestAggregate",
			version,
			DateTimeOffset.UtcNow,
			Encoding.UTF8.GetBytes(data),
			null,
			null);

	/// <summary>
	/// Reads the ambient tenant, exactly as the shipped default context does. Deliberately carries no
	/// mutator and no fallback: a fallback would make the unresolved state unreachable and silently
	/// vacate the safety arm above.
	/// </summary>
	private sealed class AmbientHolderTenantContext : ITenantContext
	{
		public string? TenantId => TenantContextHolder.Current;

		public bool HasTenant => !string.IsNullOrEmpty(TenantContextHolder.Current);
	}

	private sealed record FailClosedSnapshot(
		string SnapshotId,
		string AggregateId,
		string AggregateType,
		long Version,
		DateTimeOffset CreatedAt,
		ReadOnlyMemory<byte> Data,
		IDictionary<string, object>? Metadata,
		string? TenantId) : ISnapshot;
}
