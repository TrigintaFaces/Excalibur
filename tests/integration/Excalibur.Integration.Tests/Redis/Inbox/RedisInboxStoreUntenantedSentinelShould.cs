// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Excalibur.Inbox.Redis;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Excalibur.Integration.Tests.Redis.Inbox;

/// <summary>
/// vn4p6d — real-Redis lock proving the inbox dedup key converges on ONE representation of "no
/// tenant": the reserved <c>__untenanted__</c> sentinel is woven INTO the key exactly like a real
/// tenant, never omitted.
/// </summary>
/// <remarks>
/// <para>
/// vn4p6d described <c>RedisInboxStore.GetKey</c> as still branching on <c>scope.IsScoped</c> and
/// omitting the tenant segment on the untenanted path. That is no longer true on <c>main</c>:
/// <c>GetKey</c>/<c>StampTenant</c> unconditionally compose <c>TenantScope.FromContext(_tenantContext)
/// .TenantId</c>, which is TOTAL (never absent, per <see cref="TenantScope"/>'s remarks) — and
/// <c>IsScoped</c> does not even exist on <see cref="TenantScope"/> anymore, having been removed by the
/// tenancy-context refactor (main @ 9fe785697 / 9555bb4e6). This suite exists because that fix landed
/// with no dedicated regression lock; it proves the property directly rather than trusting the absence
/// of the old branch to persist.
/// </para>
/// <para>
/// NOT skip-gated. A Docker-unavailable run fails loudly rather than passing vacuously
/// (<c>verify-against-real-infra-not-mock</c>).
/// </para>
/// </remarks>
[Collection(RedisTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Inbox")]
[Trait("Database", "Redis")]
public sealed class RedisInboxStoreUntenantedSentinelShould : IClassFixture<RedisContainerFixture>
{
	private const string Sentinel = "__untenanted__";
	private const string HandlerType = "TestHandler";

	private readonly RedisContainerFixture _fixture;

	public RedisInboxStoreUntenantedSentinelShould(RedisContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// LIVENESS: an untenanted claim is stored under the key that binds the sentinel segment — the
	/// same shape the scoped path uses, not a bare/tenant-less key.
	/// </summary>
	[Fact]
	public async Task Claim_UnderTheUntenantedContext_UsesTheSentinelKeySegment()
	{
		_fixture.DockerAvailable.ShouldBeTrue("Redis container must be available — never skipped.");

		var keyPrefix = $"inbox-untenanted-{Guid.NewGuid():N}";
		const string messageId = "msg-untenanted-1";

		var store = CreateStore(Sentinel, keyPrefix);

		(await store.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("first claim under the untenanted context wins");

		await using var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		var db = connection.GetDatabase();

		var expectedKey = $"{keyPrefix}:{Sentinel}:{messageId}:{HandlerType}";
		(await db.KeyExistsAsync(expectedKey).ConfigureAwait(false)).ShouldBeTrue(
			$"the untenanted claim must land at '{expectedKey}' — the sentinel woven into the key exactly "
			+ "like a real tenant. A bare key (no tenant segment) would mean the untenanted path omits "
			+ "the segment, reintroducing the two-representations defect vn4p6d exists to close.");
	}

	/// <summary>
	/// SAFETY: the untenanted context and a real tenant claiming the SAME message id + handler must each
	/// win independently — the sentinel never collides with, nor is silently subsumed by, a real tenant.
	/// </summary>
	[Fact]
	public async Task Claim_UnderTheUntenantedContextAndARealTenant_BothWinIndependently()
	{
		var keyPrefix = $"inbox-untenanted-vs-real-{Guid.NewGuid():N}";
		const string messageId = "msg-shared-untenanted-and-real";

		var untenantedStore = CreateStore(Sentinel, keyPrefix);
		var realTenantStore = CreateStore("acme-corp", keyPrefix);

		(await untenantedStore.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("the untenanted context is the first writer for its own key");

		(await realTenantStore.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue(
				"a real tenant claiming the same message id must independently win — the sentinel key and "
				+ "the real tenant's key must be distinct, never colliding.");
	}

	/// <summary>
	/// LIVENESS/control: within the untenanted context alone, a second claim of the same message id +
	/// handler must still be deduplicated.
	/// </summary>
	[Fact]
	public async Task Deduplicate_WithinTheUntenantedContext()
	{
		var keyPrefix = $"inbox-untenanted-dedup-{Guid.NewGuid():N}";
		const string messageId = "msg-untenanted-dedup";

		var store = CreateStore(Sentinel, keyPrefix);

		(await store.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("first claim wins");

		(await store.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse("a second claim under the SAME (untenanted) context must be deduplicated");
	}

	private RedisInboxStore CreateStore(string tenantId, string keyPrefix)
	{
		_fixture.DockerAvailable.ShouldBeTrue("Redis container must be available — never skipped.");

		var options = Options.Create(new RedisInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			KeyPrefix = keyPrefix,
		});

		return new RedisInboxStore(
			options, NullLogger<RedisInboxStore>.Instance, new FixedTenantContext(tenantId));
	}

	/// <summary>A minimal ambient tenant context pinned to a single, always-present tenant term.</summary>
	private sealed class FixedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => true;
	}
}
