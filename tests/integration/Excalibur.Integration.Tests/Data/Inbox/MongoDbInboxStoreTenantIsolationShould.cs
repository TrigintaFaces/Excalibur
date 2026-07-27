// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// 9x2tv1 — independent (author≠impl, TestsDeveloper) NON-SKIPPED real-MongoDB tenant-isolation lock for the
/// inbox dedup key. MongoDB's only uniqueness constraint is the document <c>_id</c>, so the dedup key IS the
/// <c>_id</c>: <c>MongoDbInboxDocument.CreateId(messageId, handlerType, tenantId)</c> composes the ambient
/// tenant INTO it (<c>"{tenant}:{msg}:{handler}"</c> when scoped, byte-identical <c>"{msg}:{handler}"</c> when
/// <see langword="null"/>). Two different tenants claiming the SAME <c>MessageId</c>+<c>HandlerType</c> MUST
/// each win exactly once — dedup is per-tenant, never global.
/// </summary>
/// <remarks>
/// Real-infra proof of the tenant dimension end-to-end (a mocked path cannot reproduce the server-side unique
/// <c>_id</c> collision — see <c>verify-against-real-infra-not-mock</c>). Both stores share ONE collection so
/// the tenant dimension of the <c>_id</c> is what is under test. RED against the pre-fix key (no tenant in
/// <c>_id</c>): tenant B's claim then collides with tenant A's already-processed <c>_id</c> on
/// <c>MessageId</c>+<c>HandlerType</c> alone → treated as a duplicate → <see langword="false"/> = a silent
/// cross-tenant message loss.
/// </remarks>
[Collection(MongoDbInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "MongoDb")]
[Trait("Component", "Inbox")]
public sealed class MongoDbInboxStoreTenantIsolationShould : IClassFixture<MongoDbInboxStoreContainerFixture>
{
	private const string TenantA = "tenant-A";
	private const string TenantB = "tenant-B";
	private const string HandlerType = "TestHandler";

	private readonly MongoDbInboxStoreContainerFixture _fixture;

	public MongoDbInboxStoreTenantIsolationShould(MongoDbInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// SAFETY (cross-tenant, no false-dedup) + LIVENESS (each tenant's own claim wins): two tenants claiming the
	/// SAME message id + handler must EACH win exactly once — the tenant-scoped <c>_id</c> keeps their rows
	/// distinct. RED against a bare (msg, handler) <c>_id</c>: tenant B would collide with tenant A → false.
	/// </summary>
	[Fact]
	public async Task Admit_the_same_message_id_once_per_tenant_without_cross_tenant_collision()
	{
		var collectionName = UniqueCollection();
		var storeA = CreateStore(TenantA, collectionName);
		var storeB = CreateStore(TenantB, collectionName);
		const string MessageId = "msg-shared-across-tenants";

		// Tenant A claims the message first (LIVENESS — a valid first-writer wins).
		(await storeA.TryMarkAsProcessedAsync(MessageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("tenant A is the first writer for its own tenant-scoped _id");

		// Tenant B claims the SAME MessageId+HandlerType — must ALSO win exactly once (SAFETY: dedup is
		// PER-TENANT, not global). A false here is the cross-tenant dedup collision this lock guards against.
		(await storeB.TryMarkAsProcessedAsync(MessageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue(
				"tenant B must independently claim the same message id — the tenant is composed INTO the dedup "
				+ "_id, so tenant A's row must not shadow tenant B (RED on the pre-fix bare-_id key).");
	}

	/// <summary>
	/// LIVENESS (isolation must not disable dedup): within a SINGLE tenant, a second claim of the same message
	/// id + handler MUST still be deduplicated — the tenant-scoping preserves exactly-once per tenant.
	/// </summary>
	[Fact]
	public async Task Deduplicate_within_a_single_tenant()
	{
		var collectionName = UniqueCollection();
		var storeA = CreateStore(TenantA, collectionName);
		const string MessageId = "msg-within-tenant";

		(await storeA.TryMarkAsProcessedAsync(MessageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("first claim within a tenant wins");

		(await storeA.TryMarkAsProcessedAsync(MessageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse("a second claim within the SAME tenant must be deduplicated (exactly-once per tenant)");
	}

	private static string UniqueCollection() => $"inbox_tenant_iso_{Guid.NewGuid():N}";

	private MongoDbInboxStore CreateStore(string? tenantId, string collectionName)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB container must be available — real-infra tenant-isolation lock is never skipped.");

		var options = Options.Create(new MongoDbInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _fixture.DatabaseName,
			CollectionName = collectionName,
		});

		return new MongoDbInboxStore(
			options, NullLogger<MongoDbInboxStore>.Instance, new FixedTenantContext(tenantId));
	}

	/// <summary>A minimal ambient tenant context pinned to a single tenant id (or none).</summary>
	private sealed class FixedTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}
}
