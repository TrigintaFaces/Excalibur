// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.Postgres;
using Excalibur.Integration.Tests.Data.EventStore;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Npgsql;

using Tests.Shared.Infrastructure;

#pragma warning disable CA2100 // SQL strings use a compile-time const table name in a test fixture.

namespace Excalibur.Integration.Tests.Inbox.Postgres;

/// <summary>
/// Real-Postgres lock for the <b>fenced</b> half of the lease protocol: a caller whose lease has lapsed
/// must not be able to finalize the record of the caller that replaced it.
/// </summary>
/// <remarks>
/// <para>
/// The sibling suite (<see cref="PostgresInboxStoreLeaseReclaimShould"/>) binds <i>admission</i> — who gets
/// the lease. This one binds <i>finalization</i> — who is still allowed to write once they have it. Those
/// were separate defects: admission was already atomic and correct, while finalization carried nothing
/// identifying the holder, so a handler that outran its own lease wrote onto its successor's row.
/// </para>
/// <para>
/// <b>Why no status predicate can substitute.</b> At the instant of the bad write the row is legitimately
/// <see cref="InboxStatus.Processing"/> — its successor's. Every store already carried
/// <c>status != Processed</c> on its writes; that protects the terminal <i>state</i> and never the
/// <i>term</i>. Only the term separates the two callers.
/// </para>
/// <para>
/// <b>Why real infrastructure.</b> The term is the expiry the SERVER wrote, read back through
/// <c>RETURNING lease_expires_at</c>, and the fence compares it inside the write's own predicate. A mocked
/// connection returns whatever it was told and cannot certify either half — nor the round-trip through
/// <c>timestamptz</c>, which is where a lossy encoding would silently make every finalization fail closed.
/// </para>
/// <para>
/// <b>Determinism:</b> a short lease and real elapsed time, polled past expiry with a bounded wait. Every
/// assertion is an eventual-truth or a lower bound, so load can only lengthen the poll — never flip an
/// outcome. No wall-clock upper bounds.
/// </para>
/// <para>
/// <b>Non-vacuity:</b> the SAFETY arms go RED against the pre-fix store, where finalization carried no term
/// and the lapsed caller's write landed. The LIVENESS arms fail a store that simply refuses everything,
/// which would otherwise satisfy every safety arm by doing nothing.
/// </para>
/// </remarks>
[Collection(PostgresEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Postgres")]
[Trait("Component", "Inbox")]
public sealed class PostgresInboxStoreLeaseFencingShould : IClassFixture<PostgresEventStoreContainerFixture>
{
	private const string TableName = "inbox_lease_fencing_test";
	private static readonly TimeSpan ShortLease = TimeSpan.FromMilliseconds(750);
	private static readonly TimeSpan LongLease = TimeSpan.FromMinutes(5);
	private static readonly TimeSpan ReclaimDeadline = TimeSpan.FromSeconds(30);

	private readonly PostgresEventStoreContainerFixture _fixture;

	public PostgresInboxStoreLeaseFencingShould(PostgresEventStoreContainerFixture fixture) =>
		_fixture = fixture;

	/// <summary>
	/// Drives the two-caller lapse against the real store: A acquires under a short lease, the lease runs
	/// out on the SERVER clock, B reclaims. Returns both terms.
	/// </summary>
	private async Task<(PostgresInboxStore Store, string MessageId, string HandlerType, LeaseToken TermA, LeaseToken TermB)>
		LapsedReclaimAsync()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		var messageId = $"msg-{Guid.NewGuid():N}";
		var handlerType = $"handler-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		var termA = (await store.TryAcquireLeaseAsync(messageId, handlerType, ShortLease, ct).ConfigureAwait(false))
			.ShouldNotBeNull("the first caller must be admitted on a key the store has never seen");

		// Poll until the SERVER clock has passed the expiry and B can reclaim. Bounded, never an upper-bound
		// timing assertion: a slow box only lengthens the poll.
		LeaseToken? reclaimed = null;
		var deadline = DateTime.UtcNow + ReclaimDeadline;

		while (DateTime.UtcNow < deadline)
		{
			reclaimed = await store.TryAcquireLeaseAsync(messageId, handlerType, LongLease, ct).ConfigureAwait(false);

			if (reclaimed is not null)
			{
				break;
			}

			await Task.Delay(50, ct).ConfigureAwait(false);
		}

		var termB = reclaimed.ShouldNotBeNull(
			"an expired lease MUST be reclaimable, or a dead processor would block the message forever");

		return (store, messageId, handlerType, termA, termB);
	}

	// SAFETY (headline) — the lapsed caller cannot finalize its successor's record.
	[Fact]
	public async Task RefuseToCompleteUnderALapsedTerm()
	{
		var (store, messageId, handlerType, termA, _) = await LapsedReclaimAsync().ConfigureAwait(false);
		var ct = CancellationToken.None;

		(await store.CompleteAsync(messageId, handlerType, termA, ct).ConfigureAwait(false)).ShouldBeFalse(
			"A's lease had lapsed and been reclaimed, so its finalize must match no row");

		// The write must not merely REPORT failure — it must not have happened.
		(await store.IsProcessedAsync(messageId, handlerType, ct).ConfigureAwait(false)).ShouldBeFalse(
			"B is still processing; A must not have marked B's entry terminal");

		var entry = await store.GetEntryAsync(messageId, handlerType, ct).ConfigureAwait(false);
		entry.ShouldNotBeNull();
		entry.Status.ShouldBe(InboxStatus.Processing, "the entry still belongs to B");
	}

	// SAFETY — the failure path, which is the one that would resurrect a terminal entry.
	[Fact]
	public async Task RefuseToFailUnderALapsedTerm()
	{
		var (store, messageId, handlerType, termA, _) = await LapsedReclaimAsync().ConfigureAwait(false);
		var ct = CancellationToken.None;

		(await store.FailAsync(messageId, handlerType, termA, "A threw after losing its lease", ct).ConfigureAwait(false))
			.ShouldBeFalse("A's lease had lapsed, so its failure must not be recorded against B's entry");

		var entry = await store.GetEntryAsync(messageId, handlerType, ct).ConfigureAwait(false);
		entry.ShouldNotBeNull();
		entry.Status.ShouldBe(InboxStatus.Processing, "B is still processing; A must not have marked it Failed");
	}

	// LIVENESS — the fence must not block the caller it belongs to. Run against the REAL round-trip, which
	// is where a lossy timestamp encoding would surface as every finalization silently failing closed.
	[Fact]
	public async Task CompleteUnderALiveTerm()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		var messageId = $"msg-{Guid.NewGuid():N}";
		var handlerType = $"handler-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		var term = (await store.TryAcquireLeaseAsync(messageId, handlerType, LongLease, ct).ConfigureAwait(false))
			.ShouldNotBeNull();

		(await store.CompleteAsync(messageId, handlerType, term, ct).ConfigureAwait(false)).ShouldBeTrue(
			"the holder of a live term must be able to finalize — if this is RED the term does not survive "
			+ "the round-trip through timestamptz and every finalization fails closed");

		(await store.IsProcessedAsync(messageId, handlerType, ct).ConfigureAwait(false)).ShouldBeTrue();
	}

	// LIVENESS — the failure path, and the term being cleared so a redelivery is immediately re-admittable.
	[Fact]
	public async Task FailUnderALiveTermAndLeaveTheEntryReAdmittable()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		var messageId = $"msg-{Guid.NewGuid():N}";
		var handlerType = $"handler-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		var term = (await store.TryAcquireLeaseAsync(messageId, handlerType, LongLease, ct).ConfigureAwait(false))
			.ShouldNotBeNull();

		(await store.FailAsync(messageId, handlerType, term, "handler failed", ct).ConfigureAwait(false)).ShouldBeTrue(
			"the holder of a live term must be able to record its own failure");

		// A failed entry has no holder, so a redelivery must be admitted WITHOUT waiting out the five-minute
		// lease. If FailAsync left the old term in place this would sit behind a live lease instead.
		(await store.TryAcquireLeaseAsync(messageId, handlerType, LongLease, ct).ConfigureAwait(false))
			.ShouldNotBeNull("a Failed entry carries no holder, so a redelivery must be admitted immediately");
	}

	// LIVENESS — the arm that catches a fence which simply refuses everything.
	[Fact]
	public async Task StillLetTheReclaimingCallerFinalizeAfterTheLapsedOneIsRefused()
	{
		var (store, messageId, handlerType, termA, termB) = await LapsedReclaimAsync().ConfigureAwait(false);
		var ct = CancellationToken.None;

		(await store.CompleteAsync(messageId, handlerType, termA, ct).ConfigureAwait(false)).ShouldBeFalse();

		(await store.CompleteAsync(messageId, handlerType, termB, ct).ConfigureAwait(false)).ShouldBeTrue(
			"the live holder must still finalize after the lapsed caller was fenced out");
		(await store.IsProcessedAsync(messageId, handlerType, ct).ConfigureAwait(false)).ShouldBeTrue();
	}

	// MONOTONICITY, measured on the server clock rather than inspected in the source.
	//
	// The design rests on the reclaimed term being STRICTLY greater than the one it displaced: reclaim
	// admits only when the recorded expiry is strictly earlier than now(), and the replacement is now()
	// plus a non-negative duration. Relaxing that one comparison to <= would let a reclaim reissue the
	// same term, and the fence would stop discriminating without failing.
	[Fact]
	public async Task IssueAStrictlyGreaterTermToTheReclaimingCaller()
	{
		var (_, _, _, termA, termB) = await LapsedReclaimAsync().ConfigureAwait(false);

		termB.ShouldNotBe(termA, "a reclaim that reissued the same term would fence nothing");

		// The terms are round-trip encodings of timestamps, so the ordering is checkable here rather than
		// merely asserted in prose.
		var a = DateTimeOffset.Parse(termA.Value, System.Globalization.CultureInfo.InvariantCulture,
			System.Globalization.DateTimeStyles.RoundtripKind);
		var b = DateTimeOffset.Parse(termB.Value, System.Globalization.CultureInfo.InvariantCulture,
			System.Globalization.DateTimeStyles.RoundtripKind);

		b.ShouldBeGreaterThan(a,
			"the reclaimed term must be STRICTLY greater than the one it displaced — that is what makes the "
			+ "value an identity rather than merely a deadline");
	}

	private async Task<PostgresInboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available — the real-infra fencing lock is never skipped.");

		await EnsureTableAsync().ConfigureAwait(false);

		var connectionString = _fixture.ConnectionString;
		var options = new PostgresInboxOptions
		{
			ConnectionString = connectionString,
			SchemaName = "public",
			TableName = TableName,
		};

		return new PostgresInboxStore(
			() => new NpgsqlConnection(connectionString),
			options,
			NullLogger<PostgresInboxStore>.Instance,
			SingleTenantTestContext.Instance,
			Options.Create(new TenantContextOptions()));
	}

	private async Task EnsureTableAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// Mirrors the shipped Postgres inbox schema. lease_expires_at is the term column — no schema change
		// was needed for fencing, because the store already wrote a server-side expiry here.
		const string sql = $"""
			CREATE TABLE IF NOT EXISTS public.{TableName} (
				message_id VARCHAR(255) NOT NULL,
				handler_type VARCHAR(255) NOT NULL,
				message_type VARCHAR(255) NOT NULL,
				payload BYTEA NOT NULL,
				metadata JSONB,
				received_at TIMESTAMPTZ NOT NULL,
				processed_at TIMESTAMPTZ,
				status INT NOT NULL,
				retry_count INT NOT NULL,
				correlation_id VARCHAR(255),
				source VARCHAR(255),
				last_error TEXT,
				last_attempt_at TIMESTAMPTZ,
				lease_expires_at TIMESTAMPTZ NULL,
				PRIMARY KEY (message_id, handler_type)
			);
			""";

		await using var command = new NpgsqlCommand(sql, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}
}
