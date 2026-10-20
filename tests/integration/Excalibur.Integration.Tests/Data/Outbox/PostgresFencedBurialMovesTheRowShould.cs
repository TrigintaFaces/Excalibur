// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Outbox.Postgres;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-PostgreSQL lock on the fenced burial: a message is in the outbox or in the dead-letter table, never
/// in both and never in neither, whichever way the fence rules.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why a dedicated file when the conformance kit already covers stale-token burial.</strong> The
/// shared arm asks whether a refused burial left the row alone, and it answers that by reading a status
/// column. <b>This store has no such column for burial.</b> It MOVES the row — one statement fences, copies
/// into the dead-letter table, then deletes from the outbox. A property phrased as "the status did not
/// change" cannot be stated about a row that is supposed to stop existing, so the generic arm is not weaker
/// here, it is inapplicable. The hazard this store actually has needs naming in this store's own terms.
/// </para>
/// <para>
/// <strong>The hazard, in the statement's own words.</strong> <c>FencedMarkMessageDeadLettered</c> records
/// that "the delete is gated on the copy, not merely on the fence, and that ordering is load-bearing" —
/// because a copy that happened without its delete leaves the message <b>buried and still deliverable</b>:
/// the dead-letter table says it is finished while the outbox continues to offer it. That state is worse
/// than either failure alone, and it is invisible to any check that looks at one table.
/// </para>
/// <para>
/// <strong>Both directions, because a refusal alone proves nothing.</strong> A statement that wrote nothing
/// ever would satisfy every safety assertion below. Each refusal is therefore paired with the burial the
/// fence must still permit, and each arm asserts the presence AND the absence rather than one of them.
/// </para>
/// </remarks>
[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresFencedBurialMovesTheRowShould(PostgresOutboxStoreContainerFixture fixture)
	: IClassFixture<PostgresOutboxStoreContainerFixture>
{
	private readonly PostgresOutboxStoreContainerFixture _fixture = fixture;

	private string ConnectionString => _fixture.ConnectionString;

	/// <summary>
	/// LIVENESS, and the move itself: an accepted tenure relocates the row rather than duplicating it.
	/// </summary>
	[Fact]
	public async Task RelocateTheRow_NotCopyIt_WhenTheFenceAcceptsTheTenure()
	{
		await EnsureReadyAsync().ConfigureAwait(false);
		var store = CreateStore();

		var messageId = await StageAndClaimAsync(store).ConfigureAwait(false);

		// The FIRST token on a fresh scope establishes the fence rather than being refused by it.
		var outcome = await store.MarkDeadLetteredAsync(
			messageId, "establishes the fence", 0, 5L, CancellationToken.None).ConfigureAwait(false);

		outcome.ShouldBe(
			OutboxCompletionOutcome.Applied,
			"the first token on a fresh scope establishes the fence, so this burial must be permitted");

		(await DeadLetterRowCountAsync(messageId).ConfigureAwait(false)).ShouldBe(
			1,
			"an accepted burial must copy the message into the dead-letter table — after the delete that row "
			+ "is the only surviving record of it, so a burial that copies nothing has destroyed the message.");

		(await OutboxRowCountAsync(messageId).ConfigureAwait(false)).ShouldBe(
			0,
			"an accepted burial must also DELETE the outbox row. A copy without its delete leaves the message "
			+ "buried and still deliverable at the same time: the dead-letter table reports it finished while "
			+ "the drain keeps offering it.");
	}

	/// <summary>
	/// SAFETY, and the arm the shared conformance kit cannot state for this store: a superseded tenure
	/// performs NEITHER half of the move.
	/// </summary>
	[Fact]
	public async Task CopyNothing_AndDeleteNothing_WhenTheFenceRefusesTheTenure()
	{
		await EnsureReadyAsync().ConfigureAwait(false);
		var store = CreateStore();

		// A live tenure at 20 establishes the high-water. Its own burial is the liveness half of this arm —
		// without it, the refusal below is equally satisfied by a statement that never writes at all.
		var establisher = await StageAndClaimAsync(store).ConfigureAwait(false);
		var live = await store.MarkDeadLetteredAsync(
			establisher, "by the live tenure", 0, 20L, CancellationToken.None).ConfigureAwait(false);
		live.ShouldBe(
			OutboxCompletionOutcome.Applied,
			"the high-water must be established by a burial that was itself permitted, or the refusal below "
			+ "is not attributable to the fence");

		var victim = await StageAndClaimAsync(store).ConfigureAwait(false);

		// A superseded tenure presents a LOWER token. The fence stands at 20 and returns a high-water strictly
		// greater than 8, so the copy is skipped and the delete — gated on the copy — is skipped with it.
		var superseded = await store.MarkDeadLetteredAsync(
			victim, "by a superseded tenure", 0, 8L, CancellationToken.None).ConfigureAwait(false);

		superseded.ShouldBe(
			OutboxCompletionOutcome.FenceRefused,
			"the fence stands at 20, so a tenure presenting 8 has been superseded and must not be able to "
			+ "bury anything");

		(await DeadLetterRowCountAsync(victim).ConfigureAwait(false)).ShouldBe(
			0,
			"a refused burial must copy NOTHING. If the copy lands while the delete is refused, the message is "
			+ "buried and still deliverable — the dead-letter table declares it finished and the outbox goes "
			+ "on offering it to the next drain.");

		(await OutboxRowCountAsync(victim).ConfigureAwait(false)).ShouldBe(
			1,
			"a refused burial must delete NOTHING. A delete that ran without its copy would destroy the message "
			+ "outright, leaving no record anywhere — the failure this statement's copy-then-delete ordering "
			+ "exists to make unreachable.");
	}

    [Theory]
    [InlineData(false, 0, 0, 0)]
    [InlineData(true, 0, 3, 3)]
    [InlineData(false, 4, 2, 4)]
    [InlineData(true, 2, 4, 4)]
    public async Task ArchiveTheAbsoluteObservedCount(bool fenced, int stored, int observed, int expected)
    {
        await EnsureReadyAsync().ConfigureAwait(false);
        using var store = CreateStore();
        var id = await StageAndClaimAsync(store).ConfigureAwait(false);
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.ExecuteAsync($"UPDATE {_fixture.SchemaName}.{_fixture.OutboxTableName} SET attempts = @stored WHERE message_id = @id", new { stored, id }).ConfigureAwait(false);
        if (fenced)
        {
            (await store.MarkDeadLetteredAsync(id, "counted", observed, 5L, CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
        }
        else
        {
            await store.MarkDeadLetteredAsync(id, "counted", observed, CancellationToken.None).ConfigureAwait(false);
        }
        (await connection.QuerySingleAsync<int>($"SELECT attempts FROM {_fixture.SchemaName}.{_fixture.DeadLetterTableName} WHERE message_id = @id", new { id }).ConfigureAwait(false)).ShouldBe(expected);
        await store.MarkDeadLetteredAsync(id, "duplicate", expected + 10, CancellationToken.None).ConfigureAwait(false);
        (await connection.QuerySingleAsync<int>($"SELECT attempts FROM {_fixture.SchemaName}.{_fixture.DeadLetterTableName} WHERE message_id = @id", new { id }).ConfigureAwait(false)).ShouldBe(expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectNegativeCountsWithoutAdvancingTheFence(bool fenced)
    {
        await EnsureReadyAsync().ConfigureAwait(false);
        using var store = CreateStore();
        var id = await StageAndClaimAsync(store).ConfigureAwait(false);
        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
        {
            if (fenced) { await store.MarkDeadLetteredAsync(id, "invalid", -1, 20L, CancellationToken.None).ConfigureAwait(false); }
            else { await store.MarkDeadLetteredAsync(id, "invalid", -1, CancellationToken.None).ConfigureAwait(false); }
        });
        (await OutboxRowCountAsync(id).ConfigureAwait(false)).ShouldBe(1);
        (await DeadLetterRowCountAsync(id).ConfigureAwait(false)).ShouldBe(0);
        (await store.MarkDeadLetteredAsync(id, "valid", 2, 5L, CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
    }

    [Theory]
    [InlineData(false, "insert")]
    [InlineData(true, "insert")]
    [InlineData(false, "delete")]
    [InlineData(true, "delete")]
    [InlineData(false, "throw-delete")]
    [InlineData(true, "throw-delete")]
    public async Task PreserveTheSourceOnSuppressedOrThrowingMutation(bool fenced, string fault)
    {
        await EnsureReadyAsync().ConfigureAwait(false);
        using var store = CreateStore();
        var id = await StageAndClaimAsync(store).ConfigureAwait(false);
        var suffix = Guid.NewGuid().ToString("N");
        var function = $"{_fixture.SchemaName}.fault_{suffix}";
        var table = $"{_fixture.SchemaName}." + (fault == "insert" ? _fixture.DeadLetterTableName : _fixture.OutboxTableName);
        var trigger = $"fault_{suffix}";
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        var body = fault == "throw-delete" ? "RAISE EXCEPTION 'terminal deletion fault';" : "RETURN NULL;";
        await connection.ExecuteAsync($"CREATE FUNCTION {function}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN {body} END $$; CREATE TRIGGER {trigger} BEFORE " + (fault == "insert" ? "INSERT" : "DELETE") + $" ON {table} FOR EACH ROW EXECUTE FUNCTION {function}()").ConfigureAwait(false);
        try
        {
            async Task RetireAsync()
            {
                if (fenced) { await store.MarkDeadLetteredAsync(id, "fault", 3, 5L, CancellationToken.None).ConfigureAwait(false); }
                else { await store.MarkDeadLetteredAsync(id, "fault", 3, CancellationToken.None).ConfigureAwait(false); }
            }
            if (fault == "insert") { await RetireAsync().ConfigureAwait(false); }
            else
            {
                var error = await Should.ThrowAsync<OperationFailedException>(RetireAsync);
                var providerError = error.InnerException.ShouldBeOfType<PostgresException>();
                providerError.SqlState.ShouldBe(fault == "delete" ? PostgresErrorCodes.DivisionByZero : PostgresErrorCodes.RaiseException);
            }
            (await OutboxRowCountAsync(id).ConfigureAwait(false)).ShouldBe(1);
            (await DeadLetterRowCountAsync(id).ConfigureAwait(false)).ShouldBe(0);
        }
        finally
        {
            await connection.ExecuteAsync($"DROP TRIGGER {trigger} ON {table}; DROP FUNCTION {function}()").ConfigureAwait(false);
        }
        (await store.MarkDeadLetteredAsync(id, "restored", 3, 5L, CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
    }

    [Theory]
    [InlineData("ordinary")]
    [InlineData("fenced")]
    [InlineData("administrative")]
    public async Task PreserveAlreadyDeliveredRows(string mode)
    {
        await EnsureReadyAsync().ConfigureAwait(false);
        using var store = CreateStore();
        var id = await StageAndClaimAsync(store).ConfigureAwait(false);
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await store.MarkSentAsync(id, CancellationToken.None).ConfigureAwait(false);
        if (mode == "ordinary") { await store.MarkDeadLetteredAsync(id, "terminal", 7, CancellationToken.None).ConfigureAwait(false); }
        else if (mode == "fenced") { (await store.MarkDeadLetteredAsync(id, "terminal", 7, 5L, CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.MessageNotFound); }
        else { await connection.ResolveAsync(new MoveOutboxMessageToDeadLetter(id, _fixture.SchemaName + "." + _fixture.OutboxTableName, _fixture.SchemaName + "." + _fixture.DeadLetterTableName, 30, CancellationToken.None)).ConfigureAwait(false); }
        (await OutboxRowCountAsync(id).ConfigureAwait(false)).ShouldBe(0);
        (await DeadLetterRowCountAsync(id).ConfigureAwait(false)).ShouldBe(0);
    }

    [Fact]
    public async Task AdministrativeMovesPreserveTheExistingAttemptCount()
    {
        await EnsureReadyAsync().ConfigureAwait(false);
        using var store = CreateStore();
        var id = await StageAndClaimAsync(store).ConfigureAwait(false);
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await connection.ExecuteAsync($"UPDATE {_fixture.SchemaName}.{_fixture.OutboxTableName} SET attempts = 3 WHERE message_id = @id", new { id }).ConfigureAwait(false);
        await connection.ResolveAsync(new MoveOutboxMessageToDeadLetter(id, _fixture.SchemaName + "." + _fixture.OutboxTableName, _fixture.SchemaName + "." + _fixture.DeadLetterTableName, 30, CancellationToken.None)).ConfigureAwait(false);
        (await connection.QuerySingleAsync<int>($"SELECT attempts FROM {_fixture.SchemaName}.{_fixture.DeadLetterTableName} WHERE message_id = @id", new { id }).ConfigureAwait(false)).ShouldBe(3);
    }

    [Fact]
    public async Task WaitForTheFenceBeforeLockingTheSource_AndCancelWithoutPartialMutation()
    {
        await EnsureReadyAsync().ConfigureAwait(false);
        using var store = CreateStore();
        var id = await StageAndClaimAsync(store).ConfigureAwait(false);
        var options = new PostgresOutboxStoreOptions { SchemaName = _fixture.SchemaName, OutboxTableName = _fixture.OutboxTableName };
        await using var blocker = new NpgsqlConnection(ConnectionString);
        await blocker.OpenAsync().ConfigureAwait(false);
        await using var transaction = await blocker.BeginTransactionAsync().ConfigureAwait(false);
        await blocker.ExecuteAsync($"INSERT INTO {options.QualifiedFenceTableName} (scope_key, high_water_token) VALUES (@scope, 1)", new { scope = options.QualifiedOutboxTableName }, transaction).ConfigureAwait(false);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var retirement = store.MarkDeadLetteredAsync(id, "blocked", 2, 5L, cancellation.Token).AsTask();
        await using var observer = new NpgsqlConnection(ConnectionString);
        await observer.OpenAsync().ConfigureAwait(false);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        var waiting = false;
        while (DateTime.UtcNow < deadline)
        {
            waiting = await observer.QuerySingleAsync<bool>("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND query LIKE '%WITH fence AS (%')").ConfigureAwait(false);
            if (waiting) { break; }
            await Task.Delay(25).ConfigureAwait(false);
        }
        try
        {
            waiting.ShouldBeTrue("the terminal command must demonstrably be waiting on the fence lock");
            await using var probeTransaction = await observer.BeginTransactionAsync().ConfigureAwait(false);
            (await observer.QuerySingleAsync<string>($"SELECT message_id FROM {options.QualifiedOutboxTableName} WHERE message_id = @id FOR UPDATE NOWAIT", new { id }, probeTransaction).ConfigureAwait(false)).ShouldBe(id);
            await probeTransaction.RollbackAsync().ConfigureAwait(false);
        }
        finally
        {
            await cancellation.CancelAsync().ConfigureAwait(false);
            await Should.ThrowAsync<OperationCanceledException>(async () => { await retirement.ConfigureAwait(false); });
            await transaction.RollbackAsync().ConfigureAwait(false);
        }
        (await OutboxRowCountAsync(id).ConfigureAwait(false)).ShouldBe(1);
        (await DeadLetterRowCountAsync(id).ConfigureAwait(false)).ShouldBe(0);
        (await store.MarkDeadLetteredAsync(id, "resumed", 2, 3L, CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestoreThePersistedFailureEvidenceOnReservation(bool fenced)
    {
        await EnsureReadyAsync().ConfigureAwait(false);
        using var store = CreateStore();
        var id = await StageAndClaimAsync(store).ConfigureAwait(false);
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await store.MarkFailedAsync(id, "recovered failure", 2, CancellationToken.None).ConfigureAwait(false);
        // Advance eligibility independently; the projection under test still runs the real claim SQL.
        await connection.ExecuteAsync($"UPDATE {_fixture.SchemaName}.{_fixture.OutboxTableName} SET next_attempt_at = NULL, dispatcher_id = NULL WHERE message_id = @id", new { id }).ConfigureAwait(false);
        var recovered = fenced
            ? await store.GetUnsentMessagesAsync(10, 5L, CancellationToken.None).ConfigureAwait(false)
            : await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
        var row = recovered.Single(item => item.Id == id);
        row.RetryCount.ShouldBe(2);
        row.LastError.ShouldBe("recovered failure");
    }

	/// <summary>Stages a message and claims it, returning the message id.</summary>
	/// <param name="store">The store under test.</param>
	/// <returns>The staged message's id, once the drain has claimed it.</returns>
	private static async Task<string> StageAndClaimAsync(PostgresOutboxStore store)
	{
		var message = new OutboundMessage("Test.MessageType", "test-payload"u8.ToArray(), "test-queue")
		{
			Id = Guid.NewGuid().ToString(),
		};

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Claimed through the store's own drain rather than by stamping the row by hand: the fenced burial is
		// scoped to a claim this store granted, and a test that forged that state would keep passing against a
		// store that had stopped granting it.
		var claimed = (await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false))
			.ToList();

		claimed.ShouldContain(
			m => m.Id == message.Id,
			"the staged message must be claimable, or the burial below is acting on a row no drain holds");

		return message.Id;
	}

	private async Task<long> OutboxRowCountAsync(string messageId) =>
		await CountAsync($"SELECT COUNT(*) FROM {_fixture.SchemaName}.{_fixture.OutboxTableName} WHERE message_id = @id", messageId)
			.ConfigureAwait(false);

	private async Task<long> DeadLetterRowCountAsync(string messageId) =>
		await CountAsync($"SELECT COUNT(*) FROM {_fixture.SchemaName}.{_fixture.DeadLetterTableName} WHERE message_id = @id", messageId)
			.ConfigureAwait(false);

	private async Task<long> CountAsync(string sql, string messageId)
	{
		await using var connection = new NpgsqlConnection(ConnectionString);
		await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);

#pragma warning disable CA2100 // table names come from the fixture, never from caller input
		await using var command = new NpgsqlCommand(sql, connection);
#pragma warning restore CA2100
		_ = command.Parameters.AddWithValue("@id", messageId);

		return Convert.ToInt64(
			await command.ExecuteScalarAsync(CancellationToken.None).ConfigureAwait(false),
			System.Globalization.CultureInfo.InvariantCulture);
	}

	private async Task EnsureReadyAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"PostgreSQL container must be available - this real-infra lock is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	private PostgresOutboxStore CreateStore()
	{
		var db = A.Fake<IDb>();
		_ = A.CallTo(() => db.Connection).Returns(new NpgsqlConnection(ConnectionString));

		return new PostgresOutboxStore(
			db,
			Options.Create(new PostgresOutboxStoreOptions
			{
				SchemaName = _fixture.SchemaName,
				OutboxTableName = _fixture.OutboxTableName,
				DeadLetterTableName = _fixture.DeadLetterTableName,
			}),
			NullLogger<PostgresOutboxStore>.Instance);
	}
}
