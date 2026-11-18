// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.ExceptionServices;
using Excalibur.Dispatch;
using Excalibur.Outbox;
using Oracle.ManagedDataAccess.Client;
using Shouldly;
using Xunit;

namespace Excalibur.Outbox.Oracle.Tests;

public sealed partial class OracleOutboxEnqueueTransformShould
{
    private static readonly string[] OwnedBlobTables = ["OUTBOX", "OUTBOX_DEAD_LETTERS"];
    [Fact]
    public async Task PreserveSixtyFourKibibytesThroughTheActualBinaryStageBinding()
    {
        await RunOwnedBlobAsync(async (harness, id, token) =>
        {
            var expected = Enumerable.Range(0, 65536).Select(index => (byte)(index % 251)).ToArray();
            await harness.Store.StageMessageAsync(BlobEnvelope(harness, id, expected), token);
            AssertBlob((await ReadOwnedBlobAsync(id, false, token)).Single(), expected);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreserveEmptyAndNonemptyScheduledBlobsWithoutSqlNull(bool empty)
    {
        await RunOwnedBlobAsync(async (harness, id, token) =>
        {
            byte[] expected = empty ? [] : [1, 0, 255];
            await harness.Raw.ScheduleMessageAsync(new OutboxMessage
            {
                MessageId = id, MessageType = "binary", MessageMetadata = "{}", MessageBody = expected,
                CreatedAt = DateTimeOffset.UtcNow, TenantId = "before-tenant", Destination = "binary-destination",
            }, DateTimeOffset.UtcNow.AddHours(1), token);
            AssertBlob((await ReadOwnedBlobAsync(id, false, token)).Single(), expected);
            var scheduled = (await harness.Raw.GetAllTenantsScheduledMessagesAsync(DateTimeOffset.UtcNow.AddHours(2), 100, token)).Single(message => message.Id == id);
            scheduled.Payload.ShouldBe(expected);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KeepAnEmptyBlobExternalStageTransactionBorrowed(bool commit)
    {
        await RunOwnedBlobAsync(async (harness, id, token) =>
        {
            await using var connection = fixture.CreateConnection(); await connection.OpenAsync(token);
            await using var transaction = (OracleTransaction)await connection.BeginTransactionAsync(token);
            await harness.Raw.StageMessageAsync(BlobEnvelope(harness, id, []), transaction, token);
            // Same transaction raw probe, not a reader that normalizes null payloads.
            await using (var command = connection.CreateCommand())
            {
                command.BindByName = true; command.CommandTimeout = 30; command.Transaction = transaction;
                command.CommandText = "SELECT message_body,DBMS_LOB.GETLENGTH(message_body) FROM OUTBOX WHERE message_id=:Id";
                _ = command.Parameters.Add("Id", OracleDbType.Varchar2, id, System.Data.ParameterDirection.Input);
                await using var reader = await command.ExecuteReaderAsync(token);
                (await reader.ReadAsync(token)).ShouldBeTrue(); reader.IsDBNull(0).ShouldBeFalse();
                Convert.ToInt64(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture).ShouldBe(0L);
                ((byte[])reader.GetValue(0)).ShouldBeEmpty(); (await reader.ReadAsync(token)).ShouldBeFalse();
            }
            (await ReadOwnedBlobAsync(id, false, token)).ShouldBeEmpty("the caller has not committed");
            if (commit) { await transaction.CommitAsync(token); }
            else { await transaction.RollbackAsync(token); }
            connection.State.ShouldBe(System.Data.ConnectionState.Open);
            await using (var probe = connection.CreateCommand())
            {
                probe.CommandText = "SELECT 1 FROM DUAL"; probe.CommandTimeout = 30;
                Convert.ToInt32(await probe.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture).ShouldBe(1);
            }
            var rows = await ReadOwnedBlobAsync(id, false, token);
            if (commit) { AssertBlob(rows.Single(), []); } else { rows.ShouldBeEmpty(); }
        });
    }

    [Fact]
    public async Task KeepAnEmptyBlobNonNullThroughClaimScopedDeadLetterCopy()
    {
        await RunOwnedBlobAsync(async (harness, id, token) =>
        {
            await harness.Store.StageMessageAsync(BlobEnvelope(harness, id, []), token);
            AssertBlob((await ReadOwnedBlobAsync(id, false, token)).Single(), []);
            var claim = (await harness.Raw.GetUnsentMessagesAsync(100, token)).Single(message => message.Id == id);
            var deadLetters = ((IOutboxStore)harness.Raw).GetService(typeof(IClaimScopedDeadLetterableOutboxStore))
                .ShouldBeAssignableTo<IClaimScopedDeadLetterableOutboxStore>().ShouldNotBeNull();
            (await deadLetters.MarkDeadLetteredAsync(id, "empty-preservation", 1, claim.DispatcherId!, token)).ShouldBe(OutboxCompletionOutcome.Applied);
            (await ReadOwnedBlobAsync(id, false, token)).ShouldBeEmpty();
            AssertBlob((await ReadOwnedBlobAsync(id, true, token)).Single(), []);
        });
    }

    private static OutboundMessage BlobEnvelope(Harness harness, string id, byte[] payload) => new("binary", payload, "binary-destination")
    { Id = id, TenantId = "before-tenant", CorrelationId = harness.Correlation };
    private static void AssertBlob((bool IsNull, long? Length, byte[]? Bytes) row, byte[] expected)
    {
        row.IsNull.ShouldBeFalse(); row.Length.ShouldBe(expected.LongLength); row.Bytes.ShouldNotBeNull().ShouldBe(expected);
    }
    private async Task<List<(bool IsNull, long? Length, byte[]? Bytes)>> ReadOwnedBlobAsync(string id, bool archive, CancellationToken token)
    {
        await using var connection = fixture.CreateConnection(); await connection.OpenAsync(token);
        await using var command = connection.CreateCommand(); command.BindByName = true; command.CommandTimeout = 30;
        if (archive) { command.CommandText = "SELECT message_body,DBMS_LOB.GETLENGTH(message_body) FROM OUTBOX_DEAD_LETTERS WHERE message_id=:Id"; }
        else { command.CommandText = "SELECT message_body,DBMS_LOB.GETLENGTH(message_body) FROM OUTBOX WHERE message_id=:Id"; }
        _ = command.Parameters.Add("Id", OracleDbType.Varchar2, id, System.Data.ParameterDirection.Input);
        await using var reader = await command.ExecuteReaderAsync(token);
        var rows = new List<(bool IsNull, long? Length, byte[]? Bytes)>();
        while (await reader.ReadAsync(token))
        {
            var isNull = reader.IsDBNull(0);
            rows.Add((isNull, reader.IsDBNull(1) ? null : Convert.ToInt64(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture),
                isNull ? null : (byte[])reader.GetValue(0)));
        }
        return rows;
    }
    private async Task RunOwnedBlobAsync(Func<Harness, string, CancellationToken, Task> assertion)
    {
        await RunAsync(null, async harness =>
        {
            var id = harness.Correlation + "-blob";
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            Exception? primary = null; Exception? cleanup = null;
            try { await assertion(harness, id, deadline.Token); }
            catch (Exception error) { primary = error; }
            finally
            {
                foreach (var table in OwnedBlobTables)
                {
                    try
                    {
                        using var cleanupDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        await using var connection = fixture.CreateConnection(); await connection.OpenAsync(cleanupDeadline.Token);
                        await using var command = connection.CreateCommand(); command.BindByName = true; command.CommandTimeout = 10;
                        if (table == "OUTBOX") { command.CommandText = "DELETE FROM OUTBOX WHERE message_id=:Id"; }
                        else { command.CommandText = "DELETE FROM OUTBOX_DEAD_LETTERS WHERE message_id=:Id"; }
                        _ = command.Parameters.Add("Id", OracleDbType.Varchar2, id, System.Data.ParameterDirection.Input);
                        _ = await command.ExecuteNonQueryAsync(cleanupDeadline.Token);
                    }
                    catch (Exception error) { cleanup = cleanup is null ? error : new AggregateException(cleanup, error); }
                }
            }
            if (primary is not null)
            {
                if (cleanup is not null) { primary.Data["SecondaryOwnedBlobCleanup"] = cleanup; }
                ExceptionDispatchInfo.Capture(primary).Throw();
            }
            if (cleanup is not null) { ExceptionDispatchInfo.Capture(cleanup).Throw(); }
        });
    }
}
