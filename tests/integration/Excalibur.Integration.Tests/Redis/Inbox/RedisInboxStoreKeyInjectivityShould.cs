// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.Redis;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.Redis.Inbox;

/// <summary>
/// Real-infrastructure lock binding the requirement that the Redis inbox key is INJECTIVE in
/// (tenant, message, handler) against a live Redis container.
/// </summary>
/// <remarks>
/// <para>
/// The key was composed as <c>{KeyPrefix}:{tenantId}:{messageId}:{handlerType}</c>. Neither the tenant
/// term nor the message id is validated against any charset -- both are caller data -- so tenant "a:b"
/// with message "c" and tenant "a" with message "b:c" composed the SAME key and shared one entry.
/// </para>
/// <para>
/// This is the dedup key, so the collision is silent: the second message reads as already-seen and is
/// never processed and never retried. Silent message loss, across a tenant boundary.
/// </para>
/// <para>
/// Exercised through the real store against real Redis rather than by asserting the composed string, so
/// the property under test is the one that matters -- does the second tenant's message get delivered --
/// and not merely that a helper returns different text. Never skipped: the fixture fails fast when Docker
/// is unavailable.
/// </para>
/// </remarks>
[Collection(RedisTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Redis")]
[Trait("Component", "Inbox")]
public sealed class RedisInboxStoreKeyInjectivityShould
{
	private const string Handler = "OrderPlacedHandler";
	private readonly RedisContainerFixture _fixture;

	public RedisInboxStoreKeyInjectivityShould(RedisContainerFixture fixture) => _fixture = fixture;

	/// <summary>
	/// SAFETY: a colon shifted across the tenant/message boundary must not read as an already-seen message.
	/// </summary>
	[Fact]
	public async Task Not_treat_a_shifted_colon_tuple_as_an_already_seen_message()
	{
		// ONE key prefix, so both stores address the same Redis keyspace -- the collision is only
		// observable when the two tenants share it.
		var prefix = $"inbox-injectivity-{Guid.NewGuid():N}";

		// Tenant "a:b" receives message "c". Under the bare join this wrote "{prefix}:a:b:c:H".
		var shiftedIntoTenant = CreateStore("a:b", prefix);
		_ = await shiftedIntoTenant.CreateEntryAsync(
			"c", Handler, "TestMessageType", [1],
			new Dictionary<string, object>(StringComparer.Ordinal), CancellationToken.None)
			.ConfigureAwait(false);

		// A DIFFERENT tenant, "a", receives a DIFFERENT message, "b:c".
		// Under the bare join this composed the identical key, so the entry above answered for it.
		var shiftedIntoMessage = CreateStore("a", prefix);
		var seenByOtherTenant = await shiftedIntoMessage
			.GetEntryAsync("b:c", Handler, CancellationToken.None).ConfigureAwait(false);

		seenByOtherTenant.ShouldBeNull(
			"tenant 'a' message 'b:c' is a different message belonging to a different tenant than tenant "
			+ "'a:b' message 'c'. If the colon shifting across the boundary makes them share a Redis key, "
			+ "the second is refused as a duplicate and is never processed and never retried -- silent "
			+ "message loss, and one tenant's traffic deciding whether another's is delivered.");
	}

	/// <summary>
	/// LIVENESS: an ordinary message still writes, reads back, and is recognised for its own tenant.
	/// </summary>
	/// <remarks>
	/// Required. Without it the safety arm is satisfied by a store that writes every entry under a unique
	/// key and finds nothing on read -- perfectly non-colliding, deduplicating nothing at all, turning the
	/// inbox from at-most-once into at-least-once for every message.
	/// </remarks>
	[Fact]
	public async Task Still_store_and_find_an_ordinary_message_for_its_own_tenant()
	{
		var prefix = $"inbox-injectivity-liveness-{Guid.NewGuid():N}";
		var store = CreateStore("tenant-7", prefix);

		_ = await store.CreateEntryAsync(
			"order-42", Handler, "TestMessageType", [1],
			new Dictionary<string, object>(StringComparer.Ordinal), CancellationToken.None)
			.ConfigureAwait(false);

		var found = await store.GetEntryAsync("order-42", Handler, CancellationToken.None)
			.ConfigureAwait(false);

		found.ShouldNotBeNull(
			"an ordinary message must still be found by the tenant that received it, or the store "
			+ "recognises no duplicates at all");
	}

	/// <summary>
	/// OVER-CORRECTION GUARD: terms that legitimately contain a colon must still write and read back.
	/// </summary>
	/// <remarks>
	/// A "fix" that rejected or stripped colons would pass the safety arm while breaking dedup for every
	/// consumer whose ids contain one -- a URN message id, for instance.
	/// </remarks>
	[Fact]
	public async Task Still_find_a_message_whose_terms_contain_a_colon()
	{
		var prefix = $"inbox-injectivity-colon-{Guid.NewGuid():N}";
		var store = CreateStore("a:b", prefix);

		_ = await store.CreateEntryAsync(
			"urn:uuid:9f8c", Handler, "TestMessageType", [1],
			new Dictionary<string, object>(StringComparer.Ordinal), CancellationToken.None)
			.ConfigureAwait(false);

		var found = await store.GetEntryAsync("urn:uuid:9f8c", Handler, CancellationToken.None)
			.ConfigureAwait(false);

		found.ShouldNotBeNull(
			"a colon is legal caller data in both terms. Making the key injective must not cost dedup for "
			+ "the messages whose ids contain one");
	}

	private RedisInboxStore CreateStore(string tenantId, string keyPrefix)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Redis container must be available — real-infra injectivity lock is never skipped.");

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
