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
/// g3do61 — independent (author≠impl) NON-SKIPPED real-Postgres regression lock: a caller may supply a
/// <see cref="DateTimeOffset"/> in ANY offset. Npgsql writes a <c>DateTimeOffset</c> to <c>timestamptz</c>
/// only when its offset is zero and REJECTS any other outright, so a message scheduled with
/// <c>DateTimeOffset.Now</c> on a host east or west of UTC never reaches the outbox at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>Host offset is load-bearing.</b> These arms are vacuous on a UTC host: <c>DateTimeOffset.Now</c> would
/// carry offset zero and the pre-fix code would accept it. Each arm therefore asserts a NON-ZERO local offset
/// first and fails loudly rather than passing for the wrong reason. This is also why the whole defect class is
/// invisible to CI — every existing test uses <c>UtcNow</c>, i.e. offset zero, on the one path where offset is
/// the entire variable.
/// </para>
/// <para>
/// <b>Why real Postgres.</b> The rejection is Npgsql's, at the wire. A mocked <c>IDb</c> accepts any parameter
/// and reports success, certifying a store that cannot stage a message in production.
/// </para>
/// <para>
/// <b>Arms (testing-patterns §3).</b> "It did not throw" is satisfied by a store that silently writes the
/// wrong instant — a scheduled message firing five hours early is a worse defect than one that fails loudly.
/// Every safety arm is therefore paired with an instant-preservation arm.
/// <list type="bullet">
/// <item>SAFETY — staging with a non-zero-offset instant does not throw (the P0 verbatim: it "never stages").</item>
/// <item>LIVENESS — the stored instant is the SAME moment in time, not a coerced or shifted one.</item>
/// <item>Both, again, on the backoff path (<c>next_attempt_at</c>).</item>
/// </list>
/// </para>
/// <para>
/// <b>Non-vacuity.</b> RED against the pre-fix impl: Npgsql throws
/// <c>ArgumentException: Cannot write DateTimeOffset with Offset=-05:00:00 to PostgreSQL type 'timestamp with
/// time zone'</c>, so the safety arms fail at the call. GREEN once the parameter normalises to UTC.
/// <b>Not</b> asserted on <c>MarkFailedAsync</c>: that path writes no timestamp
/// (<c>SetOutboxMessageFailed</c> sets attempts/error/dispatcher columns only), so an arm there would bind a
/// mechanism that does not exist.
/// </para>
/// </remarks>
[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Postgres")]
[Trait("Component", "Core")]
public sealed class PostgresOutboxStoreTimestampOffsetShould : IClassFixture<PostgresOutboxStoreContainerFixture>
{
	private readonly PostgresOutboxStoreContainerFixture _fixture;

	public PostgresOutboxStoreTimestampOffsetShould(PostgresOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// SAFETY + LIVENESS on the staging path — the P0 itself. A message scheduled with a non-zero-offset
	/// instant must stage, and must stage at that exact instant.
	/// </summary>
	[Fact]
	public async Task StageAMessageScheduledWithANonZeroOffsetInstant_PreservingTheInstant()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		const string messageId = "g3do61-stage-local-offset";

		// An instant carrying a non-zero offset. The pre-fix parameter hands it to Npgsql unchanged and
		// Npgsql rejects it: the message never stages.
		var scheduledAt = NonZeroOffsetInstant();

		await store.StageMessageAsync(
			new OutboundMessage
			{
				Id = messageId,
				MessageType = "T",
				Payload = [1],
				Destination = "dest",
				ScheduledAt = scheduledAt,
			},
			CancellationToken.None).ConfigureAwait(false);

		var stored = await ScheduledAtAsync(messageId).ConfigureAwait(false);

		stored.ShouldNotBeNull(
			"a message scheduled with DateTimeOffset.Now must reach the outbox. If staging threw, Npgsql "
			+ "rejected the non-zero offset outright — the parameter must normalise to UTC before binding.");
		stored.Value.ToUniversalTime().ShouldBe(
			scheduledAt.ToUniversalTime(),
			TimeSpan.FromMilliseconds(1),
			"the stored instant must be the SAME moment the caller supplied. Not throwing is not enough: a "
			+ "coerced or truncated instant fires the scheduled message at the wrong time, which is worse than "
			+ "a loud failure because nothing reports it.");
	}

	/// <summary>
	/// SAFETY + LIVENESS on the backoff path — <c>next_attempt_at</c> carries the same parameter and the same
	/// defect.
	/// </summary>
	[Fact]
	public async Task ScheduleABackoffWithANonZeroOffsetInstant_PreservingTheInstant()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		const string messageId = "g3do61-backoff-local-offset";
		await store.StageMessageAsync(
			new OutboundMessage { Id = messageId, MessageType = "T", Payload = [1], Destination = "dest" },
			CancellationToken.None).ConfigureAwait(false);

		// A retry scheduled in a non-zero offset — the ordinary shape of a backoff computed from local Now.
		var nextAttemptAt = NonZeroOffsetInstant().AddMinutes(5);

		await store.MarkFailedWithBackoffAsync(messageId, "transient error", 1, nextAttemptAt, CancellationToken.None)
			.ConfigureAwait(false);

		var stored = await NextAttemptAtAsync(messageId).ConfigureAwait(false);

		stored.ShouldNotBeNull(
			"a backoff scheduled with a non-zero-offset instant must be recorded. If this threw, the backoff "
			+ "path binds the raw DateTimeOffset and Npgsql rejected it — the retry is silently lost.");
		stored.Value.ToUniversalTime().ShouldBe(
			nextAttemptAt.ToUniversalTime(),
			TimeSpan.FromMilliseconds(1),
			"the recorded retry instant must be the moment the caller asked for; a shifted next_attempt_at "
			+ "re-delivers early (or strands the message), and nothing reports it.");
	}

	/// <summary>
	/// Produces the current instant carried in a deliberately non-zero offset.
	/// </summary>
	/// <remarks>
	/// The offset is the entire variable under test, and it is constructed here rather than taken from
	/// <c>DateTimeOffset.Now</c> so it does not depend on where the suite happens to run. Sourcing it from
	/// the host made the arms vacuous on a UTC machine — the pre-fix parameter accepts offset zero — and
	/// the class guarded that honestly by failing on such a host. That guard made the suite unrunnable on
	/// CI, which runs UTC: the arms could only ever fail there, so the defect they cover was unprotected
	/// in the one place it would have been caught.
	/// <para>
	/// <c>ToOffset</c> re-expresses the same instant in another offset, so the moment asserted on is
	/// unchanged and only the offset — the thing under test — differs.
	/// </para>
	/// </remarks>
	private static DateTimeOffset NonZeroOffsetInstant() =>
		DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-5));

	private async Task<PostgresOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available — the timestamptz offset lock is real-infra and never "
			+ "skipped: the rejection is Npgsql's, at the wire, and no mock can express it.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var db = A.Fake<IDb>();
		_ = A.CallTo(() => db.Connection).ReturnsLazily(() => _fixture.CreateConnection());

		var options = Options.Create(new PostgresOutboxStoreOptions
		{
			SchemaName = _fixture.SchemaName,
			OutboxTableName = _fixture.OutboxTableName,
			DeadLetterTableName = _fixture.DeadLetterTableName,
			ReservationTimeout = 300,
			MaxAttempts = 3,
		});

		return new PostgresOutboxStore(db, options, NullLogger<PostgresOutboxStore>.Instance);
	}

	private async Task<DateTimeOffset?> ScheduledAtAsync(string messageId) =>
		await ScalarAsync<DateTimeOffset?>("scheduled_at", messageId).ConfigureAwait(false);

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
