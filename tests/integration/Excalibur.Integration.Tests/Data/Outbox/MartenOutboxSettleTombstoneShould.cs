// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.Marten;

using global::Marten;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Npgsql;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-infrastructure locks for the two halves of the Marten outbox's terminal-settle arbitration that
/// the conformance kit does not reach: the tombstone must survive a release, and it must not survive a
/// purge.
/// </summary>
/// <remarks>
/// <para>
/// Marking a message sent is arbitrated in the claim table rather than by reading the document's status,
/// because every caller has its own session and a session applies no optimistic concurrency. The winner
/// leaves its claim row behind as a terminal tombstone; that row is what makes a second settle lose.
/// Two properties hold this together, and neither is exercised by the conformance kit — its
/// <c>MarkFailed_*</c> arms are declared pending for this provider, and no arm inspects the claim table:
/// </para>
/// <list type="number">
/// <item>a release must NOT delete a tombstone, or the next caller settles the same message again;</item>
/// <item>a purge MUST delete it, or the table gains a row for every message ever sent.</item>
/// </list>
/// <para>
/// Both are asserted against a live PostgreSQL container by reading the claim table directly — the
/// tombstone is invisible through <c>IOutboxStore</c>, so nothing short of the real table can see it.
/// Never skipped: a missing container fails fast rather than passing silently.
/// </para>
/// </remarks>
[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class MartenOutboxSettleTombstoneShould : IClassFixture<PostgresOutboxStoreContainerFixture>
{
	private const string TerminalDispatcherId = "__excalibur_settled__";

	private static IDocumentStore? SharedDocumentStore;

	private readonly PostgresOutboxStoreContainerFixture _fixture;
	private readonly MartenOutboxStoreOptions _options = new();

	public MartenOutboxSettleTombstoneShould(PostgresOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// Safety: a release must leave a settled message's tombstone in place, so a second settle still loses.
	/// </summary>
	/// <remarks>
	/// <c>MarkFailedAsync</c> gives up the failing dispatcher's lease so a retried message returns to the
	/// pool once its floor elapses. If that release also cleared a terminal tombstone, the next
	/// <c>MarkSentAsync</c> would find no row, insert one, and settle an already-sent message a second
	/// time — reopening the very race the arbitration closes.
	/// </remarks>
	[Fact]
	public async Task Keep_the_terminal_tombstone_when_a_release_runs_after_the_message_was_settled()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		var message = NewMessage();

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		// A release against an already-settled message — the path that must not clear the tombstone.
		await store.MarkFailedAsync(message.Id, "late failure report", 1, CancellationToken.None).ConfigureAwait(false);

		(await ReadClaimDispatcherAsync(message.Id).ConfigureAwait(false)).ShouldBe(
			TerminalDispatcherId,
			"a release must never clear the terminal tombstone — clearing it lets the message be settled twice");

		// The property that actually matters, asserted through the public surface.
		_ = await Should.ThrowAsync<InvalidOperationException>(
			async () => await store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false));
	}

	/// <summary>
	/// Liveness counterpart: a live dispatcher's lease IS still given up, so the guard above did not
	/// simply disable releasing, and the failed message comes back to the pool once its floor elapses.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Without this arm the safety assertion is satisfied by a release that gives nothing up at all, which
	/// would strand every failed message for the whole claim timeout before any dispatcher could retry it.
	/// </para>
	/// <para>
	/// The release is not a delete. A plain failure leaves the row in place carrying the retry floor F —
	/// deleting it would drop the floor and let the drain re-claim the message on its very next poll, the
	/// hot-loop the floor exists to prevent. What the release gives up is the <i>lease</i>: it rewinds
	/// <c>claimed_at</c> so the claim timeout stops holding the message, leaving the floor as the only
	/// thing that does. So the property asserted here is "back in the pool once F elapses", never "at
	/// once" — and it is asserted through the public surface, because a row-shape assertion would go stale
	/// the moment the mechanism changed, which is exactly how this arm came to be checking for a delete
	/// the store had stopped doing.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Still_release_a_live_dispatchers_lease_so_a_failed_message_returns_to_the_pool_after_its_floor()
	{
		// The shortest floor the store admits: this arm is about whether the message comes back at all,
		// not about how long F is, and a 30-second default would only make it slow.
		_options.FailureBackoffFloorSeconds = 1;

		var store = await CreateStoreAsync().ConfigureAwait(false);
		var message = NewMessage();

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Claiming through the drain writes a real (non-terminal) claim row for this dispatcher.
		var claimed = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		claimed.ShouldContain(
			m => m.Id == message.Id,
			"the staged message must be claimable — otherwise this arm proves nothing about releasing");

		(await ReadClaimDispatcherAsync(message.Id).ConfigureAwait(false)).ShouldNotBeNull(
			"the drain must have written a claim row");

		await store.MarkFailedAsync(message.Id, "transient failure", 1, CancellationToken.None).ConfigureAwait(false);

		// The row survives, and it is the failing dispatcher's own — not the terminal tombstone. A plain
		// failure must not settle the message, or the sibling arm above would be asserting nothing.
		var afterFailure = await ReadClaimDispatcherAsync(message.Id).ConfigureAwait(false);
		afterFailure.ShouldNotBe(
			TerminalDispatcherId,
			"a plain failure must never settle the message — that would strand it under a tombstone");

		// The property that matters, through the public surface: the message really does return to the
		// pool. A release that left the lease in place would withhold it for the whole claim timeout,
		// which is minutes — far beyond this window — so an inert release fails here rather than passing.
		var reclaimed = await PollUntilReclaimedAsync(store, message.Id, TimeSpan.FromSeconds(30)).ConfigureAwait(false);
		reclaimed.ShouldBeTrue(
			"a failed message must return to the pool once its floor elapses; a release that did not give "
			+ "up the lease would hold it for the entire claim timeout instead");
	}

	/// <summary>
	/// Polls the drain until <paramref name="messageId"/> is handed back, or the budget runs out.
	/// </summary>
	/// <remarks>
	/// Polling rather than sleeping for F: the floor is a wall-clock property of the store, and a fixed
	/// delay would either be flaky under load or wastefully long. The budget bounds the failure instead.
	/// </remarks>
	private static async Task<bool> PollUntilReclaimedAsync(MartenOutboxStore store, string messageId, TimeSpan budget)
	{
		var deadline = DateTimeOffset.UtcNow + budget;

		do
		{
			var batch = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
			if (batch.Any(m => m.Id == messageId))
			{
				return true;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
		}
		while (DateTimeOffset.UtcNow < deadline);

		return false;
	}

	/// <summary>
	/// The growth bound: purging a message must remove its tombstone.
	/// </summary>
	/// <remarks>
	/// A settled message keeps its claim row deliberately, so nothing else ever removes it. If cleanup
	/// skipped the claim table, it would accumulate one row per message ever sent — unbounded, and
	/// invisible through <c>IOutboxStore</c>.
	/// </remarks>
	[Fact]
	public async Task Purge_the_terminal_tombstone_when_the_settled_message_is_cleaned_up()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		var message = NewMessage();

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		// Liveness for the assertion below: the row must be there to begin with, or "gone after cleanup"
		// is vacuously true.
		(await ReadClaimDispatcherAsync(message.Id).ConfigureAwait(false)).ShouldBe(
			TerminalDispatcherId,
			"settling must leave the tombstone — otherwise this arm cannot detect a cleanup that purges nothing");

		var purged = await store.CleanupAllTenantsSentMessagesAsync(
			DateTimeOffset.UtcNow.AddMinutes(1), 100, CancellationToken.None).ConfigureAwait(false);

		purged.ShouldBeGreaterThanOrEqualTo(1, "the settled message must actually have been purged");

		(await ReadClaimDispatcherAsync(message.Id).ConfigureAwait(false)).ShouldBeNull(
			"cleanup must purge the claim row of every message it deletes, or the claim table grows without bound");
	}

	/// <summary>
	/// Safety counterpart to the purge: cleanup must not take the claim of a message it did not delete.
	/// </summary>
	/// <remarks>
	/// Without this arm the purge above is satisfied by a cleanup that simply empties the claim table,
	/// which would release live dispatchers' claims and let their in-flight messages be claimed a second
	/// time — a duplicate send caused by the cleanup itself.
	/// </remarks>
	[Fact]
	public async Task Leave_a_live_claim_alone_when_cleaning_up_a_different_settled_message()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		var settled = NewMessage();
		var inFlight = NewMessage();

		await store.StageMessageAsync(settled, CancellationToken.None).ConfigureAwait(false);
		await store.StageMessageAsync(inFlight, CancellationToken.None).ConfigureAwait(false);

		// Put inFlight under a live claim, then settle only the other message.
		_ = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		await store.MarkSentAsync(settled.Id, CancellationToken.None).ConfigureAwait(false);

		_ = await store.CleanupAllTenantsSentMessagesAsync(
			DateTimeOffset.UtcNow.AddMinutes(1), 100, CancellationToken.None).ConfigureAwait(false);

		(await ReadClaimDispatcherAsync(inFlight.Id).ConfigureAwait(false)).ShouldNotBeNull(
			"cleanup must not touch the claim of a message it did not purge — doing so re-exposes an in-flight message");
	}

	private static OutboundMessage NewMessage() =>
		new("Test.MessageType", "test-payload"u8.ToArray(), "test-queue") { Id = Guid.NewGuid().ToString() };

	private async Task<MartenOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available — this real-infra tombstone lock is never skipped.");

		// The IDocumentStore is a shared singleton: DocumentStore.For builds an NpgsqlDataSource that
		// Npgsql pools by connection string, so disposing it would break every later arm in this
		// collection. Its own schema keeps these locks clear of the conformance deriver's documents.
		var documentStore = SharedDocumentStore ??= DocumentStore.For(opts =>
		{
			opts.Connection(_fixture.ConnectionString);
			opts.AutoCreateSchemaObjects = global::JasperFx.AutoCreate.All;
			opts.DatabaseSchemaName = "marten_outbox_tombstone";
		});

		return new MartenOutboxStore(
			documentStore, Options.Create(_options), NullLogger<MartenOutboxStore>.Instance);
	}

	/// <summary>
	/// Reads the claiming dispatcher for a message straight from the claim table, or <see langword="null"/>
	/// when no claim row exists. The tombstone is not observable through <c>IOutboxStore</c>.
	/// </summary>
	private async Task<string?> ReadClaimDispatcherAsync(string messageId)
	{
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);

		// Identifiers come from this test's own default options, never from input; PostgreSQL has no
		// parameter form for an object name.
#pragma warning disable CA2100 // Query built from test-owned identifiers
		await using var command = new NpgsqlCommand(
			$"SELECT dispatcher_id FROM \"{_options.ClaimsSchemaName}\".\"{_options.ClaimsTableName}\" WHERE message_id = @MessageId;",
			connection);
#pragma warning restore CA2100
		_ = command.Parameters.AddWithValue("MessageId", messageId);

		return await command.ExecuteScalarAsync(CancellationToken.None).ConfigureAwait(false) as string;
	}
}
