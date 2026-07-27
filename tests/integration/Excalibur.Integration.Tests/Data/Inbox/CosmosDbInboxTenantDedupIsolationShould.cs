// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.CosmosDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Real-Cosmos lock for tenant isolation of the inbox dedup key.
/// </summary>
/// <remarks>
/// <para>
/// The inbox dedups on <c>(messageId, handlerType)</c>. Without the tenant composed into that key, two tenants
/// legitimately processing the SAME upstream <c>messageId</c> collide: the second tenant's message is
/// swallowed as a duplicate and its handler never runs. That is silent cross-tenant message LOSS, not a
/// leak — which is why it survives a leak-shaped review. The store composes the keyed tenant partition into
/// the document id (<c>ScopedId</c>), making the collision unrepresentable.
/// </para>
/// <para>
/// <b>Serializer fidelity (the S855 hazard).</b> The store builds its own <c>CosmosClient</c> with
/// <c>UseSystemTextJsonSerializerWithOptions</c> + camelCase. Any verification client used here MUST match
/// that configuration, or it reads PascalCase fields, sees nothing, and reports a false result. This lock
/// therefore asserts through the STORE's own API rather than a hand-configured raw client — the emitted
/// behaviour, never a hand-rolled read of the wire shape.
/// </para>
/// <para>
/// <b>Real infrastructure, never mocked.</b> The dedup is enforced by Cosmos itself (a 409 on the id). A
/// mocked container returns whatever it was told and cannot produce the collision, so a mock-grade lock here
/// would certify the defect as fixed.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "CosmosDb")]
[Trait("Component", "Inbox")]
public sealed class CosmosDbInboxTenantDedupIsolationShould(CosmosDbTransactionalInboxExactlyOnceFixture fixture)
	: IClassFixture<CosmosDbTransactionalInboxExactlyOnceFixture>
{
	private const string HandlerType = "OrderPlacedHandler";
	private const string TenantA = "tenant-A";
	private const string TenantB = "tenant-B";

	private readonly CosmosDbTransactionalInboxExactlyOnceFixture _fixture = fixture;

	private sealed class FixedTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => TenantId is not null;
	}

	[Fact]
	public async Task NotSwallowOneTenantsMessage_WhenAnotherTenantAlreadyProcessedTheSameMessageId()
	{
		// SAFETY — the cross-tenant dedup collision. Tenant A processes messageId M; tenant B then receives its
		// OWN message that happens to carry the same upstream id M. B's claim must SUCCEED: the two are different
		// messages in different tenants and both handlers must run. RED against a tenant-less dedup id, where B's
		// claim is refused as a duplicate of A's and B's message is silently dropped.
		var store = await InitializedStoreAsync().ConfigureAwait(false);
		var messageId = "msg-" + Guid.NewGuid().ToString("N");

		bool claimedByA, claimedByB;
		using (TenantContextHolder.BeginScope(TenantA))
		{
			claimedByA = await StoreFor(TenantA).TryClaimAsync(messageId, HandlerType, CancellationToken.None)
				.ConfigureAwait(false);
		}

		using (TenantContextHolder.BeginScope(TenantB))
		{
			claimedByB = await StoreFor(TenantB).TryClaimAsync(messageId, HandlerType, CancellationToken.None)
				.ConfigureAwait(false);
		}

		claimedByA.ShouldBeTrue("tenant A's claim of its own message must succeed");
		claimedByB.ShouldBeTrue(
			"tenant B's message must NOT be swallowed as a duplicate of tenant A's: the dedup key must compose " +
			"the tenant, or two tenants sharing an upstream messageId silently lose one of the two messages — " +
			"cross-tenant message loss, invisible to any leak-shaped assertion");

		_ = store;
	}

	[Fact]
	public async Task StillDedupe_WhenTheSameTenantResubmitsTheSameMessageId()
	{
		// LIVENESS — proves the safety arm is not vacuous. A store that composed a UNIQUE id per call (or never
		// deduped at all) would pass the arm above while destroying exactly-once: a tenant's own retry would be
		// processed twice. Within ONE tenant the second claim must be refused.
		var store = await InitializedStoreAsync().ConfigureAwait(false);
		var messageId = "msg-" + Guid.NewGuid().ToString("N");

		using var scope = TenantContextHolder.BeginScope(TenantA);
		var tenantStore = StoreFor(TenantA);

		var first = await tenantStore.TryClaimAsync(messageId, HandlerType, CancellationToken.None)
			.ConfigureAwait(false);
		var second = await tenantStore.TryClaimAsync(messageId, HandlerType, CancellationToken.None)
			.ConfigureAwait(false);

		first.ShouldBeTrue("the first claim within a tenant must succeed");
		second.ShouldBeFalse(
			"a tenant re-submitting its OWN messageId must still be deduped — composing the tenant into the key " +
			"must not weaken same-tenant exactly-once, only separate the tenants from each other");

		_ = store;
	}

	[Fact]
	public async Task KeepEachTenantsProcessedStateSeparate_ForTheSameMessageId()
	{
		// SAFETY (read side) — the twin of the claim arm. After tenant A marks M processed, tenant B asking about
		// its OWN M must still read "not processed": a tenant-less key would report A's state as B's, so B's
		// message is skipped as already-done. RED against a tenant-less dedup id.
		var store = await InitializedStoreAsync().ConfigureAwait(false);
		var messageId = "msg-" + Guid.NewGuid().ToString("N");

		using (TenantContextHolder.BeginScope(TenantA))
		{
			_ = await StoreFor(TenantA).TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None)
				.ConfigureAwait(false);
		}

		bool processedForA, processedForB;
		using (TenantContextHolder.BeginScope(TenantA))
		{
			processedForA = await StoreFor(TenantA).IsProcessedAsync(messageId, HandlerType, CancellationToken.None)
				.ConfigureAwait(false);
		}

		using (TenantContextHolder.BeginScope(TenantB))
		{
			processedForB = await StoreFor(TenantB).IsProcessedAsync(messageId, HandlerType, CancellationToken.None)
				.ConfigureAwait(false);
		}

		processedForA.ShouldBeTrue(
			"tenant A must see its own message as processed — otherwise the isolation assertion below is vacuous " +
			"(a store that reported 'not processed' for everyone would pass it while being inert)");
		processedForB.ShouldBeFalse(
			"tenant B must NOT see tenant A's processed-state for the same messageId — reading another tenant's " +
			"dedup state causes B's message to be skipped as already-handled");

		_ = store;
	}

	private CosmosDbInboxStore StoreFor(string? tenantId)
	{
		var options = Options.Create(new CosmosDbInboxOptions
		{
			DatabaseName = _fixture.DatabaseName,
			ContainerName = _fixture.ContainerName,
			PartitionKeyPath = "/handler_type",
			DefaultTimeToLiveSeconds = 0,
			Client =
			{
				ConnectionString = _fixture.ConnectionString,
				UseDirectMode = false,
				HttpClientFactory = _fixture.HttpClientFactory,
			},
		});

		return new CosmosDbInboxStore(options, NullLogger<CosmosDbInboxStore>.Instance, new FixedTenantContext(tenantId));
	}

	private async Task<CosmosDbInboxStore> InitializedStoreAsync()
	{
		// NON-SKIP. Cross-tenant message loss is a correctness boundary; a skip-gated infra lock passes by not
		// running, which is the exact gap that ships the defect. If the emulator is unavailable this fails LOUD
		// rather than reporting a green it did not earn.
		_fixture.IsInitialized.ShouldBeTrue(
			"the Cosmos emulator must be available — this cross-tenant dedup lock must never pass by being " +
			"skipped. Initialization error: " + (_fixture.InitError ?? "<none>"));

		var store = StoreFor(null);
		await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
		return store;
	}
}
