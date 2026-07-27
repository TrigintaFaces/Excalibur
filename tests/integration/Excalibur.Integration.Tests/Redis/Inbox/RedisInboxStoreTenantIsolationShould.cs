// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.Redis;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Redis.Inbox;

/// <summary>
/// 9x2tv1 — independent (author≠impl, TestsDeveloper) NON-SKIPPED real-Redis tenant-isolation lock for the
/// inbox dedup key. Redis dedups via a per-message key; 9x2tv1 composes the ambient tenant INTO that key
/// (<c>{prefix}:{tenant}:{msg}:{handler}</c> when scoped, byte-identical <c>{prefix}:{msg}:{handler}</c> when
/// unscoped). Two different tenants claiming the SAME <c>MessageId</c>+<c>HandlerType</c> MUST each win exactly
/// once — dedup is per-tenant, never global.
/// </summary>
/// <remarks>
/// Real-infra proof of the tenant dimension end-to-end (a mocked path cannot reproduce the server-side key
/// collision — see <c>verify-against-real-infra-not-mock</c>). Both stores share ONE key prefix so the tenant
/// dimension of the key is what is under test. Non-vacuous by contrast: the within-tenant arm proves the dedup
/// FIRES on a key collision; the cross-tenant arm proves it does NOT fire across tenants — which holds only
/// because the tenant is woven into the key. A pre-fix bare key would make the cross-tenant arm collide (and
/// fail) exactly like the within-tenant arm — a silent cross-tenant message loss.
/// </remarks>
[Collection(RedisTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Redis")]
[Trait("Component", "Inbox")]
public sealed class RedisInboxStoreTenantIsolationShould : IClassFixture<RedisContainerFixture>
{
	private const string TenantA = "tenant-A";
	private const string TenantB = "tenant-B";
	private const string HandlerType = "TestHandler";

	private readonly RedisContainerFixture _fixture;

	public RedisInboxStoreTenantIsolationShould(RedisContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// SAFETY (cross-tenant, no false-dedup) + LIVENESS (each tenant's own claim wins): two tenants claiming the
	/// SAME message id + handler must EACH win exactly once — the tenant-scoped key keeps them distinct.
	/// </summary>
	[Fact]
	public async Task Admit_the_same_message_id_once_per_tenant_without_cross_tenant_collision()
	{
		var keyPrefix = UniquePrefix();
		var storeA = CreateStore(TenantA, keyPrefix);
		var storeB = CreateStore(TenantB, keyPrefix);
		const string MessageId = "msg-shared-across-tenants";

		(await storeA.TryMarkAsProcessedAsync(MessageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("tenant A is the first writer for its own tenant-scoped key");

		(await storeB.TryMarkAsProcessedAsync(MessageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue(
				"tenant B must independently claim the same message id — the tenant is composed INTO the dedup "
				+ "key, so tenant A's key must not shadow tenant B (RED on the pre-fix bare key).");
	}

	/// <summary>
	/// LIVENESS/control (isolation must not disable dedup): within a SINGLE tenant, a second claim of the same
	/// message id + handler MUST still be deduplicated — the tenant-scoping preserves exactly-once per tenant.
	/// </summary>
	[Fact]
	public async Task Deduplicate_within_a_single_tenant()
	{
		var keyPrefix = UniquePrefix();
		var storeA = CreateStore(TenantA, keyPrefix);
		const string MessageId = "msg-within-tenant";

		(await storeA.TryMarkAsProcessedAsync(MessageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("first claim within a tenant wins");

		(await storeA.TryMarkAsProcessedAsync(MessageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse("a second claim within the SAME tenant must be deduplicated (exactly-once per tenant)");
	}

	private static string UniquePrefix() => $"inbox-tenant-iso-{Guid.NewGuid():N}";

	private RedisInboxStore CreateStore(string tenantId, string keyPrefix)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Redis container must be available — real-infra tenant-isolation lock is never skipped.");

		var options = Options.Create(new RedisInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			KeyPrefix = keyPrefix,
		});

		return new RedisInboxStore(
			options, NullLogger<RedisInboxStore>.Instance, new FixedTenantContext(tenantId));
	}

	/// <summary>A minimal ambient tenant context pinned to a single tenant id.</summary>
	private sealed class FixedTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}
}
