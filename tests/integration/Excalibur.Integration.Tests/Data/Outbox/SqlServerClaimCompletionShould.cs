// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;
using Excalibur.Dispatch;
using Excalibur.Outbox.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Tests.Shared.Helpers;
using Tests.Shared.Infrastructure;

namespace Excalibur.Integration.Tests.Data.Outbox;

[Collection(SqlServerOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerClaimCompletionShould(SqlServerOutboxStoreContainerFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectAllStaleSameProcessCompletionsAndAllowTheSuccessor(bool fenced)
    {
        await PrepareAsync().ConfigureAwait(false);
        var store = Store();
        var id = await StageAsync(store).ConfigureAwait(false);
        var first = (await ClaimAsync(store, fenced).ConfigureAwait(false)).Single();
        var originalIdentity = first.DispatcherId.ShouldNotBeNull();
        await ExpireAsync(id).ConfigureAwait(false);
        var second = (await ClaimAsync(store, fenced).ConfigureAwait(false)).Single();
        var successorIdentity = second.DispatcherId.ShouldNotBeNull();
        successorIdentity.ShouldNotBe(originalIdentity);
        first.DispatcherId.ShouldBe(originalIdentity, "later acquisition must not mutate previously returned evidence");

        if (fenced)
        {
            var stale = new OutboxWriteAuthority(7, originalIdentity);
            (await store.MarkSentAsync(id, stale, CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
            (await store.MarkFailedAsync(id, "stale", 9, null, stale, CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
            (await store.MarkFailedAsync(id, "stale scheduled", 9, DateTimeOffset.UtcNow.AddHours(1), stale, CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
            (await store.MarkDeadLetteredAsync(id, "stale", 9, stale, CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
        }
        else
        {
            (await store.MarkSentAsync(id, originalIdentity, CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
            (await store.MarkFailedAsync(id, "stale", 9, originalIdentity, CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
            (await store.MarkFailedWithBackoffAsync(id, "stale scheduled", 9, DateTimeOffset.UtcNow.AddHours(1), originalIdentity, CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
            (await store.MarkDeadLetteredAsync(id, "stale", 9, originalIdentity, CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
        }
        await using (var connection = fixture.CreateConnection())
        {
            (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.OutboxMessages WHERE Id=@Id AND LeasedBy=@Owner AND Status=0 AND RetryCount=0 AND LastError IS NULL AND NextAttemptAt IS NULL",
                new { Id = id, Owner = successorIdentity }).ConfigureAwait(false)).ShouldBe(1);
        }
        var applied = fenced
            ? await store.MarkSentAsync(id, new OutboxWriteAuthority(7, successorIdentity), CancellationToken.None).ConfigureAwait(false)
            : await store.MarkSentAsync(id, successorIdentity, CancellationToken.None).ConfigureAwait(false);
        applied.ShouldBe(OutboxCompletionOutcome.Applied);
        await using var verify = fixture.CreateConnection();
        (await verify.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.OutboxMessages WHERE Id=@Id AND Status=2 AND LeasedAt IS NULL AND LeasedBy IS NULL", new { Id = id }).ConfigureAwait(false)).ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefuseAnExpiredLeaseEvenBeforeAnotherAcquisition(bool fenced)
    {
        await PrepareAsync().ConfigureAwait(false);
        var store = Store();
        var id = await StageAsync(store).ConfigureAwait(false);
        var identity = (await ClaimAsync(store, fenced).ConfigureAwait(false)).Single().DispatcherId.ShouldNotBeNull();
        await ExpireAsync(id).ConfigureAwait(false);
        var outcome = fenced
            ? await store.MarkSentAsync(id, new OutboxWriteAuthority(7, identity), CancellationToken.None).ConfigureAwait(false)
            : await store.MarkSentAsync(id, identity, CancellationToken.None).ConfigureAwait(false);
        outcome.ShouldBe(OutboxCompletionOutcome.ClaimLost);
    }

    [Theory]
    [InlineData("OWNER:é:😀:claim")]
    [InlineData("owner:e:😀:claim")]
    [InlineData("owner:e\u0301:😀:claim")]
    [InlineData("owner:é:😀:claim ")]
    public async Task CompareAcquisitionBytesUnderAnInsensitiveColumnCollation(string wrongIdentity)
    {
        await PrepareAsync().ConfigureAwait(false);
        var table = "OrdinalClaim_" + Guid.NewGuid().ToString("N");
        await using var connection = fixture.CreateConnection();
        _ = await connection.ExecuteAsync($"""
            CREATE TABLE [dbo].[{table}] (Id nvarchar(255) NOT NULL PRIMARY KEY, Status int NOT NULL,
              LeasedAt datetimeoffset NULL, LeasedBy nvarchar(255) COLLATE Latin1_General_100_CI_AI NULL,
              SentAt datetimeoffset NULL, LastError nvarchar(max) NULL, FencingToken bigint NOT NULL DEFAULT 0);
            INSERT INTO [dbo].[{table}] (Id,Status,LeasedAt,LeasedBy)
              VALUES ('id',0,TODATETIMEOFFSET(SYSUTCDATETIME(),0),@Owner);
            """, new { Owner = "owner:é:😀:claim" }).ConfigureAwait(false);
        try
        {
            var store = Store(table);
            (await store.MarkSentAsync("id", wrongIdentity, CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
            (await store.MarkSentAsync("id", "owner:é:😀:claim", CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
        }
        finally { _ = await connection.ExecuteAsync($"DROP TABLE [dbo].[{table}]").ConfigureAwait(false); }
    }

    [Fact]
    public async Task DecideFenceBeforeMissingAndTerminalAndCommitValidRefusalAdvance()
    {
        await PrepareAsync().ConfigureAwait(false);
        var store = Store();
        var id = await StageAsync(store).ConfigureAwait(false);
        var identity = (await store.GetUnsentMessagesAsync(10, 7, CancellationToken.None).ConfigureAwait(false)).Single().DispatcherId.ShouldNotBeNull();
        (await store.MarkSentAsync(id, new OutboxWriteAuthority(7, identity), CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
        (await store.MarkSentAsync(id, new OutboxWriteAuthority(6, identity), CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.FenceRefused);
        (await store.MarkSentAsync("missing", new OutboxWriteAuthority(6, identity), CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.FenceRefused);
        (await store.MarkSentAsync(id, new OutboxWriteAuthority(8, identity), CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.AlreadyTerminal);
        (await store.MarkSentAsync("missing", new OutboxWriteAuthority(8, identity), CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.MessageNotFound);
        (await store.MarkSentAsync("missing", 7, CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.FenceRefused);
        await using var connection = fixture.CreateConnection();
        (await connection.ExecuteScalarAsync<long>("SELECT HighWaterToken FROM dbo.OutboxFence WHERE OutboxTable=@Scope", new { Scope = "[dbo].[OutboxMessages]" }).ConfigureAwait(false)).ShouldBe(8);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StampTheAcceptedNewerTokenOnBothLegacyAndStrictSent(bool strict)
    {
        await PrepareAsync().ConfigureAwait(false);
        var store = Store();
        var id = await StageAsync(store).ConfigureAwait(false);
        var identity = (await ClaimAsync(store, true).ConfigureAwait(false)).Single().DispatcherId.ShouldNotBeNull();
        var outcome = strict
            ? await store.MarkSentAsync(id, new OutboxWriteAuthority(8, identity), CancellationToken.None).ConfigureAwait(false)
            : await store.MarkSentAsync(id, 8, CancellationToken.None).ConfigureAwait(false);
        outcome.ShouldBe(OutboxCompletionOutcome.Applied);
        await using var connection = fixture.CreateConnection();
        (await connection.ExecuteScalarAsync<long>("SELECT FencingToken FROM dbo.OutboxMessages WHERE Id=@Id", new { Id = id }).ConfigureAwait(false)).ShouldBe(8);
        (await connection.ExecuteScalarAsync<long>("SELECT HighWaterToken FROM dbo.OutboxFence WHERE OutboxTable=@Scope", new { Scope = "[dbo].[OutboxMessages]" }).ConfigureAwait(false)).ShouldBe(8);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ApplyCurrentFailureWithMonotoneAttemptsAndServerFloor(bool fenced, bool scheduled)
    {
        await PrepareAsync().ConfigureAwait(false);
        var store = Store();
        var id = await StageAsync(store).ConfigureAwait(false);
        var identity = (await ClaimAsync(store, fenced).ConfigureAwait(false)).Single().DispatcherId.ShouldNotBeNull();
        await using var connection = fixture.CreateConnection();
        _ = await connection.ExecuteAsync("UPDATE dbo.OutboxMessages SET RetryCount=5 WHERE Id=@Id", new { Id = id }).ConfigureAwait(false);
        var before = await connection.ExecuteScalarAsync<DateTimeOffset>("SELECT TODATETIMEOFFSET(SYSUTCDATETIME(),0)").ConfigureAwait(false);
        var next = DateTimeOffset.UtcNow.AddMinutes(10);
        var outcome = fenced
            ? await store.MarkFailedAsync(id, "current failure", 2, scheduled ? next : null, new OutboxWriteAuthority(7, identity), CancellationToken.None).ConfigureAwait(false)
            : scheduled
                ? await store.MarkFailedWithBackoffAsync(id, "current failure", 2, next, identity, CancellationToken.None).ConfigureAwait(false)
                : await store.MarkFailedAsync(id, "current failure", 2, identity, CancellationToken.None).ConfigureAwait(false);
        outcome.ShouldBe(OutboxCompletionOutcome.Applied);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.OutboxMessages WHERE Id=@Id AND Status=3 AND RetryCount=5 AND LastError='current failure' AND LeasedAt IS NULL AND LeasedBy IS NULL AND NextAttemptAt>=DATEADD(SECOND,30,@Before)",
            new { Id = id, Before = before }).ConfigureAwait(false)).ShouldBe(1);
        if (scheduled)
        {
            (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.OutboxMessages WHERE Id=@Id AND NextAttemptAt>=DATEADD(SECOND,540,@Before)", new { Id = id, Before = before }).ConfigureAwait(false)).ShouldBe(1);
        }
        (await ClaimAsync(store, fenced).ConfigureAwait(false)).ShouldBeEmpty("a released lease must retain the failure visibility floor");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApplyCurrentDeadLetterWithoutLoweringAttemptsOrLeavingALease(bool fenced)
    {
        await PrepareAsync().ConfigureAwait(false);
        var store = Store();
        var id = await StageAsync(store).ConfigureAwait(false);
        var identity = (await ClaimAsync(store, fenced).ConfigureAwait(false)).Single().DispatcherId.ShouldNotBeNull();
        await using var connection = fixture.CreateConnection();
        _ = await connection.ExecuteAsync("UPDATE dbo.OutboxMessages SET RetryCount=5 WHERE Id=@Id", new { Id = id }).ConfigureAwait(false);
        var outcome = fenced
            ? await store.MarkDeadLetteredAsync(id, "poison", 2, new OutboxWriteAuthority(7, identity), CancellationToken.None).ConfigureAwait(false)
            : await store.MarkDeadLetteredAsync(id, "poison", 2, identity, CancellationToken.None).ConfigureAwait(false);
        outcome.ShouldBe(OutboxCompletionOutcome.Applied);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.OutboxMessages WHERE Id=@Id AND Status=5 AND RetryCount=5 AND LastError='poison' AND LeasedAt IS NULL AND LeasedBy IS NULL", new { Id = id }).ConfigureAwait(false)).ShouldBe(1);
        (await ClaimAsync(store, fenced).ConfigureAwait(false)).ShouldBeEmpty();
    }

    [Fact]
    public async Task BlockSuccessorAdvanceUntilTheActualStoreTransitionCommits()
    {
        await PrepareAsync().ConfigureAwait(false);
        var store = Store();
        var id = await StageAsync(store).ConfigureAwait(false);
        var identity = (await ClaimAsync(store, true).ConfigureAwait(false)).Single().DispatcherId.ShouldNotBeNull();
        await using var blocker = fixture.CreateConnection();
        await blocker.OpenAsync(CancellationToken.None).ConfigureAwait(false);
        await using var barrier = (SqlTransaction)await blocker.BeginTransactionAsync(CancellationToken.None).ConfigureAwait(false);
        _ = await blocker.ExecuteScalarAsync<string>("SELECT Id FROM dbo.OutboxMessages WITH (UPDLOCK,HOLDLOCK) WHERE Id=@Id", new { Id = id }, barrier).ConfigureAwait(false);
        var firstSession = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var successorSession = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = TrackedStore(firstSession).MarkSentAsync(id, new OutboxWriteAuthority(7, identity), CancellationToken.None).AsTask();
        Task<OutboxCompletionOutcome>? successor = null;
        var released = false;
        try
        {
            var oldId = await firstSession.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            (await WaitForBlockAsync(oldId, blocker.ServerProcessId).ConfigureAwait(false)).ShouldBeTrue("the actual old store operation must hold its fence while waiting for the row barrier");
            successor = TrackedStore(successorSession).MarkSentAsync("missing", new OutboxWriteAuthority(8, identity), CancellationToken.None).AsTask();
            var successorId = await successorSession.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            (await WaitForBlockAsync(successorId, oldId).ConfigureAwait(false)).ShouldBeTrue("the successor fence advance must wait behind the old operation's same transaction");
            await barrier.CommitAsync(CancellationToken.None).ConfigureAwait(false);
            released = true;
            (await old.ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
            (await successor.ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.MessageNotFound);
            await using var verify = fixture.CreateConnection();
            (await verify.ExecuteScalarAsync<long>("SELECT FencingToken FROM dbo.OutboxMessages WHERE Id=@Id", new { Id = id }).ConfigureAwait(false)).ShouldBe(7);
            (await verify.ExecuteScalarAsync<long>("SELECT HighWaterToken FROM dbo.OutboxFence WHERE OutboxTable=@Scope", new { Scope = "[dbo].[OutboxMessages]" }).ConfigureAwait(false)).ShouldBe(8);
        }
        finally
        {
            if (!released) { await barrier.RollbackAsync(CancellationToken.None).ConfigureAwait(false); }
            // Join admitted operations even when a barrier assertion fails.
            try { _ = await old.ConfigureAwait(false); } catch { }
            if (successor is not null) { try { _ = await successor.ConfigureAwait(false); } catch { } }
        }
    }

    private SqlServerOutboxStore TrackedStore(TaskCompletionSource<int> opened) => new(() =>
    {
        var connection = fixture.CreateConnection();
        connection.StateChange += (_, change) =>
        {
            if (change.CurrentState == System.Data.ConnectionState.Open) { _ = opened.TrySetResult(connection.ServerProcessId); }
        };
        return connection;
    }, new SqlServerOutboxOptions { ConnectionString = fixture.ConnectionString }, NullLogger<SqlServerOutboxStore>.Instance);

    private Task<bool> WaitForBlockAsync(int waiter, int owner) => WaitHelpers.WaitUntilAsync(async () =>
    {
        await using var connection = fixture.CreateConnection();
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.dm_exec_requests WHERE session_id=@Waiter AND blocking_session_id=@Owner", new { Waiter = waiter, Owner = owner }).ConfigureAwait(false) == 1;
    }, TimeSpan.FromSeconds(20), TimeSpan.FromMilliseconds(100), CancellationToken.None);

    private SqlServerOutboxStore Store(string table = "OutboxMessages") => new(Options.Create(new SqlServerOutboxOptions
    {
        ConnectionString = fixture.ConnectionString, ProcessorId = "owner:é:😀",
        Tables = { OutboxTableName = table }, Processing = { LeaseTimeoutSeconds = 120, FailureBackoffFloorSeconds = 30 },
    }), NullLogger<SqlServerOutboxStore>.Instance);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ClaimAndCompleteWithReadCommittedSnapshotEnabledOrDisabled(bool snapshot, bool fenced)
    {
        fixture.DockerAvailable.ShouldBeTrue("this control requires real SQL Server");
        var database = "ClaimIsolation_" + Guid.NewGuid().ToString("N");
        await using var admin = fixture.CreateConnection();
        _ = await admin.ExecuteAsync($"CREATE DATABASE [{database}]").ConfigureAwait(false);
        try
        {
            if (snapshot)
            {
                _ = await admin.ExecuteAsync($"ALTER DATABASE [{database}] SET READ_COMMITTED_SNAPSHOT ON").ConfigureAwait(false);
            }
            (await admin.ExecuteScalarAsync<bool>("SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name=@Name", new { Name = database }).ConfigureAwait(false)).ShouldBe(snapshot);
            var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString) { InitialCatalog = database }.ConnectionString;
            await using (var setup = new SqlConnection(connectionString))
            {
                foreach (var batch in ShippedSchemaScript.ReadSqlCmdBatches("src/Excalibur/Excalibur.Outbox.SqlServer/Scripts/001_CreateOutboxSchema.sql"))
                {
                    _ = await setup.ExecuteAsync(batch).ConfigureAwait(false);
                }
            }
            var store = new SqlServerOutboxStore(Options.Create(new SqlServerOutboxOptions { ConnectionString = connectionString }), NullLogger<SqlServerOutboxStore>.Instance);
            var id = await StageAsync(store).ConfigureAwait(false);
            var identity = (await ClaimAsync(store, fenced).ConfigureAwait(false)).Single().DispatcherId.ShouldNotBeNull();
            var outcome = fenced
                ? await store.MarkSentAsync(id, new OutboxWriteAuthority(7, identity), CancellationToken.None).ConfigureAwait(false)
                : await store.MarkSentAsync(id, identity, CancellationToken.None).ConfigureAwait(false);
            outcome.ShouldBe(OutboxCompletionOutcome.Applied);
        }
        finally
        {
            // Only this control's uniquely created database is affected; no shared session option changes.
            _ = await admin.ExecuteAsync($"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]").ConfigureAwait(false);
        }
    }

    private static async Task<string> StageAsync(SqlServerOutboxStore store)
    {
        var message = new OutboundMessage("test-type", new byte[] { 0, 255 }, "destination") { Id = Guid.NewGuid().ToString("N") };
        await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
        return message.Id;
    }

    private static ValueTask<IEnumerable<OutboundMessage>> ClaimAsync(SqlServerOutboxStore store, bool fenced) => fenced
        ? store.GetUnsentMessagesAsync(10, 7, CancellationToken.None)
        : store.GetUnsentMessagesAsync(10, CancellationToken.None);

    private async Task ExpireAsync(string id)
    {
        await using var connection = fixture.CreateConnection();
        _ = await connection.ExecuteAsync("UPDATE dbo.OutboxMessages SET LeasedAt=DATEADD(SECOND,-121,TODATETIMEOFFSET(SYSUTCDATETIME(),0)) WHERE Id=@Id", new { Id = id }).ConfigureAwait(false);
    }

    private async Task PrepareAsync()
    {
        fixture.DockerAvailable.ShouldBeTrue("real SQL controls must fail rather than skip when infrastructure is unavailable");
        await fixture.EnsureInitializedAsync().ConfigureAwait(false);
        await fixture.CleanupTableAsync().ConfigureAwait(false);
    }
}
