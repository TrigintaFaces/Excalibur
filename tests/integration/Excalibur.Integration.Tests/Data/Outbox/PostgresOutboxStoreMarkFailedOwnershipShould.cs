// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Outbox.Postgres;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// 9u1s94 — independent (author≠impl) NON-SKIPPED real-Postgres regression lock: reporting a message failed
/// must NOT clear a reservation held by a <b>different</b> dispatcher. An unconditional unreserve lets a
/// stalled dispatcher steal the live lease of the dispatcher that has since claimed the message, so a third
/// dispatcher claims it while the second is still delivering — a double delivery.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why real Postgres.</b> The guard is a SQL predicate
/// (<c>AND (dispatcher_id IS NULL OR dispatcher_id = @DispatcherId)</c>). A mocked <c>IDb</c> returns what it
/// is told and cannot express "the UPDATE matched no row", so a unit test certifies the broken store as
/// working. This is why ~112k CI tests did not see the defect.
/// </para>
/// <para>
/// <b>Why the foreign lease is forged with raw SQL.</b> The store's dispatcher identity is
/// <c>private static</c> — one value per PROCESS (<c>dispatcher-{MachineName}-{ProcessId}</c>). Two store
/// instances inside this test host therefore present the SAME identity, so a second store cannot stand in for
/// a second dispatcher. The only honest way to represent a foreign lease in-process is to write it directly.
/// </para>
/// <para>
/// <b>Arms (testing-patterns §3).</b> SAFETY alone is satisfied by a guard that clears nobody — a predicate
/// matching no row passes every "the lease survived" assertion while silently discarding all failures. Each
/// safety arm is therefore paired with liveness, and with the <c>IS NULL</c> arm:
/// <list type="bullet">
/// <item>SAFETY — a foreign LIVE lease survives, and the failure is NOT recorded (the update matched no row).</item>
/// <item>LIVENESS (owner) — this dispatcher's own failure report still unreserves and still records.</item>
/// <item>LIVENESS (<c>IS NULL</c>) — a staged-but-never-reserved message still records its failure. This arm
/// is load-bearing, not defensive: staging and failing without ever claiming is a supported path, and a guard
/// written <c>dispatcher_id = @DispatcherId</c> ALONE passes both arms above while silently dropping every
/// failure on it.</item>
/// </list>
/// Both properties are asserted on <c>MarkFailedAsync</c> AND <c>MarkFailedWithBackoffAsync</c> — the guard
/// exists on both request paths, and a fix applied to one is the classic half-wire.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> RED against the pre-fix impl (unconditional unreserve): the safety arms find
/// <c>dispatcher_id</c> cleared to <see langword="null"/> and the error recorded — the lease was stolen.
/// GREEN on the fix. The owner and <c>IS NULL</c> arms are GREEN on both, and exist to prove the guard did
/// not simply stop working.
/// </para>
/// </remarks>
[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Postgres")]
[Trait("Component", "Core")]
public sealed class PostgresOutboxStoreMarkFailedOwnershipShould : IClassFixture<PostgresOutboxStoreContainerFixture>
{
	/// <summary>A dispatcher identity that is deliberately NOT this process's own.</summary>
	private const string ForeignDispatcherId = "dispatcher-other-host-99999";

	private readonly PostgresOutboxStoreContainerFixture _fixture;

	public PostgresOutboxStoreMarkFailedOwnershipShould(PostgresOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// SAFETY: <c>MarkFailedAsync</c> against a message held by ANOTHER dispatcher must be a no-op — the
	/// foreign lease survives and the failure is not recorded.
	/// </summary>
	[Fact]
	public async Task NotClearAForeignLiveLease_OnMarkFailed()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		const string messageId = "9u1s94-markfailed-foreign-lease";
		await StageAsync(store, messageId).ConfigureAwait(false);
		await ForgeForeignLeaseAsync(messageId).ConfigureAwait(false);

		await store.MarkFailedAsync(messageId, "transient error", 1, CancellationToken.None).ConfigureAwait(false);

		(await DispatcherIdAsync(messageId).ConfigureAwait(false)).ShouldBe(
			ForeignDispatcherId,
			"MarkFailedAsync must not clear a reservation held by a DIFFERENT dispatcher. The lease was stolen: "
			+ "the holder is still delivering, and the message is now claimable by a third dispatcher — a double "
			+ "delivery. The update must be guarded by (dispatcher_id IS NULL OR dispatcher_id = @DispatcherId).");
		(await ErrorMessageAsync(messageId).ConfigureAwait(false)).ShouldBeNull(
			"a failure reported against someone else's live lease must match no row at all — recording the error "
			+ "while leaving the lease would mean the guard covered only part of the UPDATE.");
	}

	/// <summary>
	/// SAFETY: the same guarantee on the backoff path. A fix applied to one request and not the other is the
	/// half-wire this arm exists to catch.
	/// </summary>
	[Fact]
	public async Task NotClearAForeignLiveLease_OnMarkFailedWithBackoff()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		const string messageId = "9u1s94-backoff-foreign-lease";
		await StageAsync(store, messageId).ConfigureAwait(false);
		await ForgeForeignLeaseAsync(messageId).ConfigureAwait(false);

		await store.MarkFailedWithBackoffAsync(
			messageId, "transient error", 1, DateTimeOffset.UtcNow.AddMinutes(5), CancellationToken.None)
			.ConfigureAwait(false);

		(await DispatcherIdAsync(messageId).ConfigureAwait(false)).ShouldBe(
			ForeignDispatcherId,
			"MarkFailedWithBackoffAsync must carry the same reservation guard as MarkFailedAsync: reporting a "
			+ "failure against another dispatcher's live lease must not unreserve it (double delivery).");
		(await NextAttemptAtAsync(messageId).ConfigureAwait(false)).ShouldBeNull(
			"the backoff must match no row when the lease is foreign — rescheduling someone else's in-flight "
			+ "message is the same theft as clearing its lease.");
	}

	/// <summary>
	/// LIVENESS: the owner's own failure report still unreserves and still records. A guard that matched
	/// nobody would pass every safety arm above while breaking retry entirely.
	/// </summary>
	[Fact]
	public async Task StillUnreserveAndRecord_WhenTheOwnerReportsTheFailure_OnMarkFailedWithBackoff()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		const string messageId = "9u1s94-backoff-owner-liveness";
		await StageAsync(store, messageId).ConfigureAwait(false);

		// Claim through the store: the row is now reserved by THIS process's dispatcher identity.
		var claim = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		claim.ShouldContain(m => m.Id == messageId, "the claim must reserve the staged message");
		(await DispatcherIdAsync(messageId).ConfigureAwait(false)).ShouldNotBeNull(
			"the claim must have set dispatcher_id — otherwise the owner arm proves nothing");

		var nextAttempt = DateTimeOffset.UtcNow.AddMinutes(5);
		await store.MarkFailedWithBackoffAsync(messageId, "transient error", 1, nextAttempt, CancellationToken.None)
			.ConfigureAwait(false);

		(await DispatcherIdAsync(messageId).ConfigureAwait(false)).ShouldBeNull(
			"the guard's dispatcher_id = @DispatcherId arm must recognise this caller as the lease holder and "
			+ "unreserve the message, so it returns to the pool for retry. If this is non-null the guard rejects "
			+ "the owner and no message can ever be retried through the backoff path.");
		(await NextAttemptAtAsync(messageId).ConfigureAwait(false)).ShouldNotBeNull(
			"the owner's backoff must record next_attempt_at — a guard that unreserves without rescheduling "
			+ "would re-deliver immediately, defeating the backoff.");
	}

	/// <summary>
	/// LIVENESS (<c>IS NULL</c> arm): a staged-but-never-reserved message still records its failure. Staging
	/// and failing without ever claiming is a supported path.
	/// </summary>
	/// <remarks>
	/// This is the arm that separates the guard the implementer wrote from the guard a reasonable person would
	/// write. <c>AND dispatcher_id = @DispatcherId</c> — without the <c>IS NULL</c> disjunct — passes the
	/// foreign-lease safety arms AND the owner-liveness arm, and silently discards every failure reported on a
	/// message that was never claimed.
	/// </remarks>
	[Fact]
	public async Task StillRecordTheFailure_WhenTheMessageWasNeverReserved_OnBothPaths()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);

		const string failedId = "9u1s94-never-reserved-markfailed";
		await StageAsync(store, failedId).ConfigureAwait(false);
		(await DispatcherIdAsync(failedId).ConfigureAwait(false)).ShouldBeNull(
			"the staged message must be unreserved — this arm's whole premise is dispatcher_id IS NULL");

		await store.MarkFailedAsync(failedId, "never-claimed failure", 2, CancellationToken.None).ConfigureAwait(false);

		(await ErrorMessageAsync(failedId).ConfigureAwait(false)).ShouldBe(
			"never-claimed failure",
			"MarkFailedAsync must still record a failure on a message nobody holds. The reservation guard's "
			+ "IS NULL arm is load-bearing: without it, staging then failing (a supported path) silently "
			+ "discards the failure and the message never dead-letters.");

		const string backoffId = "9u1s94-never-reserved-backoff";
		await StageAsync(store, backoffId).ConfigureAwait(false);

		await store.MarkFailedWithBackoffAsync(
			backoffId, "never-claimed failure", 2, DateTimeOffset.UtcNow.AddMinutes(5), CancellationToken.None)
			.ConfigureAwait(false);

		(await NextAttemptAtAsync(backoffId).ConfigureAwait(false)).ShouldNotBeNull(
			"MarkFailedWithBackoffAsync must still reschedule a message nobody holds — the IS NULL arm applies "
			+ "identically on the backoff path.");
	}

	private static async Task StageAsync(IOutboxStore store, string messageId) =>
		await store.StageMessageAsync(
			new OutboundMessage { Id = messageId, MessageType = "T", Payload = [1], Destination = "dest" },
			CancellationToken.None).ConfigureAwait(false);

	private async Task<PostgresOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available — the reservation-guard lock is real-infra and never skipped: "
			+ "a mocked IDb cannot express 'the UPDATE matched no row', which is the entire property.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var db = A.Fake<IDb>();
		_ = A.CallTo(() => db.Connection).ReturnsLazily(() => _fixture.CreateConnection());

		var options = Options.Create(new PostgresOutboxStoreOptions
		{
			SchemaName = _fixture.SchemaName,
			OutboxTableName = _fixture.OutboxTableName,
			DeadLetterTableName = _fixture.DeadLetterTableName,
			// Long enough that no lease can expire mid-test: a reservation that lapsed on its own would make
			// the safety arms pass for the wrong reason.
			ReservationTimeout = 300,
			MaxAttempts = 3,
		});

		return new PostgresOutboxStore(db, options, NullLogger<PostgresOutboxStore>.Instance);
	}

	/// <summary>
	/// Writes a LIVE reservation held by a different dispatcher. Raw SQL is the only honest representation:
	/// the store's dispatcher identity is per-process, so no second store instance in this host can hold a
	/// genuinely foreign lease.
	/// </summary>
	private async Task ForgeForeignLeaseAsync(string messageId)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);
		var affected = await connection.ExecuteAsync(
			$"""
			UPDATE {_fixture.SchemaName}.{_fixture.OutboxTableName}
			   SET dispatcher_id = @Dispatcher,
			       dispatcher_timeout = NOW() + INTERVAL '5 minutes'
			 WHERE message_id = @Id
			""",
			new { Id = messageId, Dispatcher = ForeignDispatcherId }).ConfigureAwait(false);

		affected.ShouldBe(1, "the foreign lease must actually be written, or the safety arm asserts nothing");
	}

	private async Task<string?> DispatcherIdAsync(string messageId) =>
		await ScalarAsync<string?>("dispatcher_id", messageId).ConfigureAwait(false);

	private async Task<string?> ErrorMessageAsync(string messageId) =>
		await ScalarAsync<string?>("error_message", messageId).ConfigureAwait(false);

	private async Task<DateTimeOffset?> NextAttemptAtAsync(string messageId) =>
		await ScalarAsync<DateTimeOffset?>("next_attempt_at", messageId).ConfigureAwait(false);

	private async Task<T?> ScalarAsync<T>(string column, string messageId)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);
		return await connection.ExecuteScalarAsync<T?>(
			$"SELECT {column} FROM {_fixture.SchemaName}.{_fixture.OutboxTableName} WHERE message_id = @Id",
			new { Id = messageId }).ConfigureAwait(false);
	}
}
