// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;
using Excalibur.Dispatch;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

#pragma warning disable CA1812 // Instantiated by xUnit.

namespace Excalibur.Outbox.Oracle.Tests;

/// <summary>
/// bd-2mtb74 — the Oracle <c>MarkFailedAsync</c> drain-path regression lock (author≠impl). On Oracle the
/// reserve stamps <c>dispatcher_id = "{DispatcherId}:{guid}"</c> (per-call claim token) but the mark-failed
/// guard tested a <b>bare</b> <c>= :DispatcherId</c> — so the reserving dispatcher failing its OWN in-flight
/// message matched <b>zero rows</b> and <c>MarkFailedAsync</c> was a <b>silent no-op</b> on the only drain
/// path: attempts never increment, <c>error_message</c> is never written, the floor is never stamped, the
/// lease is never freed, and R1/R2/R3 are all inert. The direct consequence is that a poison message never
/// reaches <c>MaxAttempts</c>, so it <b>never dead-letters</b> — the at-least-once termination guarantee is
/// broken, Oracle-only.
/// </summary>
/// <remarks>
/// <para>
/// This is a NON-SKIPPED real-infra lock (<c>verify-against-real-infra-not-mock</c>): the operational root
/// cause was precisely that the conformance R2-liveness arm which would have caught this was skip-gated and
/// never ran (no <c>gvenzl/oracle-free</c> container in the gate). It runs against a live Oracle container and
/// <c>DockerAvailable.ShouldBeTrue(...)</c> fails fast rather than skipping. It is RED on the pre-fix bare
/// guard and GREEN on the dispatcher-level exact-prefix guard (SA ruling on 2mtb74). It is mechanism-
/// independent: it asserts the emitted behaviour (the failure is recorded; the poison reaches the ceiling),
/// not the SQL form, so it holds for the exact-prefix guard and for any later claim-token-threading refactor.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
public sealed class OracleOutboxMarkFailedRecordsOnDrainPathShould : IClassFixture<OracleOutboxStoreContainerFixture>
{
	private const int MaxAttempts = 3;

	private readonly OracleOutboxStoreContainerFixture _fixture;

	/// <summary>Initializes a new instance of the <see cref="OracleOutboxMarkFailedRecordsOnDrainPathShould"/> class.</summary>
	/// <param name="fixture">The Oracle container fixture.</param>
	public OracleOutboxMarkFailedRecordsOnDrainPathShould(OracleOutboxStoreContainerFixture fixture) => _fixture = fixture;

	private async Task<OracleOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Oracle container must be available — this bd-2mtb74 real-infra regression lock is NEVER skipped. "
			+ "The operational root cause was a skip-gated Oracle arm that never ran; a skip here would re-open "
			+ "exactly that hole.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		// Consumer-default surface: an IDb whose Connection yields a fresh Oracle connection per access.
		var db = A.Fake<IDb>();
		_ = A.CallTo(() => db.Connection).ReturnsLazily(() => _fixture.CreateConnection());

		var options = Options.Create(new OracleOutboxStoreOptions
		{
			SchemaName = _fixture.SchemaName,
			OutboxTableName = _fixture.OutboxTableName,
			DeadLetterTableName = _fixture.DeadLetterTableName,
			ReservationTimeout = 300,
			MaxAttempts = MaxAttempts,
		});

		return new OracleOutboxStore(db, options, NullLogger<OracleOutboxStore>.Instance);
	}

	private static OutboundMessage NewMessage() => new("Test.Poison", "payload"u8.ToArray(), "test-queue");

	/// <summary>
	/// ARM 1 — the reserving dispatcher's OWN <c>MarkFailedAsync</c> IS recorded. RED on the pre-fix bare-guard
	/// no-op (the drain reserves under <c>"{DispatcherId}:{guid}"</c>, the mark guard tested bare
	/// <c>:DispatcherId</c> → zero rows → nothing written → invisible to <c>GetFailedMessages</c>).
	/// </summary>
	[Fact]
	public async Task RecordTheFailure_WhenTheReservingDispatcherMarksItsOwnMessageFailed()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
		try
		{
			var msg = NewMessage();
			await store.StageMessageAsync(msg, CancellationToken.None).ConfigureAwait(false);

			// Reserve under THIS store's own dispatcher id — the real drain path (stamps "{DispatcherId}:{guid}").
			(await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false))
				.ShouldContain(m => m.Id == msg.Id, "the freshly-staged message must be claimable — the drain reserves it here");

			// The reserving dispatcher fails its OWN in-flight message.
			await store.MarkFailedAsync(msg.Id, "boom", 1, CancellationToken.None).ConfigureAwait(false);

			var failed = await ((IOutboxStoreAdmin)store)
				.GetAllTenantsFailedMessagesAsync(100, null, 10, CancellationToken.None).ConfigureAwait(false);
			var reloaded = failed.FirstOrDefault(m => m.Id == msg.Id);

			_ = reloaded.ShouldNotBeNull(
				"the reserving dispatcher's own MarkFailedAsync must RECORD the failure — a silent no-op here means "
				+ "attempts never increment, error_message is never written, and the failure is invisible to "
				+ "GetFailedMessages (bd-2mtb74: the mark guard tested a bare DispatcherId against the reserve's "
				+ "'{DispatcherId}:{guid}' claim token → zero rows).");
			reloaded.LastError.ShouldBe("boom", "the error message must be persisted, not dropped by a no-op");
			reloaded.RetryCount.ShouldBe(1, "the attempt count must increment, not stay 0");
		}
		finally
		{
			await _fixture.CleanupTableAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// ARM 2 — a POISON message's attempts increment to the retry ceiling, so it becomes retry-exhausted /
	/// dead-letter-eligible and eventually TERMINATES. RED on the no-op (attempts stay 0 → the message never
	/// reaches <c>MaxAttempts</c> → it re-claims forever and never dead-letters — the broken at-least-once
	/// termination guarantee this contract exists to hold).
	/// </summary>
	[Fact]
	public async Task DriveAPoisonMessageToTheRetryCeiling_SoItBecomesDeadLetterEligible()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
		try
		{
			var msg = NewMessage();
			await store.StageMessageAsync(msg, CancellationToken.None).ConfigureAwait(false);

			// Reserve under this store's own dispatcher id (the drain path the bug lived on), then fail at the
			// retry ceiling. On the fixed guard this records at attempts == MaxAttempts; on the no-op nothing is.
			(await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false))
				.ShouldContain(m => m.Id == msg.Id);
			await store.MarkFailedAsync(msg.Id, "poison", MaxAttempts, CancellationToken.None).ConfigureAwait(false);

			var admin = (IOutboxStoreAdmin)store;

			// The failure is recorded with attempts AT the retry ceiling — RED on the no-op (never recorded →
			// attempts stuck at 0 → the poison never reaches MaxAttempts → the processor's DLQ ceiling
			// (attempts >= MaxAttempts) is never reached → it re-claims forever and never dead-letters). This is
			// the store-level precondition the no-op breaks; the processor's actual DLQ move at the ceiling, and
			// that a dead-lettered message is never re-claimed, are covered by the shared conformance
			// DeadLettered_NeverReclaimed arm. GetFailedMessages(maxRetries) returns failed messages whose
			// attempts are BETWEEN 1 AND maxRetries (inclusive), so the maxRetries == MaxAttempts view includes it.
			var atCeiling = await admin.GetAllTenantsFailedMessagesAsync(MaxAttempts, null, 10, CancellationToken.None).ConfigureAwait(false);
			var reloaded = atCeiling.FirstOrDefault(m => m.Id == msg.Id);
			_ = reloaded.ShouldNotBeNull(
				"a poison message's attempts must be persisted so it can reach MaxAttempts and dead-letter — RED on "
				+ "the no-op, where attempts stay 0, the message is never recorded, and it re-claims forever.");
			reloaded.RetryCount.ShouldBe(
				MaxAttempts,
				"attempts must reach MaxAttempts so the processor's DLQ ceiling (attempts >= MaxAttempts) is "
				+ "reachable; a stuck-at-0 count means the poison never terminates.");
		}
		finally
		{
			await _fixture.CleanupTableAsync().ConfigureAwait(false);
		}
	}
}
