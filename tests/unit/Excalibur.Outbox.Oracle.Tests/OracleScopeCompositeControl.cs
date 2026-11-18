// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text.Json;

using Dapper;
using Excalibur.Data;
using Excalibur.Dispatch;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;
using Shouldly;
using Tests.Shared.Helpers;

namespace Excalibur.Outbox.Oracle.Tests;

// No attributed cases: the existing conformance wrapper must await both real mechanisms.
internal sealed class OracleScopeCompositeControl(OracleOutboxStoreContainerFixture fixture, string prefix) : IAsyncDisposable
{
    private readonly ServiceProvider _services = new ServiceCollection().BuildServiceProvider();
    private readonly List<string> _created = [];
    private string Table => prefix;
    private string Fence => prefix + "_FENCE";

    internal static async Task VerifyAsync(OracleOutboxStoreContainerFixture fixture, ITestOutputHelper output)
    {
        fixture.DockerAvailable.ShouldBeTrue("this composite requires real Oracle");
        await fixture.EnsureInitializedAsync().ConfigureAwait(false);
        // Independent physical scopes: each proof starts with a genuinely new high-water row.
        await RunControlAsync(fixture, output, reclaim: true).ConfigureAwait(false);
        await RunControlAsync(fixture, output, reclaim: false).ConfigureAwait(false);
    }

    private static async Task RunControlAsync(OracleOutboxStoreContainerFixture fixture, ITestOutputHelper output, bool reclaim)
    {
        var control = new OracleScopeCompositeControl(fixture, "OC" + Guid.NewGuid().ToString("N").ToUpperInvariant());
        Exception? primary = null;
        try
        {
            await control.ProvisionAsync().ConfigureAwait(false);
            if (reclaim) { await control.VerifyReclaimAsync().ConfigureAwait(false); }
            else { await control.VerifyAtomicityAsync(output).ConfigureAwait(false); }
            output.WriteLine(reclaim
                ? "Executed Oracle scope reclaim: expired same-row receipt replaced, stale7 refused unchanged, current8 applied; per-document overwrite remains NA."
                : "Executed Oracle owned-scope atomicity: exact server blocker -> old7 -> successor8 authority chain, serialized commits, stale7 refusal and new8 liveness.");
        }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            try { await control.DisposeAsync().ConfigureAwait(false); }
            catch (Exception cleanup) when (primary is not null) { output.WriteLine("Secondary scope-control cleanup failure: " + cleanup); }
        }
    }

    private async Task ProvisionAsync()
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync(stop.Token).ConfigureAwait(false);
        // Apply the actual shipped units, including defaults, PKs and indexes, with a generated
        // collision-free identifier prefix. No copied schema or CTAS constraint omission.
        var units = ShippedSchemaScript.ReadOracleUnits("src/Excalibur/Excalibur.Outbox.Oracle/Scripts/001_CreateOutboxSchema.sql");
        _created.AddRange([prefix + "_DEAD_LETTERS", Fence, Table]);
        foreach (var unit in units)
        {
            var sql = unit.Replace("OUTBOX", prefix, StringComparison.Ordinal)
                .Replace("outbox", prefix, StringComparison.Ordinal);
            await ExecuteAsync(connection, sql, null, null, stop.Token).ConfigureAwait(false);
        }
    }

    private async Task VerifyReclaimAsync()
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var store = Store();
        await StageAsync(store, "target", false, stop.Token).ConfigureAwait(false);
        await StageAsync(store, "sentinel", true, stop.Token).ConfigureAwait(false);
        var first = (await store.GetUnsentMessagesAsync(1, 7, stop.Token).ConfigureAwait(false)).Single();
        first.Id.ShouldBe("target");
        var oldReceipt = first.DispatcherId!;
        oldReceipt.ShouldNotBeNullOrWhiteSpace();
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync(stop.Token).ConfigureAwait(false);
        (await ExecuteAsync(connection, $"UPDATE {Table} SET dispatcher_timeout=SYSTIMESTAMP-NUMTODSINTERVAL(1,'SECOND') WHERE message_id=:Id",
            new { Id = "target" }, null, stop.Token).ConfigureAwait(false)).ShouldBe(1);
        var current = (await store.GetUnsentMessagesAsync(1, 8, stop.Token).ConfigureAwait(false)).Single();
        current.Id.ShouldBe(first.Id);
        current.DispatcherId.ShouldNotBeNullOrWhiteSpace();
        string.Equals(current.DispatcherId, oldReceipt, StringComparison.Ordinal).ShouldBeFalse();
        (await store.GetFencingHighWaterAsync(stop.Token).ConfigureAwait(false)).ShouldBe(8);
        var before = await SnapshotAsync(connection, "target", stop.Token).ConfigureAwait(false);
        before.ShouldNotBeNull();
        var sentinel = await SnapshotAsync(connection, "sentinel", stop.Token).ConfigureAwait(false);
        sentinel.ShouldNotBeNull();
        (await store.MarkSentAsync(first.Id, new OutboxWriteAuthority(7, oldReceipt), stop.Token).ConfigureAwait(false))
            .ShouldBe(OutboxCompletionOutcome.FenceRefused);
        (await SnapshotAsync(connection, "target", stop.Token).ConfigureAwait(false)).ShouldBe(before);
        (await SnapshotAsync(connection, "sentinel", stop.Token).ConfigureAwait(false)).ShouldBe(sentinel);
        (await store.MarkSentAsync(current.Id, new OutboxWriteAuthority(8, current.DispatcherId!), stop.Token).ConfigureAwait(false))
            .ShouldBe(OutboxCompletionOutcome.Applied);
        (await SnapshotAsync(connection, "target", stop.Token).ConfigureAwait(false)).ShouldBeNull();
        (await SnapshotAsync(connection, "sentinel", stop.Token).ConfigureAwait(false)).ShouldBe(sentinel);
    }

    private async Task VerifyAtomicityAsync(ITestOutputHelper output)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var oldTag = "old-" + Guid.NewGuid().ToString("N");
        var successorTag = "next-" + Guid.NewGuid().ToString("N");
        using var oldStore = Store(oldTag);
        using var successor = Store(successorTag);
        await StageAsync(oldStore, "target", false, stop.Token).ConfigureAwait(false);
        await StageAsync(oldStore, "sentinel", true, stop.Token).ConfigureAwait(false);
        var claim = (await oldStore.GetUnsentMessagesAsync(1, 7, stop.Token).ConfigureAwait(false)).Single();
        claim.Id.ShouldBe("target");
        claim.DispatcherId.ShouldNotBeNullOrWhiteSpace();
        await using var blocker = fixture.CreateConnection();
        await blocker.OpenAsync(stop.Token).ConfigureAwait(false);
        blocker.ClientId = "block-" + Guid.NewGuid().ToString("N");
        var blockSession = await OwnSessionAsync(blocker, stop.Token).ConfigureAwait(false);
        await using var observer = fixture.CreateConnection();
        await observer.OpenAsync(stop.Token).ConfigureAwait(false);
        var sentinel = await SnapshotAsync(observer, "sentinel", stop.Token).ConfigureAwait(false);
        sentinel.ShouldNotBeNull();
        await using var transaction = await blocker.BeginTransactionAsync(stop.Token).ConfigureAwait(false);
        await using (var rowLock = blocker.CreateCommand())
        {
            rowLock.Transaction = (OracleTransaction)transaction;
#pragma warning disable CA2100 // Table is generated by this helper; ID is bound.
            rowLock.CommandText = $"SELECT message_id FROM {Table} WHERE message_id=:Id FOR UPDATE";
#pragma warning restore CA2100
            rowLock.Parameters.Add("Id", OracleDbType.Varchar2).Value = "target";
            ((string)(await rowLock.ExecuteScalarAsync(stop.Token).ConfigureAwait(false))!).ShouldBe("target");
        }
        var old = oldStore.MarkSentAsync(claim.Id, new OutboxWriteAuthority(7, claim.DispatcherId!), stop.Token).AsTask();
        Task? advance = null;
        bool released = false;
        Exception? primary = null;
        try
        {
            var oldSession = await WaitForChainAsync(observer, oldTag, blockSession, stop.Token).ConfigureAwait(false);
            // Actual authority competitor, not a fabricated successor row acquisition.
            advance = successor.ResetFencingHighWaterAsync(8, false, stop.Token);
            _ = await WaitForChainAsync(observer, successorTag, oldSession, stop.Token).ConfigureAwait(false);
            var stillOld = await WaitForChainAsync(observer, oldTag, blockSession, stop.Token).ConfigureAwait(false);
            stillOld.ShouldBe(oldSession);
            (await oldStore.GetFencingHighWaterAsync(stop.Token).ConfigureAwait(false)).ShouldBe(7);
            (await ScalarAsync(observer, $"SELECT COUNT(*) FROM {Table} WHERE message_id='target' AND dispatcher_timeout>SYSTIMESTAMP+NUMTODSINTERVAL(120,'SECOND')", stop.Token).ConfigureAwait(false))
                .ShouldBe(1, "the old claim must still be live throughout the lock-chain barrier");
            await transaction.CommitAsync(stop.Token).ConfigureAwait(false);
            released = true;
            (await old.ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
            await advance.ConfigureAwait(false);
            (await successor.GetFencingHighWaterAsync(stop.Token).ConfigureAwait(false)).ShouldBe(8);
            (await SnapshotAsync(observer, "target", stop.Token).ConfigureAwait(false)).ShouldBeNull();
            (await oldStore.MarkSentAsync(claim.Id, new OutboxWriteAuthority(7, claim.DispatcherId!), stop.Token).ConfigureAwait(false))
                .ShouldBe(OutboxCompletionOutcome.FenceRefused);
            (await SnapshotAsync(observer, "sentinel", stop.Token).ConfigureAwait(false)).ShouldBe(sentinel);
            await StageAsync(successor, "healthy", false, stop.Token).ConfigureAwait(false);
            var healthy = (await successor.GetUnsentMessagesAsync(1, 8, stop.Token).ConfigureAwait(false)).Single();
            healthy.Id.ShouldBe("healthy");
            (await successor.MarkSentAsync(healthy.Id, new OutboxWriteAuthority(8, healthy.DispatcherId!), stop.Token).ConfigureAwait(false))
                .ShouldBe(OutboxCompletionOutcome.Applied);
            (await SnapshotAsync(observer, "healthy", stop.Token).ConfigureAwait(false)).ShouldBeNull();
            (await SnapshotAsync(observer, "sentinel", stop.Token).ConfigureAwait(false)).ShouldBe(sentinel);
        }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            // Each phase has its own bound and error boundary. A cancellation callback fault
            // must never bypass releasing the blocker or joining every owned operation.
            var cleanupFailures = new List<Exception>();
            try { await stop.CancelAsync().WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false); }
            catch (Exception error) { cleanupFailures.Add(error); }
            if (!released)
            {
                try { await transaction.RollbackAsync().WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false); }
                catch (Exception error) { cleanupFailures.Add(error); }
            }
            foreach (var owned in new[] { old, advance })
            {
                if (owned is null) { continue; }
                try { await owned.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false); }
                catch (OperationCanceledException) when (stop.IsCancellationRequested && owned.IsCanceled) { }
                catch (Exception error) { cleanupFailures.Add(error); }
            }
            if (primary is null && cleanupFailures.Count != 0)
            {
                ExceptionDispatchInfo.Capture(cleanupFailures[0]).Throw();
            }
            foreach (var secondary in cleanupFailures)
            {
                output.WriteLine("Secondary atomic-control cleanup failure: " + secondary);
            }
        }
    }

    private sealed record Session(int Sid, int Serial, int Instance);

    private static async Task<Session> OwnSessionAsync(OracleConnection connection, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT sid,serial#,TO_NUMBER(SYS_CONTEXT('USERENV','INSTANCE')) FROM v$session WHERE sid=TO_NUMBER(SYS_CONTEXT('USERENV','SID'))";
        await using var rows = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        (await rows.ReadAsync(token).ConfigureAwait(false)).ShouldBeTrue();
        var result = new Session(rows.GetInt32(0), rows.GetInt32(1), rows.GetInt32(2));
        (await rows.ReadAsync(token).ConfigureAwait(false)).ShouldBeFalse();
        return result;
    }

    private static async Task<Session> WaitForChainAsync(OracleConnection observer, string tag, Session blocker, CancellationToken token)
    {
        string last = "no session";
        while (true)
        {
            token.ThrowIfCancellationRequested();
            await using var command = observer.CreateCommand();
            command.BindByName = true;
            command.CommandText = "SELECT sid,serial#,TO_NUMBER(SYS_CONTEXT('USERENV','INSTANCE')),blocking_session,blocking_instance,blocking_session_status,event,row_wait_obj# FROM v$session WHERE client_identifier=:Tag AND status='ACTIVE' AND state='WAITING'";
            command.Parameters.Add("Tag", OracleDbType.Varchar2).Value = tag;
            Session? candidate = null;
            await using (var rows = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
            {
                if (await rows.ReadAsync(token).ConfigureAwait(false))
                {
                    var session = new Session(rows.GetInt32(0), rows.GetInt32(1), rows.GetInt32(2));
                    var blockedSid = rows.IsDBNull(3) ? -1 : rows.GetInt32(3);
                    var blockedInstance = rows.IsDBNull(4) ? -1 : rows.GetInt32(4);
                    var status = rows.IsDBNull(5) ? "" : rows.GetString(5);
                    var wait = rows.IsDBNull(6) ? "" : rows.GetString(6);
                    last = $"session={session}; blocker={blockedInstance}:{blockedSid}; status={status}; event={wait}; rowObject={rows.GetValue(7)}";
                    (await rows.ReadAsync(token).ConfigureAwait(false)).ShouldBeFalse("the test tag must identify exactly one owned worker");
                    if (blockedSid == blocker.Sid && blockedInstance == blocker.Instance
                        && session.Instance == blocker.Instance && status == "VALID" && wait == "enq: TX - row lock contention")
                    {
                        candidate = session;
                    }
                }
            }
            if (candidate is not null)
            {
                // Re-read after closing the first reader; reject recycled SID identity.
                await using var identity = observer.CreateCommand();
                identity.CommandText = "SELECT COUNT(*) FROM v$session WHERE sid=:Sid AND serial#=:Serial";
                identity.BindByName = true;
                identity.Parameters.Add("Sid", OracleDbType.Int32).Value = blocker.Sid;
                identity.Parameters.Add("Serial", OracleDbType.Int32).Value = blocker.Serial;
                Convert.ToInt32(await identity.ExecuteScalarAsync(token).ConfigureAwait(false), CultureInfo.InvariantCulture).ShouldBe(1);
                return candidate;
            }
            try
            {
                await Task.Delay(25, token).ConfigureAwait(false); // delay-ok: Poll the exact owned Oracle lock chain under the existing cancellation bound before proceeding.
            }
            catch (OperationCanceledException error) when (token.IsCancellationRequested)
            { throw new OperationCanceledException("Exact Oracle lock chain not observed: " + last, error, token); }
        }
    }

    private async Task<string?> SnapshotAsync(OracleConnection connection, string id, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Generated table name; exact row ID is bound.
        command.CommandText = $"SELECT * FROM {Table} WHERE message_id=:Id";
#pragma warning restore CA2100
        command.Parameters.Add("Id", OracleDbType.Varchar2).Value = id;
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        if (!await reader.ReadAsync(token).ConfigureAwait(false)) { return null; }
        var values = new List<string?>();
        for (var index = 0; index < reader.FieldCount; index++)
        {
            if (await reader.IsDBNullAsync(index, token).ConfigureAwait(false)) { values.Add(null); continue; }
            var name = reader.GetName(index);
            values.Add(name is "OCCURRED_ON" or "DISPATCHER_TIMEOUT" or "NEXT_ATTEMPT_AT" or "SCHEDULED_AT"
                ? reader.GetDateTimeOffset(index).ToString("O", CultureInfo.InvariantCulture)
                : name == "MESSAGE_BODY" ? Convert.ToHexString(await reader.GetFieldValueAsync<byte[]>(index, token).ConfigureAwait(false))
                : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture));
        }
        (await reader.ReadAsync(token).ConfigureAwait(false)).ShouldBeFalse();
        return JsonSerializer.Serialize(values);
    }

    private static async Task StageAsync(OracleOutboxStore store, string id, bool scheduled, CancellationToken token) =>
        await store.StageMessageAsync(new OutboundMessage("type", [0, 255, 128, 1], "destination")
        { Id = id, TenantId = "scope-control", ScheduledAt = scheduled ? DateTimeOffset.UtcNow.AddYears(1) : null }, token).ConfigureAwait(false);

    private OracleOutboxStore Store(string? tag = null)
    {
        var db = A.Fake<IDb>();
        A.CallTo(() => db.Connection).ReturnsLazily(() => fixture.CreateConnection());
        var source = OracleWorkerConnectionSource.FromFactory(_services.GetRequiredService<IServiceScopeFactory>(), (_, _) =>
        {
            var connection = fixture.CreateConnection();
            if (tag is not null)
            {
                connection.StateChange += (_, change) => { if (change.CurrentState == ConnectionState.Open) { connection.ClientId = tag; } };
            }
            return ValueTask.FromResult(connection);
        });
        return new OracleOutboxStore(db, Options.Create(new OracleOutboxStoreOptions
        { OutboxTableName = Table, DeadLetterTableName = prefix + "_DEAD_LETTERS", FenceTableName = Fence, ReservationTimeout = 600 }),
            NullLogger<OracleOutboxStore>.Instance, workerConnections: source);
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Only generated test identifiers or shipped DDL are composed; all runtime row values are bound.")]
    private static Task<int> ExecuteAsync(OracleConnection connection, string sql, object? values, IDbTransaction? transaction, CancellationToken token) =>
        connection.ExecuteAsync(new CommandDefinition(sql, values, transaction, cancellationToken: token));

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "SQL contains only generated test identifiers and fixed literals.")]
    private static async Task<int> ScalarAsync(OracleConnection connection, string sql, CancellationToken token) =>
        await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, cancellationToken: token)).ConfigureAwait(false);

    public async ValueTask DisposeAsync()
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync(stop.Token).ConfigureAwait(false);
        Exception? failure = null;
        foreach (var table in _created)
        {
            try { await ExecuteAsync(connection, $"DROP TABLE {table} CASCADE CONSTRAINTS PURGE", null, null, stop.Token).ConfigureAwait(false); }
            catch (OracleException error) when (error.Number == 942) { }
            catch (Exception error) { failure ??= error; }
        }
        await _services.DisposeAsync().ConfigureAwait(false);
        if (failure is not null) { ExceptionDispatchInfo.Capture(failure).Throw(); }
    }
}
