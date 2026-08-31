// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Integration.Tests.Data.Outbox;
using Excalibur.Outbox.Redis;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using StackExchange.Redis;

using Xunit;

namespace Excalibur.Integration.Tests.Redis.Outbox;

/// <summary>
/// Author≠impl real-infra lock: <see cref="RedisOutboxStore"/> decides claim eligibility on the Redis
/// server's clock, so a dispatcher whose own clock runs ahead cannot take a live lease.
/// </summary>
/// <remarks>
/// <para>
/// The claim script now derives every instant it uses from <c>redis.call('TIME')</c> — the lease expiry it
/// writes, the cutoff it reclaims against, and the gate it promotes scheduled messages by. The C# caller
/// passes <b>no clock at all</b>, so the skewed-claim defect is not merely fixed here but
/// <b>inexpressible</b>: there is no longer a parameter through which a caller's clock could reach the
/// decision. That is why the safety arm below can advance the store's <see cref="TimeProvider"/> arbitrarily
/// far and still observe nothing being stolen.
/// </para>
/// <para>
/// <c>TIME</c> is safe to call before a write here because Redis scripts replicate by their <i>effects</i>
/// rather than verbatim, so a replica records what the master computed instead of re-reading its own clock.
/// Verified against a live master/replica pair: both recorded an identical score.
/// </para>
/// <para>
/// <b>verify-against-real-infra-not-mock:</b> the clock under test is the server's, which no mocked
/// <c>IDatabase</c> can exhibit. NON-SKIPPED via <c>DockerAvailable.ShouldBeTrue(...)</c>.
/// </para>
/// <para>
/// <b>RED-on-pre-fix-code:</b> restore the caller-supplied instants (<c>ARGV[1] = now</c>,
/// <c>ARGV[3] = now + leaseTimeout</c>, computed from <c>_timeProvider</c>) and
/// <see cref="AClockRunningAheadOfTheServer_DoesNotStealALiveLease"/> goes RED: the skewed dispatcher's
/// <c>now</c> is past the peer's lease-expiry score, so step 1 reclaims every live lease and step 2 hands
/// them straight over.
/// </para>
/// </remarks>
[Collection(RedisTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Database", "Redis")]
public sealed class RedisOutboxClaimClockSkewShould : IAsyncLifetime
{
	private const int LeaseSeconds = 300;
	private const int ShortLeaseSeconds = 2;

	private readonly RedisContainerFixture _fixture;
	private readonly List<ConnectionMultiplexer> _connections = [];
	private string _keyPrefix = string.Empty;

	public RedisOutboxClaimClockSkewShould(RedisContainerFixture fixture) => _fixture = fixture;

	public ValueTask InitializeAsync()
	{
		// Its own key space per test class run, so these arms never observe another suite's messages.
		_keyPrefix = $"outbox-skew-{Guid.NewGuid():N}";
		return ValueTask.CompletedTask;
	}

	public async ValueTask DisposeAsync()
	{
		foreach (var connection in _connections)
		{
			await connection.DisposeAsync().ConfigureAwait(false);
		}

		_connections.Clear();
	}

	private async Task<RedisOutboxStore> NewStoreAsync(
		string processorId, TimeProvider? clock = null, int leaseSeconds = LeaseSeconds)
	{
		var options = Options.Create(new RedisOutboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			KeyPrefix = _keyPrefix,
			ProcessorId = processorId,
			LeaseTimeoutSeconds = leaseSeconds,
		});

		var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		_connections.Add(connection);

		return new RedisOutboxStore(connection, options, NullLogger<RedisOutboxStore>.Instance, clock);
	}

	private static async Task<IReadOnlyCollection<string>> ClaimAsync(RedisOutboxStore store, int batch = 20) =>
		(await store.GetUnsentMessagesAsync(batch, CancellationToken.None).ConfigureAwait(false))
			.Select(m => m.Id)
			.ToList();

	/// <summary>
	/// SAFETY. A dispatcher a full lease ahead of the server is handed nothing its peer holds.
	/// </summary>
	[Fact]
	public async Task AClockRunningAheadOfTheServer_DoesNotStealALiveLease()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"claim-eligibility clock skew is an at-most-once dispatch safety control — never skipped");

		var holder = await NewStoreAsync("holder-A").ConfigureAwait(false);
		var staged = new List<string>();
		for (var i = 0; i < 5; i++)
		{
			var message = new OutboundMessage("test.message", [(byte)i], "dest");
			await holder.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
			staged.Add(message.Id);
		}

		var held = await ClaimAsync(holder).ConfigureAwait(false);
		foreach (var id in staged)
		{
			held.ShouldContain(id, "the holder must actually take the leases this arm is about");
		}

		var skewed = await NewStoreAsync(
			"skewed-B",
			new SkewedClock(TimeSpan.FromSeconds(LeaseSeconds) + OutboxClockSkewArms.SafetyMargin))
			.ConfigureAwait(false);

		var stolen = await ClaimAsync(skewed).ConfigureAwait(false);

		stolen.ShouldBeEmpty(
			"a dispatcher whose clock runs ahead must not be handed messages a peer is still delivering; "
			+ "every instant the claim script uses comes from the Redis server");
	}

	/// <summary>
	/// LIVENESS. An elapsed lease is reclaimed, so the safety arm is not passing on an inert store.
	/// </summary>
	[Fact]
	public async Task AnExpiredLease_IsReclaimedByThePeer()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the liveness half is what separates a correct claim from one that returns nothing to anybody — never skipped");

		var crashed = await NewStoreAsync("crashed-A", leaseSeconds: ShortLeaseSeconds).ConfigureAwait(false);
		var message = new OutboundMessage("test.message", [1], "dest");
		await crashed.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var claimed = await ClaimAsync(crashed).ConfigureAwait(false);
		claimed.ShouldContain(message.Id, "the first dispatcher must hold the lease before it can lapse");

		var successor = await NewStoreAsync("successor-B", leaseSeconds: ShortLeaseSeconds).ConfigureAwait(false);
		var reclaimed = await OutboxClockSkewArms.PollUntilClaimableAsync(
			() => ClaimAsync(successor), message.Id, TimeSpan.FromSeconds(30)).ConfigureAwait(false);

		reclaimed.ShouldBeTrue(
			"a message whose holder died must become claimable once its lease elapses on the server's clock");
	}

	/// <summary>
	/// BASE. One un-skewed dispatcher claims and settles normally.
	/// </summary>
	[Fact]
	public async Task AnUnskewedDispatcher_ClaimsAndSettlesNormally()
	{
		_fixture.DockerAvailable.ShouldBeTrue("never skipped");

		var store = await NewStoreAsync("plain-A").ConfigureAwait(false);
		var message = new OutboundMessage("test.message", [7], "dest");
		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var claimed = await ClaimAsync(store).ConfigureAwait(false);
		claimed.ShouldContain(message.Id);

		await store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		var afterSettle = await ClaimAsync(store).ConfigureAwait(false);
		afterSettle.ShouldNotContain(message.Id, "a sent message is terminal and never re-claimed");
	}
}
