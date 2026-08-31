// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.ElasticSearch;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Author≠impl real-infra lock: the Elasticsearch outbox's failure-anchored retry floor is both STAMPED and
/// COMPARED on the Elasticsearch node's clock, so a dispatcher whose own clock disagrees can neither retry
/// inside the floor nor be stalled beyond it.
/// </summary>
/// <remarks>
/// <para>
/// The sibling lock in this directory covers the LEASE. This one covers the FLOOR, which was the half left
/// behind: the lease moved to a painless conditional update reading <c>System.currentTimeMillis()</c>, while
/// <c>MarkFailedAsync</c> went on stamping <c>nextAttemptAt</c> from the injected dispatcher clock and the
/// claim query went on comparing it against a caller-computed instant.
/// </para>
/// <para>
/// <b>Why that was not visible before.</b> With both halves on the dispatcher's clock a SINGLE dispatcher is
/// self-consistent: it stamps and compares against the same offset, so the floor it observes is exactly F
/// however wrong its clock is. The defect only exists between two dispatchers, or between a dispatcher and
/// the node — which is why both arms below use two stores whose clocks disagree, and why an arm using one
/// store could not have failed no matter how the skew was chosen. Fixing only the comparison would have made
/// the single-dispatcher case worse rather than better, which is why both halves moved together.
/// </para>
/// <para>
/// <b>verify-against-real-infra-not-mock:</b> a mocked client has no node clock, and the node clock is the
/// entire subject. NON-SKIPPED via <c>DockerAvailable.ShouldBeTrue(...)</c>.
/// </para>
/// <para>
/// <b>RED-on-pre-fix-code.</b> Restore either half and an arm fails.
/// Restore the comparison — <c>new DateRangeQuery("nextAttemptAt") { Lte = (DateMath)now.UtcDateTime }</c> —
/// and <see cref="AClockRunningAheadOfTheNode_DoesNotRetryInsideTheFloor"/> goes RED: the skewed dispatcher's
/// own <c>now</c> is an hour past a floor stamped seconds ago, so the search offers the message and the claim
/// script, seeing no lease, takes it. Restore the stamp —
/// <c>doc.NextAttemptAt = now.AddSeconds(FailureBackoffFloorSeconds)</c> written through a re-indexing CAS —
/// and <see cref="AFloorStampedByASkewedDispatcher_ElapsesOnTheNodeClock"/> goes RED: the gate lands an hour
/// in the node's future and an unskewed peer polls for its whole window without ever seeing the message.
/// </para>
/// </remarks>
[Collection(ElasticsearchOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Database", "ElasticSearch")]
public sealed class ElasticsearchOutboxRetryFloorClockSkewShould
{
	/// <summary>
	/// A floor long enough that the safety arm cannot pass by simply outlasting it while the test runs.
	/// </summary>
	private const int LongFloorSeconds = 120;

	/// <summary>A floor short enough that the two liveness arms can wait it out for real.</summary>
	private const int ShortFloorSeconds = 3;

	/// <summary>
	/// How far the skewed dispatcher's clock runs ahead. Far beyond either floor, so a gate carrying the
	/// skew is unmistakably distinguishable from one anchored on the node.
	/// </summary>
	private static readonly TimeSpan DispatcherSkew = TimeSpan.FromHours(1);

	/// <summary>
	/// How long a liveness arm keeps asking before it calls the message stranded.
	/// </summary>
	/// <remarks>
	/// Generous, and not a tolerance on the store's behaviour. The floor elapses on the NODE's clock while
	/// any wait this test performs is measured on the test host's, and a containerised service under load
	/// does not advance in step with its host. Polling asserts the property without assuming the two agree;
	/// a store that genuinely strands the message still fails when the window closes.
	/// </remarks>
	private static readonly TimeSpan ReclaimWindow = TimeSpan.FromSeconds(45);

	private readonly ElasticsearchOutboxStoreContainerFixture _fixture;

	/// <summary>Initializes a new instance of the <see cref="ElasticsearchOutboxRetryFloorClockSkewShould"/> class.</summary>
	/// <param name="fixture">The shared Elasticsearch container fixture.</param>
	public ElasticsearchOutboxRetryFloorClockSkewShould(ElasticsearchOutboxStoreContainerFixture fixture) =>
		_fixture = fixture;

	/// <summary>
	/// SAFETY. A dispatcher an hour ahead of the node must not treat a floor stamped seconds ago as elapsed.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public async Task AClockRunningAheadOfTheNode_DoesNotRetryInsideTheFloor()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the retry floor is what keeps a failing destination from being hot-looped — never skipped");

		var holder = NewStore("floor-holder-A", LongFloorSeconds);
		var skewed = NewStore("floor-skewed-B", LongFloorSeconds, new SkewedClock(DispatcherSkew));

		try
		{
			var message = new OutboundMessage("test.message", [1], "dest");
			await holder.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

			var claimed = await ClaimAsync(holder).ConfigureAwait(false);
			claimed.ShouldContain(message.Id, "the message must be claimed before it can be reported failed");

			await holder.MarkFailedAsync(message.Id, "boom", 1, CancellationToken.None).ConfigureAwait(false);

			// The failure released the lease, so the floor is now the ONLY thing withholding this message.
			// That is deliberate: it isolates the property under test from the lease the sibling lock covers.
			var takenEarly = await ClaimAsync(skewed).ConfigureAwait(false);

			takenEarly.ShouldNotContain(
				message.Id,
				$"a floor of {LongFloorSeconds}s was stamped moments ago, so no dispatcher may retry this "
				+ "message yet. A dispatcher whose clock runs ahead reading the gate against its OWN now is "
				+ "comparing two machines: it sees an elapsed floor that has not elapsed, and hot-loops the "
				+ "retry the floor exists to prevent.");
		}
		finally
		{
			await DisposeAndCleanAsync(holder, skewed).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// LIVENESS. A floor stamped BY a skewed dispatcher must still elapse on the node's clock, so an
	/// unskewed peer gets the message back on schedule rather than an hour late.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// This is the direction that has no safety symptom and therefore no alarm: the message is simply never
	/// handed back. A store that withholds everything forever satisfies every safety property in the
	/// contract while delivering nothing, so this arm is what separates a correct floor from an inert one.
	/// </remarks>
	[Fact]
	public async Task AFloorStampedByASkewedDispatcher_ElapsesOnTheNodeClock()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"a floor that never elapses is a silent drop, not a backoff — never skipped");

		var skewed = NewStore("floor-writer-A", ShortFloorSeconds, new SkewedClock(DispatcherSkew));
		var peer = NewStore("floor-reader-B", ShortFloorSeconds);

		try
		{
			var message = new OutboundMessage("test.message", [2], "dest");
			await skewed.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

			var claimed = await ClaimAsync(skewed).ConfigureAwait(false);
			claimed.ShouldContain(message.Id, "the skewed dispatcher must hold the lease it then reports on");

			await skewed.MarkFailedAsync(message.Id, "boom", 1, CancellationToken.None).ConfigureAwait(false);

			var returned = await OutboxClockSkewArms.PollUntilClaimableAsync(
				() => ClaimAsync(peer), message.Id, ReclaimWindow).ConfigureAwait(false);

			returned.ShouldBeTrue(
				$"the floor is {ShortFloorSeconds}s and this arm kept asking for "
				+ $"{ReclaimWindow.TotalSeconds:0}s, so a message still withheld is carrying the writing "
				+ $"dispatcher's {DispatcherSkew.TotalHours:0}-hour skew in its gate. A floor stamped on one "
				+ "machine's clock and read on another's is not a floor of F, it is a floor of F plus "
				+ "whatever those two machines disagree by.");
		}
		finally
		{
			await DisposeAndCleanAsync(skewed, peer).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// BASE. With no skew at all the floor withholds the message and then returns it, so neither arm above
	/// is passing because the floor is inert in one direction or absent in the other.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public async Task AnUnskewedDispatcher_IsWithheldForTheFloorAndThenReclaims()
	{
		_fixture.DockerAvailable.ShouldBeTrue("never skipped");

		var store = NewStore("floor-plain-A", ShortFloorSeconds);

		try
		{
			var message = new OutboundMessage("test.message", [3], "dest");
			await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

			var claimed = await ClaimAsync(store).ConfigureAwait(false);
			claimed.ShouldContain(message.Id);

			await store.MarkFailedAsync(message.Id, "boom", 1, CancellationToken.None).ConfigureAwait(false);

			var immediately = await ClaimAsync(store).ConfigureAwait(false);
			immediately.ShouldNotContain(
				message.Id,
				"a failure must withhold the message for the floor, or the drain hot-loops a failing "
				+ "destination at poll cadence.");

			var returned = await OutboxClockSkewArms.PollUntilClaimableAsync(
				() => ClaimAsync(store), message.Id, ReclaimWindow).ConfigureAwait(false);

			returned.ShouldBeTrue(
				"once the floor elapses the message must come back. A floor that never releases is a silent "
				+ "drop wearing a backoff's clothes.");
		}
		finally
		{
			await DisposeAndCleanAsync(store).ConfigureAwait(false);
		}
	}

	private ElasticsearchOutboxStore NewStore(string processorId, int floorSeconds, TimeProvider? clock = null)
	{
		var options = Options.Create(new ElasticsearchOutboxOptions
		{
			IndexName = _fixture.IndexName,
			RefreshPolicy = "wait_for",
			ProcessorId = processorId,
			FailureBackoffFloorSeconds = floorSeconds,
		});

		return new ElasticsearchOutboxStore(
			_fixture.Client, options, NullLogger<ElasticsearchOutboxStore>.Instance, clock);
	}

	private static async Task<IReadOnlyCollection<string>> ClaimAsync(ElasticsearchOutboxStore store, int batch = 20) =>
		(await store.GetUnsentMessagesAsync(batch, CancellationToken.None).ConfigureAwait(false))
			.Select(m => m.Id)
			.ToList();

	/// <summary>
	/// Disposes the stores an arm created and drops the shared index.
	/// </summary>
	/// <remarks>
	/// Every class in this collection is handed the SAME index, so documents left behind here are counted by
	/// the neighbouring classes' index-wide statistics assertions. Dropping the index is load-bearing rather
	/// than tidiness.
	/// </remarks>
	private async Task DisposeAndCleanAsync(params ElasticsearchOutboxStore[] stores)
	{
		foreach (var store in stores)
		{
			await store.DisposeAsync().ConfigureAwait(false);
		}

		await _fixture.DeleteIndexAsync().ConfigureAwait(false);
	}
}
