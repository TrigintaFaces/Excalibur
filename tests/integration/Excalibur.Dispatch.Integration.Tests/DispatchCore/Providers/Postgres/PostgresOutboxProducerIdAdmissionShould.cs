// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Runtime.ExceptionServices;
using Excalibur.Outbox;
using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Postgres;

// Shares the reviewed real-provider/owned-database harness, without changing its existing test identities.
public sealed partial class PostgresOutboxEnqueueTransformShould
{
    [Theory]
    [InlineData(0, null)]
    [InlineData(1, null)]
    [InlineData(2, null)]
    [InlineData(0, "")]
    [InlineData(1, "")]
    [InlineData(2, "")]
    [InlineData(0, " ")]
    [InlineData(1, " ")]
    [InlineData(2, " ")]
    public async Task RejectInvalidProducerMessageIdBeforePersistence(int route, string? id)
    {
        await RunAsync(null, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var readsBefore = harness.Db.Reads;
            async Task AssertRefusalAsync(NpgsqlTransaction? transaction)
            {
                Exception? observed = null;
                try
                {
                    if (route == 2) { await harness.Raw.SaveMessagesAsync([ProducerRecord(id, "invalid-producer")], deadline.Token); }
                    else
                    {
                        var message = ProducerEnvelope(id, "invalid-producer");
                        if (route == 1) { await harness.Raw.StageMessageAsync(message, transaction!, deadline.Token); }
                        else { await harness.Raw.StageMessageAsync(message, deadline.Token); }
                    }
                }
                catch (Exception error) { observed = error; }
                (await harness.ReadAsync("invalid-producer", deadline.Token)).ShouldBeEmpty();
                observed.ShouldBeAssignableTo<ArgumentException>().ShouldNotBeNull();
                harness.Db.Reads.ShouldBe(readsBefore, "invalid IDs are rejected before the provider obtains its connection");
                if (transaction is not null)
                {
                    transaction.Connection.ShouldBeSameAs(harness.Connection);
                    await using var command = new NpgsqlCommand("SELECT 1", harness.Connection, transaction) { CommandTimeout = 30 };
                    (await command.ExecuteScalarAsync(deadline.Token)).ShouldBe(1, "invalid input must not abort or complete the caller transaction");
                }
            }
            if (route == 1) { await WithCallerTransactionAsync(harness, transaction => AssertRefusalAsync(transaction), deadline.Token); }
            else { await AssertRefusalAsync(null); }
        });
    }

    [Fact]
    public async Task ValidateTheEntireSaveCollectionBeforeItsFirstAcceptingWrite()
    {
        await RunAsync(null, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var readsBefore = harness.Db.Reads;
            Exception? observed = null;
            try
            {
                await harness.Raw.SaveMessagesAsync([
                    ProducerRecord("first-valid-id", "batch-admission"),
                    ProducerRecord(null, "batch-admission")], deadline.Token);
            }
            catch (Exception error) { observed = error; }
            (await harness.ReadAsync("batch-admission", deadline.Token)).ShouldBeEmpty("a later invalid item must not allow an earlier item to be written");
            observed.ShouldBeAssignableTo<ArgumentException>().ShouldNotBeNull();
            harness.Db.Reads.ShouldBe(readsBefore);
            // This is validation atomicity only. Backend failures after accepting valid input retain
            // existing per-message/borrowed-transaction semantics; no general batch transaction is claimed.
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task KeepNonblankOpaqueProducerIdsExactAndCompletable(int route)
    {
        await RunAsync(null, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            const string id = "  opaque-producer-id  ";
            if (route == 2) { (await harness.Raw.SaveMessagesAsync([ProducerRecord(id, "valid-producer")], deadline.Token)).ShouldBe(1); }
            else if (route == 1)
            {
                await WithCallerTransactionAsync(harness, async transaction =>
                {
                    await harness.Raw.StageMessageAsync(ProducerEnvelope(id, "valid-producer"), transaction, deadline.Token);
                    (await harness.ReadAsync("valid-producer", deadline.Token)).ShouldBeEmpty();
                    await transaction.CommitAsync(deadline.Token);
                    harness.Connection.State.ShouldBe(ConnectionState.Open);
                }, deadline.Token);
            }
            else { await harness.Raw.StageMessageAsync(ProducerEnvelope(id, "valid-producer"), deadline.Token); }
            var row = (await harness.ReadAsync("valid-producer", deadline.Token)).Single();
            row.Id.ShouldBe(id); row.Payload.ShouldBe(new byte[] { 41 }); row.Tenant.ShouldBe("before-tenant");
            var claim = (await harness.Raw.GetUnsentMessagesAsync(10, deadline.Token)).Single();
            claim.Id.ShouldBe(id);
            var completion = ((IOutboxStore)harness.Raw).GetService(typeof(IClaimScopedOutboxStore))
                .ShouldBeAssignableTo<IClaimScopedOutboxStore>().ShouldNotBeNull();
            (await completion.MarkSentAsync(id, claim.DispatcherId!, deadline.Token)).ShouldBe(OutboxCompletionOutcome.Applied);
            (await harness.ReadAsync("valid-producer", deadline.Token)).ShouldBeEmpty();
        });
    }

    private static async Task WithCallerTransactionAsync(Harness harness, Func<NpgsqlTransaction, Task> assertion, CancellationToken token)
    {
        var transaction = await harness.Connection.BeginTransactionAsync(token);
        Exception? primary = null;
        Exception? cleanup = null;
        try { await assertion(transaction); }
        catch (Exception error) { primary = error; }
        finally
        {
            try { await transaction.DisposeAsync(); }
            catch (Exception error) { cleanup = error; }
        }
        if (primary is not null)
        {
            if (cleanup is not null) { primary.Data["SecondaryCallerTransactionCleanup"] = cleanup; }
            ExceptionDispatchInfo.Capture(primary).Throw();
        }
        if (cleanup is not null) { ExceptionDispatchInfo.Capture(cleanup).Throw(); }
    }

    private static OutboundMessage ProducerEnvelope(string? id, string correlation) => new("producer", [41], "producer-destination")
    {
        Id = id!, TenantId = "before-tenant", CorrelationId = correlation, CausationId = "producer-cause",
    };
    private static OutboxMessage ProducerRecord(string? id, string correlation) => new()
    {
        MessageId = id!, MessageType = "producer", MessageMetadata = "{}", MessageBody = [41], CreatedAt = DateTimeOffset.UtcNow,
        TenantId = "before-tenant", Destination = "producer-destination", CorrelationId = correlation, CausationId = "producer-cause",
    };
}
