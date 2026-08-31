// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MongoDB.Bson;
using MongoDB.Driver;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Author≠impl real-infra lock: <see cref="MongoDbOutboxStore"/> decides claim eligibility on MongoDB's
/// clock, so two dispatchers whose own clocks disagree cannot both hold one message.
/// </summary>
/// <remarks>
/// <para>
/// The store used to compare a lease stamped by one dispatcher against a cutoff computed from another
/// dispatcher's clock. Where those differ by more than the lease timeout, the second reads a live lease as
/// expired and claims a message the first is still delivering — no crash, no pause, no elapsed time, and
/// the atomic <c>FindOneAndUpdate</c> is no defence: under skew the two are not simultaneous, so the second
/// write is the only one at that instant and succeeds on a predicate that was already false. Atomicity
/// arbitrates a race; it does not make the predicate true. The claim now reads <c>$$NOW</c> for both sides
/// of the comparison.
/// </para>
/// <para>
/// <b>verify-against-real-infra-not-mock:</b> the object under test is precisely where the clock is read,
/// so a mocked <c>IMongoCollection</c> would return whatever it was told and could never exhibit the
/// server's own clock. <c>DockerAvailable.ShouldBeTrue(...)</c> makes these NON-SKIPPED.
/// </para>
/// <para>
/// <b>RED-on-pre-fix-code:</b> restore the client-clock predicate (<c>leaseCutoff = _timeProvider.GetUtcNow()
/// - LeaseTimeoutSeconds</c> with <c>filter.Lt(d =&gt; d.LeasedAt, leaseCutoff)</c>) and
/// <see cref="AClockRunningAheadOfTheServer_DoesNotStealALiveLease"/> goes RED: the skewed dispatcher's
/// cutoff lands ahead of the live lease stamp and it claims every message the peer holds.
/// </para>
/// <para>
/// <b>Also RED on the representation:</b> revert
/// <c>[BsonRepresentation(BsonType.DateTime)]</c> on the document's instants and
/// <see cref="TheStoredLeaseInstant_IsABsonDate"/> goes RED — and so, silently, does everything else. The
/// driver's default stores a <see cref="DateTimeOffset"/> as a three-field sub-document, and comparing a
/// sub-document to <c>$$NOW</c> reports EVERY lease expired rather than erroring, which is worse than the
/// skew defect because it needs no skew at all.
/// </para>
/// </remarks>
[Collection(MongoDbOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Database", "MongoDb")]
public sealed class MongoDbOutboxClaimClockSkewShould
{
	private const int LeaseSeconds = 300;
	private const int ShortLeaseSeconds = 2;

	private readonly MongoDbOutboxStoreContainerFixture _fixture;

	public MongoDbOutboxClaimClockSkewShould(MongoDbOutboxStoreContainerFixture fixture) => _fixture = fixture;

	private MongoDbOutboxStore NewStore(string processorId, TimeProvider? clock = null, int leaseSeconds = LeaseSeconds)
	{
		var options = Options.Create(new MongoDbOutboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _fixture.DatabaseName,
			ProcessorId = processorId,
			LeaseTimeoutSeconds = leaseSeconds,
		});

		return new MongoDbOutboxStore(options, NullLogger<MongoDbOutboxStore>.Instance, clock);
	}

	private static async Task<IReadOnlyCollection<string>> ClaimAsync(MongoDbOutboxStore store, int batch = 20) =>
		(await store.GetUnsentMessagesAsync(batch, CancellationToken.None).ConfigureAwait(false))
			.Select(m => m.Id)
			.ToList();

	/// <summary>
	/// SAFETY. A dispatcher whose clock runs a full lease ahead is handed nothing its peer holds.
	/// </summary>
	[Fact]
	public async Task AClockRunningAheadOfTheServer_DoesNotStealALiveLease()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"claim-eligibility clock skew is an at-most-once dispatch safety control — never skipped");
		await _fixture.CleanupAsync().ConfigureAwait(false);

		var holder = NewStore("holder-A");
		for (var i = 0; i < 5; i++)
		{
			await holder.StageMessageAsync(
				new OutboundMessage("test.message", [(byte)i], "dest"), CancellationToken.None).ConfigureAwait(false);
		}

		var held = await ClaimAsync(holder).ConfigureAwait(false);
		held.Count.ShouldBe(5, "the holder must actually take the leases this arm is about");

		// A peer whose clock believes it is a full lease plus a margin later. No time has passed.
		var skewed = NewStore("skewed-B", new SkewedClock(
			TimeSpan.FromSeconds(LeaseSeconds) + OutboxClockSkewArms.SafetyMargin));

		var stolen = await ClaimAsync(skewed).ConfigureAwait(false);

		stolen.ShouldBeEmpty(
			"a dispatcher whose clock runs ahead must not be handed messages a peer is still delivering; "
			+ "the lease is judged on MongoDB's clock, not on the caller's");
	}

	/// <summary>
	/// LIVENESS. Once a lease really elapses, the next dispatcher does get the message.
	/// </summary>
	/// <remarks>
	/// Without this arm the safety assertion above is satisfied by a store that claims nothing ever — a
	/// total stall reads as perfect safety and delivers no messages at all.
	/// </remarks>
	[Fact]
	public async Task AnExpiredLease_IsReclaimedByThePeer()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the liveness half of the skew lock is what separates a correct claim from an inert one — never skipped");
		await _fixture.CleanupAsync().ConfigureAwait(false);

		var crashed = NewStore("crashed-A", leaseSeconds: ShortLeaseSeconds);
		var message = new OutboundMessage("test.message", [1], "dest");
		await crashed.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var claimed = await ClaimAsync(crashed).ConfigureAwait(false);
		claimed.ShouldContain(message.Id, "the first dispatcher must hold the lease before it can lapse");

		// It dies here: never marks sent, never releases. The lease has to lapse on the SERVER's clock,
		// so this wait is real and is polled rather than slept.
		var successor = NewStore("successor-B", leaseSeconds: ShortLeaseSeconds);
		var reclaimed = await OutboxClockSkewArms.PollUntilClaimableAsync(
			() => ClaimAsync(successor), message.Id, TimeSpan.FromSeconds(30)).ConfigureAwait(false);

		reclaimed.ShouldBeTrue(
			"a message whose holder died must become claimable once its lease elapses, or a crashed "
			+ "dispatcher strands it permanently");
	}

	/// <summary>
	/// BASE. One un-skewed dispatcher claims and settles, so the arms above are not passing on a broken store.
	/// </summary>
	[Fact]
	public async Task AnUnskewedDispatcher_ClaimsAndSettlesNormally()
	{
		_fixture.DockerAvailable.ShouldBeTrue("never skipped");
		await _fixture.CleanupAsync().ConfigureAwait(false);

		var store = NewStore("plain-A");
		var message = new OutboundMessage("test.message", [7], "dest");
		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var claimed = await ClaimAsync(store).ConfigureAwait(false);
		claimed.ShouldContain(message.Id);

		await store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		var afterSettle = await ClaimAsync(store).ConfigureAwait(false);
		afterSettle.ShouldNotContain(message.Id, "a sent message is terminal and never re-claimed");
	}

	/// <summary>
	/// The stored lease instant is a BSON date, which is what makes a server-clock comparison expressible.
	/// </summary>
	/// <remarks>
	/// Not a stylistic assertion. <c>$$NOW</c> is a date; the driver's default representation for a
	/// <see cref="DateTimeOffset"/> is a <c>{ DateTime, Ticks, Offset }</c> sub-document. Comparing the two
	/// does not error — measured against real MongoDB, it reports every lease expired, live ones included.
	/// This arm pins the representation the claim predicate depends on.
	/// </remarks>
	[Fact]
	public async Task TheStoredLeaseInstant_IsABsonDate()
	{
		_fixture.DockerAvailable.ShouldBeTrue("never skipped");
		await _fixture.CleanupAsync().ConfigureAwait(false);

		var store = NewStore("shape-A");
		var message = new OutboundMessage("test.message", [3], "dest");
		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		_ = await ClaimAsync(store).ConfigureAwait(false);

		var raw = await new MongoClient(_fixture.ConnectionString)
			.GetDatabase(_fixture.DatabaseName)
			.GetCollection<BsonDocument>("outbox_messages")
			.Find(new BsonDocument("_id", message.Id))
			.FirstAsync()
			.ConfigureAwait(false);

		raw["leasedAt"].BsonType.ShouldBe(
			BsonType.DateTime,
			"a lease stored as a sub-document cannot be compared against the server's $$NOW; the comparison "
			+ "silently reports every lease expired rather than failing");
	}
}
