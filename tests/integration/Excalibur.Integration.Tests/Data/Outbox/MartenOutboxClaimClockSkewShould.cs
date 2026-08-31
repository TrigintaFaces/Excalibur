// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.Marten;

using global::Marten;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Author≠impl real-infra lock: <see cref="MartenOutboxStore"/> decides claim eligibility on PostgreSQL's
/// clock, so a dispatcher whose own clock runs ahead cannot take a live claim.
/// </summary>
/// <remarks>
/// <para>
/// Like the Elasticsearch store, this one had no injectable clock before the fix — it read
/// <c>DateTimeOffset.UtcNow</c> directly, so the defect could not be expressed as a test at all. The
/// injected <see cref="TimeProvider"/> is the seam these arms drive.
/// </para>
/// <para>
/// The claim statement now takes every instant from <c>clock_timestamp()</c>: the cutoff it compares
/// <c>claimed_at</c> against, the stamp it writes, and the failure floor. <c>clock_timestamp()</c> rather
/// than <c>now()</c> is deliberate — <c>now()</c> is the enclosing transaction's start time, and the
/// failure floor is written inside a caller-managed transaction, so <c>now()</c> there would be stale by
/// however long that transaction has been open.
/// </para>
/// <para>
/// <b>RED-on-pre-fix-code:</b> restore the client-computed parameters (<c>@Now = DateTimeOffset.UtcNow</c>,
/// <c>@Cutoff = @Now - claimTimeout</c>, with <c>claimed_at &lt; @Cutoff</c>) and
/// <see cref="AClockRunningAheadOfTheDatabase_DoesNotStealALiveClaim"/> goes RED: the skewed dispatcher's
/// cutoff lands past the live claim and <c>RETURNING</c> hands it every message the peer holds.
/// </para>
/// </remarks>
[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Database", "Postgres")]
public sealed class MartenOutboxClaimClockSkewShould
{
	private static readonly TimeSpan ClaimTimeout = TimeSpan.FromMinutes(5);
	private static readonly TimeSpan ShortClaimTimeout = TimeSpan.FromSeconds(2);

	/// <summary>
	/// Shared because <c>DocumentStore.For</c> builds an <c>NpgsqlDataSource</c> that Npgsql pools by
	/// connection string; disposing one would break every later arm in this collection.
	/// </summary>
	private static IDocumentStore? sharedDocumentStore;

	private readonly PostgresOutboxStoreContainerFixture _fixture;

	public MartenOutboxClaimClockSkewShould(PostgresOutboxStoreContainerFixture fixture) => _fixture = fixture;

	private MartenOutboxStore NewStore(TimeProvider? clock = null, TimeSpan? claimTimeout = null)
	{
		var documentStore = sharedDocumentStore ??= DocumentStore.For(opts =>
		{
			opts.Connection(_fixture.ConnectionString);
			opts.AutoCreateSchemaObjects = global::JasperFx.AutoCreate.All;
			opts.DatabaseSchemaName = "marten_outbox_skew";
		});

		var options = new MartenOutboxStoreOptions
		{
			ClaimTimeout = claimTimeout ?? ClaimTimeout,
			ClaimsSchemaName = "marten_outbox_skew",
			ClaimsTableName = "excalibur_outbox_claims_skew",
		};

		return new MartenOutboxStore(
			documentStore, Options.Create(options), NullLogger<MartenOutboxStore>.Instance, clock);
	}

	private static async Task<IReadOnlyCollection<string>> ClaimAsync(MartenOutboxStore store, int batch = 20) =>
		(await store.GetUnsentMessagesAsync(batch, CancellationToken.None).ConfigureAwait(false))
			.Select(m => m.Id)
			.ToList();

	/// <summary>
	/// SAFETY. A dispatcher a full claim timeout ahead is handed nothing its peer holds.
	/// </summary>
	[Fact]
	public async Task AClockRunningAheadOfTheDatabase_DoesNotStealALiveClaim()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"claim-eligibility clock skew is an at-most-once dispatch safety control — never skipped");

		// A separate store instance is a separate dispatcher: the claim identity is per instance.
		var holder = NewStore();
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
			held.ShouldContain(id, "the holder must actually take the claims this arm is about");
		}

		var skewed = NewStore(new SkewedClock(ClaimTimeout + OutboxClockSkewArms.SafetyMargin));
		var stolen = await ClaimAsync(skewed).ConfigureAwait(false);

		foreach (var id in staged)
		{
			stolen.ShouldNotContain(
				id,
				"a dispatcher whose clock runs ahead must not be handed messages a peer is still delivering; "
				+ "the claim is judged by clock_timestamp(), not by the caller");
		}
	}

	/// <summary>
	/// LIVENESS. An elapsed claim is takeable, so the safety arm is not passing on an inert store.
	/// </summary>
	[Fact]
	public async Task AnExpiredClaim_IsTakenOverByThePeer()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the liveness half is what separates a correct claim from one that hands out nothing — never skipped");

		var crashed = NewStore(claimTimeout: ShortClaimTimeout);
		var message = new OutboundMessage("test.message", [1], "dest");
		await crashed.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var claimed = await ClaimAsync(crashed).ConfigureAwait(false);
		claimed.ShouldContain(message.Id, "the first dispatcher must hold the claim before it can lapse");

		var successor = NewStore(claimTimeout: ShortClaimTimeout);
		var reclaimed = await OutboxClockSkewArms.PollUntilClaimableAsync(
			() => ClaimAsync(successor), message.Id, TimeSpan.FromSeconds(30)).ConfigureAwait(false);

		reclaimed.ShouldBeTrue(
			"a message whose holder died must become claimable once its claim elapses on the database's clock");
	}

	/// <summary>
	/// BASE. One un-skewed dispatcher claims and settles normally.
	/// </summary>
	[Fact]
	public async Task AnUnskewedDispatcher_ClaimsAndSettlesNormally()
	{
		_fixture.DockerAvailable.ShouldBeTrue("never skipped");

		var store = NewStore();
		var message = new OutboundMessage("test.message", [7], "dest");
		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var claimed = await ClaimAsync(store).ConfigureAwait(false);
		claimed.ShouldContain(message.Id);

		await store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		var afterSettle = await ClaimAsync(store).ConfigureAwait(false);
		afterSettle.ShouldNotContain(message.Id, "a sent message is terminal and never re-claimed");
	}
}
