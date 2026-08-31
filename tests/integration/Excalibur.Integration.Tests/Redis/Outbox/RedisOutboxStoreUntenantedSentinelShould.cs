// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Excalibur.Outbox.Redis;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Excalibur.Integration.Tests.Redis.Outbox;

/// <summary>
/// fgfhbo — real-Redis lock proving the outbox converges on ONE representation of "no tenant": the
/// reserved <c>__untenanted__</c> sentinel, never an absent hash field.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="OutboxStoreConformanceTestKit"/>'s <c>UntenantedPartition_MustRoundTripItsOwnMessage</c>
/// deliberately accepts EITHER a null tenant or the sentinel — that arm binds the interface-wide
/// contract, which the in-memory store legitimately satisfies with null. This suite binds a STRICTER,
/// Redis-specific property: Redis chose to converge on the sentinel representation the SQL providers
/// use (see <c>Excalibur.Outbox.Postgres/Scripts/002_MakeOutboxTenantTotal.sql</c>), so an untenanted
/// message read back through THIS store must be the sentinel — never null, never an absent field.
/// </para>
/// <para>
/// RED against the pre-fix store, which wrote the <c>TenantId</c> hash field only when non-empty
/// (<c>SerializeToHashEntries</c>) and read it back only when present (<c>DeserializeFromHashEntries</c>)
/// — an untenanted message round-tripped as <see langword="null"/>, not the sentinel.
/// </para>
/// <para>
/// NOT skip-gated. A Docker-unavailable run fails loudly rather than passing vacuously
/// (<c>verify-against-real-infra-not-mock</c>).
/// </para>
/// </remarks>
[Collection(RedisTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Outbox")]
[Trait("Database", "Redis")]
public sealed class RedisOutboxStoreUntenantedSentinelShould : IClassFixture<RedisContainerFixture>
{
	private const string Sentinel = "__untenanted__";

	private readonly RedisContainerFixture _fixture;

	public RedisOutboxStoreUntenantedSentinelShould(RedisContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// LIVENESS: an untenanted message reads back the reserved sentinel — not null, not an absent field.
	/// </summary>
	[Fact]
	public async Task StageAnUntenantedMessage_AndReadBackTheSentinel()
	{
		var (store, _) = await CreateStoreAsync().ConfigureAwait(false);
		var message = CreateMessage(tenantId: null);

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		var drained = await store.GetUnsentMessagesAsync(100, CancellationToken.None).ConfigureAwait(false);
		var own = drained.FirstOrDefault(m => string.Equals(m.Id, message.Id, StringComparison.Ordinal));

		own.ShouldNotBeNull("the untenanted partition must round-trip its own message");
		own.TenantId.ShouldBe(
			Sentinel,
			"an untenanted message must bind the reserved sentinel — Redis converged on the same "
			+ "representation the SQL providers use, so a null readback here means the field was "
			+ "omitted on write, the pre-fix defect (fgfhbo).");
	}

	/// <summary>
	/// SAFETY: a real tenant is stored verbatim — the sentinel conversion never absorbs a real tenant id.
	/// </summary>
	[Fact]
	public async Task StageARealTenantedMessage_AndReadBackTheRealTenantVerbatim()
	{
		var (store, _) = await CreateStoreAsync().ConfigureAwait(false);
		const string tenantId = "acme-corp";
		var message = CreateMessage(tenantId: tenantId);

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		var drained = await store.GetUnsentMessagesAsync(100, CancellationToken.None).ConfigureAwait(false);
		var own = drained.FirstOrDefault(m => string.Equals(m.Id, message.Id, StringComparison.Ordinal));

		own.ShouldNotBeNull();
		own.TenantId.ShouldBe(tenantId, "a real tenant must survive the write unchanged");
	}

	/// <summary>
	/// The sentinel written by one store instance is readable, byte-for-byte, by a SECOND, independently
	/// constructed store instance sharing the same connection and key prefix — proving the sentinel is
	/// the persisted Redis representation, not an in-process artifact of the writer.
	/// </summary>
	[Fact]
	public async Task RoundTripTheSentinel_AcrossAFreshStoreInstance()
	{
		var (writer, keyPrefix) = await CreateStoreAsync().ConfigureAwait(false);
		var message = CreateMessage(tenantId: null);
		await writer.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var (reader, _) = await CreateStoreAsync(keyPrefix).ConfigureAwait(false);
		var drained = await reader.GetUnsentMessagesAsync(100, CancellationToken.None).ConfigureAwait(false);
		var own = drained.FirstOrDefault(m => string.Equals(m.Id, message.Id, StringComparison.Ordinal));

		own.ShouldNotBeNull();
		own.TenantId.ShouldBe(Sentinel, "a fresh store instance must read the same persisted sentinel");
	}

	/// <summary>
	/// Read-tolerance: a row written BEFORE this fix (or by any writer bypassing this store) carries no
	/// <c>TenantId</c> hash field at all. The fixed read path must fold that absence onto the sentinel —
	/// exactly as it folds an explicit null/empty/sentinel — rather than surfacing it as
	/// <see langword="null"/>.
	/// </summary>
	[Fact]
	public async Task ReadALegacyRowMissingTheTenantField_AsTheSentinel()
	{
		_fixture.DockerAvailable.ShouldBeTrue("Redis container must be available — never skipped.");

		var keyPrefix = $"outbox-legacy-{Guid.NewGuid():N}";
		var options = Options.Create(new RedisOutboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			KeyPrefix = keyPrefix,
			SentMessageTtlSeconds = 604800,
			ConnectTimeoutMs = 5000,
			SyncTimeoutMs = 5000,
			AbortOnConnectFail = false
		});

		await using var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		var db = connection.GetDatabase();

		// Write a hash directly, bypassing RedisOutboxStore entirely, replicating the PRE-FIX shape: no
		// TenantId field present at all. Mirrors SerializeToHashEntries's mandatory fields.
		const string messageId = "legacy-msg-1";
		var messageKey = $"{keyPrefix}:msg:{messageId}";
		await db.HashSetAsync(
			messageKey,
			[
				new HashEntry("MessageType", "TestMessageType"),
				new HashEntry("Payload", "legacy-payload"u8.ToArray()),
				new HashEntry("Destination", "test-destination"),
				new HashEntry("CreatedAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
				new HashEntry("Status", 0), // Staged
				new HashEntry("Priority", 0),
				new HashEntry("RetryCount", 0)
				// Deliberately NO TenantId field — the pre-fix legacy shape.
			]).ConfigureAwait(false);
		await db.SortedSetAddAsync($"{keyPrefix}:idx:staged", messageId, 0).ConfigureAwait(false);

		var store = new RedisOutboxStore(options, NullLogger<RedisOutboxStore>.Instance);
		var drained = await store.GetUnsentMessagesAsync(100, CancellationToken.None).ConfigureAwait(false);
		var own = drained.FirstOrDefault(m => string.Equals(m.Id, messageId, StringComparison.Ordinal));

		own.ShouldNotBeNull("a legacy row missing the TenantId field must still round-trip");
		own.TenantId.ShouldBe(
			Sentinel,
			"an absent TenantId field must fold onto the sentinel on read, the same as an explicit "
			+ "null/empty/sentinel — a legacy row and a freshly-written untenanted row must read back "
			+ "identically.");

		await store.DisposeAsync().ConfigureAwait(false);
	}

	private static OutboundMessage CreateMessage(string? tenantId) =>
		new(
			messageType: "TestMessageType",
			payload: "test-payload"u8.ToArray(),
			destination: "test-destination")
		{
			Id = $"msg-{Guid.NewGuid():N}",
			TenantId = tenantId
		};

	private async Task<(RedisOutboxStore Store, string KeyPrefix)> CreateStoreAsync(string? keyPrefix = null)
	{
		_fixture.DockerAvailable.ShouldBeTrue("Redis container must be available — never skipped.");

		var prefix = keyPrefix ?? $"outbox-sentinel-{Guid.NewGuid():N}";
		var options = Options.Create(new RedisOutboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			KeyPrefix = prefix,
			SentMessageTtlSeconds = 604800,
			ConnectTimeoutMs = 5000,
			SyncTimeoutMs = 5000,
			AbortOnConnectFail = false
		});

		var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		var store = new RedisOutboxStore(connection, options, NullLogger<RedisOutboxStore>.Instance);
		return (store, prefix);
	}
}
