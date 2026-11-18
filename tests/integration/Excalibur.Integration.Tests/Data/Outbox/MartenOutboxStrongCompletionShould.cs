// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using Excalibur.Dispatch;
using Excalibur.Outbox.Marten;
using global::Marten;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Shouldly;

namespace Excalibur.Integration.Tests.Data.Outbox;

[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Pattern", "Outbox")]
[Trait("Database", "Postgres")]
public sealed class MartenOutboxStrongCompletionShould(PostgresOutboxStoreContainerFixture fixture)
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task RefuseOldAndExpiredAcquisitionsWhileCurrentOwnerCommitsBothHalves(int operation)
    {
        fixture.DockerAvailable.ShouldBeTrue("actual PostgreSQL claim/document atomicity must not be skipped");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var schema = "marten_strong_" + Guid.NewGuid().ToString("N");
        // Test schemas are isolated so pending failure rows cannot change which message this arm claims.
        // A unique application name gives this test ownership of its data source/pool.
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { ApplicationName = schema }.ConnectionString;
        using var documents = DocumentStore.For(options =>
        {
            options.Connection(connectionString);
            options.DatabaseSchemaName = schema;
            options.AutoCreateSchemaObjects = global::JasperFx.AutoCreate.All;
        });
        using var store = new MartenOutboxStore(documents, Options.Create(new MartenOutboxStoreOptions
        {
            ClaimsSchemaName = schema, ClaimsTableName = "claims", ClaimTimeout = TimeSpan.FromMinutes(2),
            FailureBackoffFloorSeconds = 2,
        }), NullLogger<MartenOutboxStore>.Instance);
        var row = new OutboundMessage("event", [1, 2], "destination");
        await store.StageMessageAsync(row, deadline.Token);
        var first = (await store.GetUnsentMessagesAsync(1, deadline.Token)).Single();
        first.DispatcherId.ShouldNotBeNullOrWhiteSpace();
        var firstIdentity = first.DispatcherId!;
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(deadline.Token);
#pragma warning disable CA2100 // Schema is this test's own generated ASCII identifier.
        await using (var expire = new NpgsqlCommand($"UPDATE \"{schema}\".claims SET claimed_at = clock_timestamp() - INTERVAL '3 minutes' WHERE message_id = @Id;", connection))
#pragma warning restore CA2100
        {
            expire.Parameters.AddWithValue("Id", row.Id);
            (await expire.ExecuteNonQueryAsync(deadline.Token)).ShouldBe(1);
        }
        (await Complete(store, row.Id, firstIdentity, operation, deadline.Token)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
        var second = (await store.GetUnsentMessagesAsync(1, deadline.Token)).Single();
        second.DispatcherId.ShouldNotBe(firstIdentity);
        first.DispatcherId.ShouldBe(firstIdentity);
        (await Complete(store, row.Id, firstIdentity, operation, deadline.Token)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
        (await Complete(store, row.Id, second.DispatcherId!, operation, deadline.Token)).ShouldBe(OutboxCompletionOutcome.Applied);
        (await Complete(store, row.Id, second.DispatcherId!, operation, deadline.Token)).ShouldBe(
            operation == 0 ? OutboxCompletionOutcome.AlreadyTerminal : OutboxCompletionOutcome.ClaimLost);
        (await Complete(store, "missing", "known", operation, deadline.Token)).ShouldBe(OutboxCompletionOutcome.MessageNotFound);
#pragma warning disable CA2100 // Generated schema; payload/id remain bound parameters.
        await using var read = new NpgsqlCommand($"SELECT d.data::text, c.dispatcher_id, c.next_attempt_at, clock_timestamp() FROM \"{schema}\".mt_doc_martenoutboxdocument d JOIN \"{schema}\".claims c ON c.message_id = d.id WHERE d.id = @Id;", connection);
#pragma warning restore CA2100
        read.Parameters.AddWithValue("Id", row.Id);
        await using var reader = await read.ExecuteReaderAsync(deadline.Token);
        (await reader.ReadAsync(deadline.Token)).ShouldBeTrue();
        using var persisted = JsonDocument.Parse(reader.GetString(0));
        var expectedStatus = operation switch { 0 => OutboxStatus.Sent, 1 => OutboxStatus.Failed, _ => OutboxStatus.Staged };
        var status = Property(persisted.RootElement, "Status");
        if (status.ValueKind == JsonValueKind.Number) { status.GetInt32().ShouldBe((int)expectedStatus); }
        else { status.GetString().ShouldBe(expectedStatus.ToString()); }
        if (operation == 0)
        {
            reader.GetString(1).ShouldBe("__excalibur_settled__");
            Property(persisted.RootElement, "SentAt").ValueKind.ShouldNotBe(JsonValueKind.Null);
        }
        else
        {
            reader.GetString(1).ShouldBe("__excalibur_released__");
            Property(persisted.RootElement, "RetryCount").GetInt32().ShouldBe(2);
            Property(persisted.RootElement, "LastError").GetString().ShouldBe(operation == 1 ? "failure" : "backoff");
            Property(persisted.RootElement, "LastAttemptAt").ValueKind.ShouldNotBe(JsonValueKind.Null);
            var next = reader.GetFieldValue<DateTimeOffset>(2);
            var serverNow = reader.GetFieldValue<DateTimeOffset>(3);
            if (operation == 2) { next.ShouldBeGreaterThan(serverNow.AddSeconds(30)); }
            else { next.ShouldBeGreaterThan(DateTimeOffset.UnixEpoch); }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RollBackTheTerminalTombstoneWhenTheDocumentWriteIsCanceled(bool strong)
    {
        fixture.DockerAvailable.ShouldBeTrue("actual PostgreSQL rollback must not be skipped");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var schema = "marten_cancel_" + Guid.NewGuid().ToString("N");
        var application = "marten-write-" + Guid.NewGuid().ToString("N");
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { ApplicationName = application }.ConnectionString;
        using var documents = DocumentStore.For(options =>
        {
            options.Connection(connectionString);
            options.DatabaseSchemaName = schema;
            options.AutoCreateSchemaObjects = global::JasperFx.AutoCreate.All;
        });
        using var store = new MartenOutboxStore(documents, Options.Create(new MartenOutboxStoreOptions
        {
            ClaimsSchemaName = schema, ClaimsTableName = "claims", ClaimTimeout = TimeSpan.FromMinutes(2),
        }), NullLogger<MartenOutboxStore>.Instance);
        var row = new OutboundMessage("event", [1], "destination");
        await store.StageMessageAsync(row, deadline.Token);
        var claim = (await store.GetUnsentMessagesAsync(1, deadline.Token)).Single();
        claim.DispatcherId.ShouldNotBeNullOrWhiteSpace();
        await using var blocker = new NpgsqlConnection(fixture.ConnectionString);
        await blocker.OpenAsync(deadline.Token);
        await using var transaction = await blocker.BeginTransactionAsync(deadline.Token);
#pragma warning disable CA2100 // Generated owned schema, message identifier is bound.
        await using (var takeLock = new NpgsqlCommand($"SELECT id FROM \"{schema}\".mt_doc_martenoutboxdocument WHERE id = @Id FOR UPDATE;", blocker, transaction))
#pragma warning restore CA2100
        {
            takeLock.Parameters.AddWithValue("Id", row.Id);
            ((string?)await takeLock.ExecuteScalarAsync(deadline.Token)).ShouldBe(row.Id);
        }
        using var cancelWrite = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        var completion = strong
            ? Complete(store, row.Id, claim.DispatcherId!, 0, cancelWrite.Token).AsTask()
            : CompleteLegacyAsync(store, row.Id, cancelWrite.Token);
        var blockerReleased = false;
        try
        {
            // Observe the actual database lock wait before cancellation. Elapsed time alone is not evidence
            // that terminal arbitration has run or that the document write reached its failure boundary.
            await using var observer = new NpgsqlConnection(fixture.ConnectionString);
            await observer.OpenAsync(deadline.Token);
            var blocked = false;
            while (!blocked && !completion.IsCompleted)
            {
                await using var waiting = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE application_name = @Application AND wait_event_type = 'Lock' AND query LIKE '%mt_doc_martenoutboxdocument%');", observer);
                waiting.Parameters.AddWithValue("Application", application);
                blocked = (bool)(await waiting.ExecuteScalarAsync(deadline.Token))!;
                if (!blocked)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(50), deadline.Token); // delay-ok: Poll the observed PostgreSQL row-lock condition before cancellation; elapsed time is not phase evidence.
                }
            }
            blocked.ShouldBeTrue("the document write must reach the held row before cancellation");
            await cancelWrite.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(async () => await completion);
            await transaction.RollbackAsync(deadline.Token);
            blockerReleased = true;
    #pragma warning disable CA2100 // Generated owned schema and bound id.
            await using var read = new NpgsqlCommand($"SELECT dispatcher_id FROM \"{schema}\".claims WHERE message_id = @Id;", observer);
    #pragma warning restore CA2100
            read.Parameters.AddWithValue("Id", row.Id);
            ((string?)await read.ExecuteScalarAsync(deadline.Token)).ShouldBe(claim.DispatcherId,
                "a failed document save must not commit its terminal tombstone independently");
            // Positive control on the same acquisition proves rollback did not poison or consume it.
            (await Complete(store, row.Id, claim.DispatcherId!, 0, deadline.Token)).ShouldBe(OutboxCompletionOutcome.Applied);
        }
        finally
        {
            await CancelReleaseAndJoinAsync(completion, cancelWrite, transaction, blockerReleased);
        }
    }

    private static async Task<OutboxCompletionOutcome> CompleteLegacyAsync(MartenOutboxStore store, string id, CancellationToken token)
    {
        await store.MarkSentAsync(id, token);
        return OutboxCompletionOutcome.Applied;
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task WithholdAStaleCandidateAfterAtomicPurgeOrAnAgedTerminalTombstone(bool terminal)
    {
        fixture.DockerAvailable.ShouldBeTrue("actual PostgreSQL stale-query admission must not be skipped");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var schema = "marten_stale_" + Guid.NewGuid().ToString("N");
        var application = "marten-claim-" + Guid.NewGuid().ToString("N");
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { ApplicationName = application }.ConnectionString;
        using var documents = DocumentStore.For(options =>
        {
            options.Connection(connectionString);
            options.DatabaseSchemaName = schema;
            options.AutoCreateSchemaObjects = global::JasperFx.AutoCreate.All;
        });
        using var store = new MartenOutboxStore(documents, Options.Create(new MartenOutboxStoreOptions
        {
            ClaimsSchemaName = schema, ClaimsTableName = "claims", ClaimTimeout = TimeSpan.FromMinutes(2),
        }), NullLogger<MartenOutboxStore>.Instance);
        var row = new OutboundMessage("event", [1], "destination");
        await store.StageMessageAsync(row, deadline.Token);
        (await store.GetUnsentMessagesAsync(1, deadline.Token)).ShouldHaveSingleItem();
        await using var blocker = new NpgsqlConnection(fixture.ConnectionString);
        await blocker.OpenAsync(deadline.Token);
        await using var transaction = await blocker.BeginTransactionAsync(deadline.Token);
#pragma warning disable CA2100 // Generated test-owned schema, all runtime values bound.
        await using (var claimLock = new NpgsqlCommand($"UPDATE \"{schema}\".claims SET claimed_at = clock_timestamp() - INTERVAL '3 minutes' WHERE message_id = @Id;", blocker, transaction))
#pragma warning restore CA2100
        {
            claimLock.Parameters.AddWithValue("Id", row.Id);
            (await claimLock.ExecuteNonQueryAsync(deadline.Token)).ShouldBe(1);
        }
        using var cancelDrain = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        var drain = store.GetUnsentMessagesAsync(1, cancelDrain.Token).AsTask();
        var blockerReleased = false;
        Exception? primaryFailure = null;
        try
        {
            await using var observer = new NpgsqlConnection(fixture.ConnectionString);
            await observer.OpenAsync(deadline.Token);
            // Candidate materialization precedes OpenClaimConnection/EnsureTable. Its current ALTER
            // requests AccessExclusive on claims, before any acquisition can be attempted.
            await ObserveExactClaimInitializerWaitAsync(observer, application, schema, blocker.ProcessID,
                drain, deadline.Token).ConfigureAwait(false);
            // Test-owned SQL simulates either an atomic purge or an aged retained terminal tombstone.
            // The latter intentionally retains a Staged document, representing the old split-settle crash state.
    #pragma warning disable CA2100 // Generated test-owned schema and bound id.
            await using (var change = new NpgsqlCommand(terminal
                ? $"UPDATE \"{schema}\".claims SET dispatcher_id = '__excalibur_settled__' WHERE message_id = @Id;"
                : $"DELETE FROM \"{schema}\".mt_doc_martenoutboxdocument WHERE id = @Id; DELETE FROM \"{schema}\".claims WHERE message_id = @Id;", blocker, transaction))
    #pragma warning restore CA2100
            {
                change.Parameters.AddWithValue("Id", row.Id);
                await change.ExecuteNonQueryAsync(deadline.Token);
            }
            await transaction.CommitAsync(deadline.Token);
            blockerReleased = true;
            (await drain).ShouldBeEmpty("a pre-claim materialized document is not proof of current deliverability");
    #pragma warning disable CA2100 // Generated owned schema, bound id.
            await using var count = new NpgsqlCommand($"SELECT count(*) FROM \"{schema}\".claims WHERE message_id = @Id;", observer);
    #pragma warning restore CA2100
            count.Parameters.AddWithValue("Id", row.Id);
            ((long)(await count.ExecuteScalarAsync(deadline.Token))!).ShouldBe(terminal ? 1L : 0L);
            // Unrelated current work must still be deliverable, excluding a drain that suppresses all rows.
            var healthy = new OutboundMessage("event", [2], "destination") { Priority = -100 };
            await store.StageMessageAsync(healthy, deadline.Token);
            var healthyClaim = (await store.GetUnsentMessagesAsync(2, deadline.Token)).Single();
            healthyClaim.Id.ShouldBe(healthy.Id);
            (await Complete(store, healthy.Id, healthyClaim.DispatcherId!, 0, deadline.Token)).ShouldBe(OutboxCompletionOutcome.Applied);
        }
        catch (Exception error) { primaryFailure = error; throw; }
        finally
        {
            try { await CancelReleaseAndJoinAsync(drain, cancelDrain, transaction, blockerReleased); }
            catch (Exception cleanupError) when (primaryFailure is not null)
            {
                TestContext.Current.TestOutputHelper?.WriteLine("Cleanup/join failure after the primary barrier/assertion failure: {0}", cleanupError);
            }
        }
    }

    [Fact]
    public async Task ReturnTheOpenedConnectionToItsPoolWhenClaimSetupFails()
    {
        fixture.DockerAvailable.ShouldBeTrue("resource ownership requires an actual connection pool, never skipped");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var schema = "marten_setup_" + Guid.NewGuid().ToString("N");
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            ApplicationName = schema, MaxPoolSize = 1, MinPoolSize = 0, Timeout = 3,
        }.ConnectionString;
        using var documents = DocumentStore.For(options =>
        {
            options.Connection(connectionString);
            options.DatabaseSchemaName = schema;
            options.AutoCreateSchemaObjects = global::JasperFx.AutoCreate.All;
        });
        using var store = new MartenOutboxStore(documents, Options.Create(new MartenOutboxStoreOptions
        {
            ClaimsSchemaName = "invalid-schema", ClaimsTableName = "claims",
        }), NullLogger<MartenOutboxStore>.Instance);
        await store.StageMessageAsync(new OutboundMessage("event", [1], "destination"), deadline.Token);
        var error = await Should.ThrowAsync<ArgumentException>(async () =>
            await store.GetUnsentMessagesAsync(1, deadline.Token));
        error.ParamName.ShouldBe("schema");
        // With a maximum pool size of one, leaking the connection opened before Qualify failed blocks this.
        await using var healthy = new NpgsqlConnection(connectionString);
        await healthy.OpenAsync(deadline.Token);
        await using var ping = new NpgsqlCommand("SELECT 1;", healthy);
        ((int)(await ping.ExecuteScalarAsync(deadline.Token))!).ShouldBe(1);
    }

    [Fact]
    public async Task AcquireClaimLocksInClrOrdinalOrderEvenWhenDatabaseCollationDiffers()
    {
        fixture.DockerAvailable.ShouldBeTrue("the lock order is verified by actual PostgreSQL insert execution");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var schema = "marten_order_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(deadline.Token);
        await using var transaction = await connection.BeginTransactionAsync(deadline.Token);
#pragma warning disable CA2100 // All identifiers are generated by this test; values are bound.
        await using (var setup = new NpgsqlCommand($"""
            CREATE SCHEMA "{schema}";
            CREATE TABLE "{schema}".claims (
                message_id text COLLATE "C" PRIMARY KEY, dispatcher_id text NOT NULL,
                claimed_at timestamptz NOT NULL, next_attempt_at timestamptz NULL);
            CREATE TABLE "{schema}".observed (sequence bigint GENERATED ALWAYS AS IDENTITY, message_id text NOT NULL);
            CREATE FUNCTION "{schema}".observe_claim() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                INSERT INTO "{schema}".observed(message_id) VALUES (NEW.message_id);
                RETURN NEW;
            END;
            $$;
            CREATE TRIGGER observe_claim BEFORE INSERT ON "{schema}".claims
                FOR EACH ROW EXECUTE FUNCTION "{schema}".observe_claim();
            """, connection, transaction))
#pragma warning restore CA2100
        {
            await setup.ExecuteNonQueryAsync(deadline.Token);
        }
        // Ordinal UTF-16 orders the supplementary character before U+E000; PostgreSQL C UTF-8
        // collation orders those in the other direction. ASCII-only ids would not expose this defect.
        string[] ids = ["\uE000", "z", "\U00010000", "a", "A"];
        var expected = ids.Order(StringComparer.Ordinal).ToArray();
        await using (var collation = new NpgsqlCommand("SELECT array_agg(id ORDER BY id COLLATE \"C\") FROM unnest(@Ids) AS items(id);", connection, transaction))
        {
            collation.Parameters.AddWithValue("Ids", ids);
            var databaseOrder = (string[])(await collation.ExecuteScalarAsync(deadline.Token))!;
            databaseOrder.SequenceEqual(expected, StringComparer.Ordinal).ShouldBeFalse("the control must distinguish database and CLR orders");
        }
        var claimed = await MartenOutboxClaims.ClaimInTransactionAsync(connection, transaction,
            schema, "claims", ids, "owner:acquisition", TimeSpan.FromMinutes(2), deadline.Token);
        claimed.Count.ShouldBe(ids.Length);
        var observed = new List<string>();
#pragma warning disable CA2100 // Generated test-owned schema.
        await using (var read = new NpgsqlCommand($"SELECT message_id FROM \"{schema}\".observed ORDER BY sequence;", connection, transaction))
#pragma warning restore CA2100
        await using (var reader = await read.ExecuteReaderAsync(deadline.Token))
        {
            while (await reader.ReadAsync(deadline.Token)) { observed.Add(reader.GetString(0)); }
        }
        observed.SequenceEqual(expected, StringComparer.Ordinal).ShouldBeTrue(
            "claim and cleanup must acquire all keys in the same ordinal order");
        await transaction.RollbackAsync(deadline.Token);
    }

    private static async Task ObserveExactClaimInitializerWaitAsync(NpgsqlConnection observer,
        string application, string schema, int blockerPid, Task drain, CancellationToken token)
    {
        var qualifiedClaims = $"\"{schema}\".\"claims\"";
        var ddlFragment = $"ALTER TABLE {qualifiedClaims} ADD COLUMN IF NOT EXISTS next_attempt_at timestamptz NULL";
        int? workerPid = null;
        var lastSnapshot = "No poll completed.";
        try
        {
        while (!drain.IsCompleted)
        {
            // Bounded diagnostics only for this test's unique application; predicate below is unchanged.
            await using (var snapshot = new NpgsqlCommand("""
                SELECT json_build_object(
                    'track_activity_query_size', current_setting('track_activity_query_size'),
                    'application', @Application,
                    'sessions', COALESCE((SELECT json_agg(s) FROM (
                        SELECT a.pid, a.state, left(a.query,8192) AS query, a.wait_event,
                               a.wait_event_type, pg_blocking_pids(a.pid) AS blocking_pids,
                               (SELECT json_agg(locks) FROM (
                                   SELECT l.locktype, l.relation::text AS relation_oid,
                                          l.relation::regclass::text AS relation_name, l.mode, l.granted
                                   FROM pg_locks l WHERE l.pid=a.pid
                                   ORDER BY l.locktype, l.relation, l.mode LIMIT 32
                               ) locks) AS locks
                        FROM pg_stat_activity a WHERE a.application_name=@Application
                        ORDER BY a.pid LIMIT 8
                    ) s), '[]'::json))::text;
                """, observer))
            {
                snapshot.Parameters.AddWithValue("Application", application);
                lastSnapshot = (string)(await snapshot.ExecuteScalarAsync(token))!;
            }
            if (workerPid is null)
            {
                await using var identify = new NpgsqlCommand("SELECT pid FROM pg_stat_activity WHERE application_name=@Application AND state='active' AND strpos(query,@DdlFragment)>0;", observer);
                identify.Parameters.AddWithValue("Application", application);
                identify.Parameters.AddWithValue("DdlFragment", ddlFragment);
                await using var rows = await identify.ExecuteReaderAsync(token);
                if (await rows.ReadAsync(token))
                {
                    workerPid = rows.GetInt32(0);
                    workerPid.Value.ShouldNotBe(blockerPid);
                    workerPid.Value.ShouldNotBe(observer.ProcessID);
                    (await rows.ReadAsync(token)).ShouldBeFalse("the unique application and exact initializer must identify one worker PID");
                }
            }
            if (workerPid is { } pid)
            {
                await using var waiting = new NpgsqlCommand("""
                    SELECT EXISTS (
                        SELECT 1 FROM pg_stat_activity a
                        WHERE a.pid=@WorkerPid AND a.application_name=@Application AND a.state='active'
                          AND strpos(a.query,@DdlFragment)>0 AND a.wait_event_type='Lock'
                          AND @BlockerPid=ANY(pg_blocking_pids(a.pid))
                          AND EXISTS (SELECT 1 FROM pg_locks l
                                      WHERE l.pid=a.pid AND l.relation=to_regclass(@QualifiedClaims)
                                        AND l.mode='AccessExclusiveLock' AND NOT l.granted));
                    """, observer);
                waiting.Parameters.AddWithValue("WorkerPid", pid);
                waiting.Parameters.AddWithValue("Application", application);
                waiting.Parameters.AddWithValue("DdlFragment", ddlFragment);
                waiting.Parameters.AddWithValue("BlockerPid", blockerPid);
                waiting.Parameters.AddWithValue("QualifiedClaims", qualifiedClaims);
                if ((bool)(await waiting.ExecuteScalarAsync(token))!) { return; }
            }
            await Task.Delay(TimeSpan.FromMilliseconds(50), token); // delay-ok: exact server PID/relation/blocker lock observation.
        }
        throw new InvalidOperationException("The selected worker never reached its exact claims initializer AccessExclusive wait behind this test's blocker.");
        }
        catch (OperationCanceledException error) when (token.IsCancellationRequested)
        {
            throw new OperationCanceledException($"Exact initializer observation canceled. Last bounded test-owned snapshot: {lastSnapshot}", error, token);
        }
        catch (Exception error)
        {
            throw new InvalidOperationException($"Exact initializer observation failed. Last bounded test-owned snapshot: {lastSnapshot}", error);
        }
    }

    private static async Task CancelReleaseAndJoinAsync(Task owned, CancellationTokenSource cancellation,
        NpgsqlTransaction blocker, bool blockerReleased)
    {
        // Observation or an assertion can fail before the normal await. Always release the database
        // blocker and join the operation before its connections/transaction leave this test's scope.
        try
        {
            await cancellation.CancelAsync();
        }
        finally
        {
            try
            {
                if (!blockerReleased)
                {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await blocker.RollbackAsync(cleanup.Token);
                }
            }
            finally
            {
                try { await owned; }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            }
        }
    }

    private static JsonElement Property(JsonElement document, string name) =>
        document.EnumerateObject().Single(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)).Value;

    private static ValueTask<OutboxCompletionOutcome> Complete(MartenOutboxStore store, string id, string claim, int operation, CancellationToken token) =>
        operation switch
        {
            0 => store.MarkSentAsync(id, claim, token),
            1 => store.MarkFailedAsync(id, "failure", 2, claim, token),
            _ => store.MarkFailedWithBackoffAsync(id, "backoff", 2, DateTimeOffset.UtcNow.AddMinutes(1), claim, token),
        };
}
