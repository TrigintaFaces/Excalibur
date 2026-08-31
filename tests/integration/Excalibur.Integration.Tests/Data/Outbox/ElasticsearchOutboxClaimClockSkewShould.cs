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
/// Author≠impl real-infra lock: <see cref="ElasticsearchOutboxStore"/> decides the lease on the
/// Elasticsearch node's clock, so a dispatcher whose own clock runs ahead cannot take a live lease.
/// </summary>
/// <remarks>
/// <para>
/// Before the fix this store had no injectable clock at all — it called <c>DateTimeOffset.UtcNow</c>
/// directly, so the defect was not merely unfixed, it was <b>unfalsifiable</b>: there was no seam at which
/// a test could express "this dispatcher's clock disagrees". Injecting <see cref="TimeProvider"/> is what
/// makes these arms writable, and is separately the abstraction the framework mandates for time.
/// </para>
/// <para>
/// The claim is now a painless script evaluated on the node: it reads <c>System.currentTimeMillis()</c>,
/// compares the stored lease against it, and stamps the new lease from that same reading, declining a live
/// lease with <c>ctx.op = 'noop'</c>. The compare-and-swap on <c>if_seq_no</c>/<c>if_primary_term</c> is
/// kept, because the two guard different things: the CAS refuses a candidate that changed since the search,
/// while the script is what makes the eligibility predicate true rather than merely atomic.
/// </para>
/// <para>
/// <b>verify-against-real-infra-not-mock:</b> a mocked client cannot exhibit the node's clock, which is the
/// entire subject. NON-SKIPPED via <c>DockerAvailable.ShouldBeTrue(...)</c>.
/// </para>
/// <para>
/// <b>RED-on-pre-fix-code:</b> restore the client-clock claim — <c>leaseExpiresAt = now.AddSeconds(...)</c>
/// written through a plain indexing CAS, with the search comparing <c>leaseExpiresAt</c> against a
/// caller-computed <c>now</c> — and <see cref="AClockRunningAheadOfTheNode_DoesNotStealALiveLease"/> goes
/// RED: the skewed dispatcher's <c>now</c> is past the live lease, so the search offers those messages and
/// the unconditional write takes them.
/// </para>
/// </remarks>
[Collection(ElasticsearchOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Database", "ElasticSearch")]
public sealed class ElasticsearchOutboxClaimClockSkewShould
{
	private const int LeaseSeconds = 300;
	private const int ShortLeaseSeconds = 2;

	private readonly ElasticsearchOutboxStoreContainerFixture _fixture;

	public ElasticsearchOutboxClaimClockSkewShould(ElasticsearchOutboxStoreContainerFixture fixture) =>
		_fixture = fixture;

	private ElasticsearchOutboxStore NewStore(
		string processorId, TimeProvider? clock = null, int leaseSeconds = LeaseSeconds)
	{
		var options = Options.Create(new ElasticsearchOutboxOptions
		{
			IndexName = _fixture.IndexName,
			RefreshPolicy = "wait_for",
			ProcessorId = processorId,
			LeaseTimeoutSeconds = leaseSeconds,
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
	/// Every class in this collection is handed the SAME index by the fixture, so documents left behind
	/// here are counted by the neighbouring classes' index-wide statistics assertions. Dropping the index
	/// is the convention those classes already follow, and it is load-bearing rather than tidiness.
	/// </remarks>
	private async Task DisposeAndCleanAsync(params ElasticsearchOutboxStore[] stores)
	{
		foreach (var store in stores)
		{
			await store.DisposeAsync().ConfigureAwait(false);
		}

		await _fixture.DeleteIndexAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// SAFETY. A dispatcher a full lease ahead of the node is handed nothing its peer holds.
	/// </summary>
	[Fact]
	public async Task AClockRunningAheadOfTheNode_DoesNotStealALiveLease()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"claim-eligibility clock skew is an at-most-once dispatch safety control — never skipped");

		var holder = NewStore("holder-A");
		var skewed = NewStore("skewed-B", new SkewedClock(
			TimeSpan.FromSeconds(LeaseSeconds) + OutboxClockSkewArms.SafetyMargin));

		try
		{
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

			var stolen = await ClaimAsync(skewed).ConfigureAwait(false);

			stolen.ShouldBeEmpty(
				"a dispatcher whose clock runs ahead must not be handed messages a peer is still delivering; "
				+ "the lease is judged by the Elasticsearch node, not by the caller");
		}
		finally
		{
			await DisposeAndCleanAsync(holder, skewed);
		}
	}

	/// <summary>
	/// LIVENESS. An elapsed lease is reclaimable, so the safety arm is not passing on an inert store.
	/// </summary>
	[Fact]
	public async Task AnExpiredLease_IsReclaimedByThePeer()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the liveness half is what separates a correct claim from one that returns nothing to anybody — never skipped");

		var crashed = NewStore("crashed-A", leaseSeconds: ShortLeaseSeconds);
		var successor = NewStore("successor-B", leaseSeconds: ShortLeaseSeconds);

		try
		{
			var message = new OutboundMessage("test.message", [1], "dest");
			await crashed.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

			var claimed = await ClaimAsync(crashed).ConfigureAwait(false);
			claimed.ShouldContain(message.Id, "the first dispatcher must hold the lease before it can lapse");

			var reclaimed = await OutboxClockSkewArms.PollUntilClaimableAsync(
				() => ClaimAsync(successor), message.Id, TimeSpan.FromSeconds(30)).ConfigureAwait(false);

			reclaimed.ShouldBeTrue(
				"a message whose holder died must become claimable once its lease elapses on the node's clock");
		}
		finally
		{
			await DisposeAndCleanAsync(crashed, successor);
		}
	}

	/// <summary>
	/// BASE. One un-skewed dispatcher claims and settles normally.
	/// </summary>
	[Fact]
	public async Task AnUnskewedDispatcher_ClaimsAndSettlesNormally()
	{
		_fixture.DockerAvailable.ShouldBeTrue("never skipped");

		var store = NewStore("plain-A");

		try
		{
			var message = new OutboundMessage("test.message", [7], "dest");
			await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

			var claimed = await ClaimAsync(store).ConfigureAwait(false);
			claimed.ShouldContain(message.Id);

			await store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

			var afterSettle = await ClaimAsync(store).ConfigureAwait(false);
			afterSettle.ShouldNotContain(message.Id, "a sent message is terminal and never re-claimed");
		}
		finally
		{
			await DisposeAndCleanAsync(store);
		}
	}
}
