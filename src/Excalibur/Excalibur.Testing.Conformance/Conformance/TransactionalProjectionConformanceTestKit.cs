// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;

namespace Excalibur.Testing.Conformance;

/// <summary>Real-storage contract tests for aggregate-local projections enlisted in an event append.</summary>
/// <remarks>
/// Every arm requires the transactional capabilities. Missing capabilities fail, never skip.
/// Fixtures must resolve stores using their shipped registrations and provision isolated destinations.
/// Readback must open a new database session, so uncommitted connection-local state cannot satisfy an assertion.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Conformance arm naming convention")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Conformance runs in an untrimmed test host.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Conformance runs in an untrimmed test host.")]
public abstract class TransactionalProjectionConformanceTestKit : ConformanceTestKit
{
    private const string AggregateType = "TransactionalConformance";

    /// <summary>Resolves an event store against the fixture's real database.</summary>
    /// <returns>The normally registered event store, including its decorators.</returns>
    protected abstract Task<IEventStore> CreateEventStoreAsync();

    /// <summary>Provisions and resolves a fresh dedicated transactional destination.</summary>
    /// <returns>A projection store in the event store's database and tenant.</returns>
    protected abstract Task<IProjectionStore<ConformanceProjection>> CreateProjectionStoreAsync();

    /// <summary>Creates a store whose commit reaches the engine and then loses its acknowledgement.</summary>
    /// <returns>A fault-injected event store targeting the same source.</returns>
    protected abstract Task<IEventStore> CreateLostAcknowledgementStoreAsync();

    /// <summary>Opens a separate connection to the fixture's event source for adversarial locking tests.</summary>
    /// <returns>An open connection owned by the caller.</returns>
    protected abstract Task<System.Data.Common.DbConnection> OpenSourceConnectionAsync();

    /// <summary>Creates an enlisted update marking the selected history erased, without changing its version.</summary>
    /// <param name="connection">Open source connection.</param>
    /// <param name="transaction">The adversarial transaction.</param>
    /// <param name="aggregateId">Stream identifier.</param>
    /// <param name="aggregateType">Stream type.</param>
    /// <returns>A caller-owned command that marks only the fixture tenant's selected stream.</returns>
    protected abstract System.Data.Common.DbCommand CreateHistoryErasureCommand(System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction, string aggregateId, string aggregateType);

    /// <summary>Requires an engine lock refusal while another transaction owns the selected history.</summary>
    /// <param name="aggregateId">Locked stream identifier.</param>
    /// <param name="aggregateType">Locked stream type.</param>
    /// <returns>Completes only after the engine has refused the conflicting lock.</returns>
    protected abstract Task AssertHistoryLockRefusedAsync(string aggregateId, string aggregateType);

    /// <summary>Observes an engine waiter, or a completed SQLite busy refusal, before releasing history.</summary>
    /// <param name="blocker">The connection holding the erasure transaction.</param>
    /// <param name="append">The pending append attempt.</param>
    /// <returns>True when a busy-refused append must be retried after releasing the lock.</returns>
    protected abstract Task<bool> ObserveHistoryContentionAsync(System.Data.Common.DbConnection blocker, Task append);

    /// <summary>Verifies an erasure committed while history reading waits is observed by the callback.</summary>
    /// <returns>The verification task.</returns>
    public virtual async Task Observe_erasure_that_commits_while_waiting_for_history()
    {
        RecordArmExecuted(nameof(Observe_erasure_that_commits_while_waiting_for_history));
        var events = await CreateEventStoreAsync().ConfigureAwait(false);
        var id = Guid.NewGuid().ToString("N");
        Require((await events.AppendAsync(id, AggregateType, Batch(id, 0), -1, CancellationToken.None).ConfigureAwait(false)).Success, "Seed append failed.");
        await using var connection = await OpenSourceConnectionAsync().ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(CancellationToken.None).ConfigureAwait(false);
        await using var erase = CreateHistoryErasureCommand(connection, transaction, id, AggregateType);
        Require(await erase.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false) == 1, "Adversarial erasure must lock exactly the seeded event.");
        var called = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new InvalidOperationException("Refuse erased history");
#pragma warning disable RS0030 // Independent SQLite writer needs its own worker while the test owns the erasure lock.
        Task AppendAttemptAsync() => Task.Run(async () =>
        {
            try
            {
                await Capability(events).AppendWithProjectionsAsync(id, AggregateType, Batch(id, 1), 0,
                    (history, _) =>
                    {
                        called.SetResult();
                        Require(history.History.Count == 1 && ErasedEventMarker.IsErased(history.History[0].EventType),
                            "History published after waiting must observe the committed erasure, not stale pre-lock payload.");
                        throw failure;
                    }, CancellationToken.None).ConfigureAwait(false);
                throw new TestFixtureAssertionException("Erased-history handler failure was swallowed.");
            }
            catch (InvalidOperationException exception) when (ReferenceEquals(exception, failure)) { }
        });
#pragma warning restore RS0030
        var append = AppendAttemptAsync();
        var retry = await ObserveHistoryContentionAsync(connection, append).ConfigureAwait(false);
        Require(!called.Task.IsCompleted, "The callback must not observe history while erasure owns its lock.");
        await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
        if (retry) { append = AppendAttemptAsync(); }
        await append.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        Require((await events.LoadAsync(id, AggregateType, CancellationToken.None).ConfigureAwait(false)).Count == 1, "Rejected erased-history append changed stream length.");
    }

    /// <summary>Verifies observed history remains locked until models and pending events commit.</summary>
    /// <returns>The verification task.</returns>
    public virtual async Task Hold_history_against_erasure_until_commit()
    {
        RecordArmExecuted(nameof(Hold_history_against_erasure_until_commit));
        var events = await CreateEventStoreAsync().ConfigureAwait(false);
        var projection = await CreateProjectionStoreAsync().ConfigureAwait(false);
        var id = Guid.NewGuid().ToString("N");
        Require((await events.AppendAsync(id, AggregateType, Batch(id, 0), -1, CancellationToken.None).ConfigureAwait(false)).Success, "Seed append failed.");
        var inCallback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
#pragma warning disable RS0030 // Real SQLite writer contention requires independent workers.
        var append = Task.Run(async () => await Capability(events).AppendWithProjectionsAsync(id, AggregateType, Batch(id, 1), 0,
            async (history, token) =>
            {
                Require(history.History.Count == 1 && !ErasedEventMarker.IsErased(history.History[0].EventType), "Seed history must be intact.");
                inCallback.SetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(30), token).ConfigureAwait(false);
                await WriteAsync(projection, history, id, 2, token).ConfigureAwait(false);
            }, CancellationToken.None).ConfigureAwait(false));
        await inCallback.Task.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
#pragma warning restore RS0030
        try
        {
            // Require an actual engine lock error, not a scheduling delay or an incomplete Task.
            await AssertHistoryLockRefusedAsync(id, AggregateType).ConfigureAwait(false);
        }
        finally { release.TrySetResult(); }
        Require((await append.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false)).Success, "History-owning save must commit.");
        await using var connection = await OpenSourceConnectionAsync().ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(CancellationToken.None).ConfigureAwait(false);
        await using var command = CreateHistoryErasureCommand(connection, transaction, id, AggregateType);
        Require(await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false) == 2, "Erasure must succeed after the history owner commits.");
        await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
        Require((await projection.GetByIdAsync(id, CancellationToken.None).ConfigureAwait(false))?.Total == 2, "The committed model is missing.");
    }

    /// <summary>Verifies ordinary reads, queries, counts and deletion remain usable on the dedicated destination.</summary>
    /// <returns>The verification task.</returns>
    public virtual async Task Query_and_count_visible_models_and_preserve_deletion()
    {
        RecordArmExecuted(nameof(Query_and_count_visible_models_and_preserve_deletion));
        var store = await CreateProjectionStoreAsync().ConfigureAwait(false);
        foreach (var total in new[] { 10, 20, 30 })
        {
            var id = total.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await store.UpsertAsync(id, new ConformanceProjection { Id = id, Total = total }, CancellationToken.None).ConfigureAwait(false);
        }
        var filters = new Dictionary<string, object> { ["Total:gte"] = 20 };
        Require(await store.CountAsync(filters, CancellationToken.None).ConfigureAwait(false) == 2, "Count must apply the supplied filter.");
        var rows = await store.QueryAsync(filters, new QueryOptions(Take: 1, OrderBy: "Total", Descending: true), CancellationToken.None).ConfigureAwait(false);
        Require(rows.Count == 1 && rows[0].Total == 30, "Query must filter, order and page models.");
        await store.DeleteAsync("30", CancellationToken.None).ConfigureAwait(false);
        await store.DeleteAsync("30", CancellationToken.None).ConfigureAwait(false);
        Require(await store.CountAsync(filters, CancellationToken.None).ConfigureAwait(false) == 1, "Count must hide retirement metadata.");
        Require(await store.GetByIdAsync("30", CancellationToken.None).ConfigureAwait(false) is null, "Read must hide retired payload.");
        var rejected = false;
        try { await store.UpsertAsync("30", new ConformanceProjection { Total = 99 }, CancellationToken.None).ConfigureAwait(false); }
        catch (Exception exception) when (exception is not TestFixtureAssertionException and not OperationCanceledException) { rejected = true; }
        Require(rejected && await store.GetByIdAsync("30", CancellationToken.None).ConfigureAwait(false) is null,
            "An ordinary upsert must not resurrect a retired key.");
    }

    /// <summary>Verifies cancellation before commit rolls back already staged model changes.</summary>
    /// <returns>The verification task.</returns>
    public virtual async Task Roll_back_when_cancelled_before_commit()
    {
        RecordArmExecuted(nameof(Roll_back_when_cancelled_before_commit));
        var events = await CreateEventStoreAsync().ConfigureAwait(false);
        var projection = await CreateProjectionStoreAsync().ConfigureAwait(false);
        var id = Guid.NewGuid().ToString("N");
        using var cancellation = new CancellationTokenSource();
        var cancelled = false;
        try
        {
            await Capability(events).AppendWithProjectionsAsync(id, AggregateType, Batch(id, 0), -1,
                async (transaction, token) =>
                {
                    await WriteAsync(projection, transaction, id, 1, token).ConfigureAwait(false);
                    await cancellation.CancelAsync().ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                }, cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { cancelled = true; }
        Require(cancelled, "Cancellation must propagate.");
        Require((await events.LoadAsync(id, AggregateType, CancellationToken.None).ConfigureAwait(false)).Count == 0, "Cancellation before commit appended an event.");
        Require(await projection.GetByIdAsync(id, CancellationToken.None).ConfigureAwait(false) is null, "Cancellation before commit published the model.");
    }

    /// <summary>Verifies acknowledgement loss remains unknown even though events and projections are durable.</summary>
    /// <returns>The verification task.</returns>
    public virtual async Task Report_unknown_after_lost_commit_acknowledgement()
    {
        RecordArmExecuted(nameof(Report_unknown_after_lost_commit_acknowledgement));
        var events = await CreateLostAcknowledgementStoreAsync().ConfigureAwait(false);
        var projection = await CreateProjectionStoreAsync().ConfigureAwait(false);
        var id = Guid.NewGuid().ToString("N");
        var batch = Batch(id, 0);
        var result = await Capability(events).AppendWithProjectionsAsync(id, AggregateType, batch, -1,
            (transaction, token) => WriteAsync(projection, transaction, id, 9, token), CancellationToken.None).ConfigureAwait(false);
        Require(result.Outcome == AppendOutcome.Unknown, "An unacknowledged commit must not report success, conflict or confirmed rollback.");
        var fresh = await CreateEventStoreAsync().ConfigureAwait(false);
        Require((await fresh.LoadAsync(id, AggregateType, CancellationToken.None).ConfigureAwait(false)).Count == 1, "Injected commit did not reach the database.");
        Require((await projection.GetByIdAsync(id, CancellationToken.None).ConfigureAwait(false))?.Total == 9, "Committed model was not atomic with events.");
        var called = false;
        var retry = await Capability(fresh).AppendWithProjectionsAsync(id, AggregateType, batch, -1,
            (_, _) => { called = true; return ValueTask.CompletedTask; }, CancellationToken.None).ConfigureAwait(false);
        Require(retry.Outcome == AppendOutcome.Unknown && !called, "Identity recognition cannot attest the retry's projection callback.");
    }

    /// <summary>Verifies a stale transactional model cannot be resumed after an ordinary upsert.</summary>
    /// <returns>The verification task.</returns>
    public virtual async Task Refuse_model_replaced_by_ordinary_upsert()
    {
        RecordArmExecuted(nameof(Refuse_model_replaced_by_ordinary_upsert));
        var events = await CreateEventStoreAsync().ConfigureAwait(false);
        var projection = await CreateProjectionStoreAsync().ConfigureAwait(false);
        var id = Guid.NewGuid().ToString("N");
        Require((await Capability(events).AppendWithProjectionsAsync(id, AggregateType, Batch(id, 0), -1,
            (transaction, token) => WriteAsync(projection, transaction, id, 1, token), CancellationToken.None).ConfigureAwait(false)).Success,
            "Initial transactional write must succeed.");
        await projection.UpsertAsync(id, new ConformanceProjection { Id = id, Total = 99 }, CancellationToken.None).ConfigureAwait(false);
        Require((await projection.GetByIdAsync(id, CancellationToken.None).ConfigureAwait(false))?.Total == 99, "Ordinary upsert must retain its existing semantics.");
        await RequireRejectedAsync(() => Capability(events).AppendWithProjectionsAsync(id, AggregateType, Batch(id, 1), 0,
            (transaction, token) => WriteAsync(projection, transaction, id, 2, token), CancellationToken.None)).ConfigureAwait(false);
        Require((await events.LoadAsync(id, AggregateType, CancellationToken.None).ConfigureAwait(false)).Count == 1, "Rejected mode change appended events.");
        Require((await projection.GetByIdAsync(id, CancellationToken.None).ConfigureAwait(false))?.Total == 99, "Rejected mode change overwrote the ordinary state.");
    }

    /// <summary>Verifies source, aggregate-type and tenant mismatches cannot overwrite an existing transactional model.</summary>
    /// <returns>The verification task.</returns>
    public virtual async Task Preserve_source_type_and_tenant_ownership()
    {
        RecordArmExecuted(nameof(Preserve_source_type_and_tenant_ownership));
        var events = await CreateEventStoreAsync().ConfigureAwait(false);
        var projection = await CreateProjectionStoreAsync().ConfigureAwait(false);
        var id = Guid.NewGuid().ToString("N");
        Require((await Capability(events).AppendWithProjectionsAsync(id, AggregateType, Batch(id, 0), -1,
            (transaction, token) => WriteAsync(projection, transaction, id, 5, token), CancellationToken.None).ConfigureAwait(false)).Success, "Seed commit failed.");
        var writer = (ITransactionalProjectionStore<ConformanceProjection>)projection.GetService(typeof(ITransactionalProjectionStore<ConformanceProjection>))!;
        foreach (var mismatch in new[] { "source", "tenant", "type" })
        {
            await RequireRejectedAsync(() => Capability(events).AppendWithProjectionsAsync(id, AggregateType, Batch(id, 1), 0,
                (transaction, token) => writer.WriteAsync(mismatch switch
                    {
                        "source" => transaction with { SourceIdentity = transaction.SourceIdentity + "_other" },
                        "tenant" => transaction with { TenantId = "other-tenant" },
                        _ => transaction,
                    }, id, mismatch == "type" ? "OtherAggregate" : AggregateType, "{}", token), CancellationToken.None)).ConfigureAwait(false);
        }
        Require((await projection.GetByIdAsync(id, CancellationToken.None).ConfigureAwait(false))?.Total == 5, "Mismatched owner changed the model.");
        Require((await events.LoadAsync(id, AggregateType, CancellationToken.None).ConfigureAwait(false)).Count == 1, "Mismatched owner appended events.");
    }

    /// <summary>Verifies contending saves cannot leave the losing writer's model durable.</summary>
    /// <returns>The verification task.</returns>
    public virtual async Task Commit_only_the_winning_concurrent_model()
    {
        RecordArmExecuted(nameof(Commit_only_the_winning_concurrent_model));
        var projection = await CreateProjectionStoreAsync().ConfigureAwait(false);
        var first = await CreateEventStoreAsync().ConfigureAwait(false);
        var second = await CreateEventStoreAsync().ConfigureAwait(false);
        var id = Guid.NewGuid().ToString("N");
        async Task<AppendResult> Save(IEventStore store, int total)
        {
            try
            {
                return await Capability(store).AppendWithProjectionsAsync(id, AggregateType, Batch(id, 0), -1,
                    (transaction, token) => WriteAsync(projection, transaction, id, total, token), CancellationToken.None).ConfigureAwait(false);
            }
            catch (System.Data.Common.DbException) { return AppendResult.CreateFailure("Database rejected a contending transaction."); }
        }
#pragma warning disable RS0030 // SQLite's async APIs execute synchronously; separate workers are essential to exercise real writer contention in this test arm.
        var results = await Task.WhenAll(Task.Run(() => Save(first, 10)), Task.Run(() => Save(second, 20))).ConfigureAwait(false);
#pragma warning restore RS0030
        Require(results.Count(result => result.Success) == 1, "Exactly one competing first append must commit.");
        Require((await first.LoadAsync(id, AggregateType, CancellationToken.None).ConfigureAwait(false)).Count == 1, "Competing appends must leave one event.");
        Require((await projection.GetByIdAsync(id, CancellationToken.None).ConfigureAwait(false))?.Total == (results[0].Success ? 10 : 20),
            "Losing transaction's model escaped rollback.");
    }

    private static async Task RequireRejectedAsync(Func<ValueTask<AppendResult>> operation)
    {
        var rejected = false;
        try
        {
            var result = await operation().ConfigureAwait(false);
            rejected = !result.Success && result.Outcome != AppendOutcome.Unknown;
        }
        catch (Exception exception) when (exception is not TestFixtureAssertionException and not OperationCanceledException) { rejected = true; }
        Require(rejected, "The invalid transactional write must be refused before commit.");
    }

    /// <summary>Resolves two model types with different destinations from one service provider and scope.</summary>
    /// <returns>The independently configured stores from the same host.</returns>
    protected abstract Task<(IProjectionStore<ConformanceProjection> First,
        IProjectionStore<ConformanceProjectionNextGeneration> Second)> CreateRegisteredProjectionPairAsync();

    /// <summary>Verifies same-host typed registration preserves both models and rolls both back on failure.</summary>
    /// <returns>The verification task.</returns>
    public virtual async Task Isolate_two_registered_models_in_one_host()
    {
        RecordArmExecuted(nameof(Isolate_two_registered_models_in_one_host));
        var events = await CreateEventStoreAsync().ConfigureAwait(false);
        var (first, second) = await CreateRegisteredProjectionPairAsync().ConfigureAwait(false);
        var secondCapability = ValidateCapability<ITransactionalProjectionStore<ConformanceProjectionNextGeneration>>(second,
            second.GetService(typeof(ITransactionalProjectionStore<ConformanceProjectionNextGeneration>)))
            ?? throw new TestFixtureAssertionException("Second registered model lacks the transactional capability.");
        var id = Guid.NewGuid().ToString("N");
        async ValueTask Apply(ProjectionTransaction transaction, int total, CancellationToken token)
        {
            await WriteAsync(first, transaction, id, total, token).ConfigureAwait(false);
            await secondCapability.WriteAsync(transaction, id, AggregateType,
                secondCapability.Serialize(new ConformanceProjectionNextGeneration { Id = id, Total = total * 10 }), token).ConfigureAwait(false);
        }
        var committed = await Capability(events).AppendWithProjectionsAsync(id, AggregateType, Batch(id, 0), -1,
            (transaction, token) => Apply(transaction, 3, token), CancellationToken.None).ConfigureAwait(false);
        Require(committed.Success, "Both registered models must commit.");
        Require((await first.GetByIdAsync(id, CancellationToken.None).ConfigureAwait(false))?.Total == 3, "First model was redirected or overwritten.");
        Require((await second.GetByIdAsync(id, CancellationToken.None).ConfigureAwait(false))?.Total == 30, "Second model was redirected or overwritten.");
        var failure = new InvalidOperationException("Injected failure in same-host second save");
        var observed = false;
        try
        {
            await Capability(events).AppendWithProjectionsAsync(id, AggregateType, Batch(id, 1), 0,
                async (transaction, token) => { await Apply(transaction, 7, token).ConfigureAwait(false); throw failure; },
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (InvalidOperationException exception) when (ReferenceEquals(exception, failure)) { observed = true; }
        Require(observed, "Handler failure must propagate.");
        Require((await first.GetByIdAsync(id, CancellationToken.None).ConfigureAwait(false))?.Total == 3, "First model update escaped rollback.");
        Require((await second.GetByIdAsync(id, CancellationToken.None).ConfigureAwait(false))?.Total == 30, "Second model update escaped rollback.");
        Require((await events.LoadAsync(id, AggregateType, CancellationToken.None).ConfigureAwait(false)).Count == 1, "Failed second save changed the stream.");
    }

    /// <summary>Verifies that an append publishes its events and model together and supplies complete prior history.</summary>
    /// <returns>The verification task.</returns>
    public virtual async Task Commit_events_and_complete_history_model()
    {
        RecordArmExecuted(nameof(Commit_events_and_complete_history_model));
        var events = await CreateEventStoreAsync().ConfigureAwait(false);
        var projection = await CreateProjectionStoreAsync().ConfigureAwait(false);
        var id = Guid.NewGuid().ToString("N");
        IReadOnlyList<IDomainEvent> seed = [TestDomainEvent.Create(id, 0), TestDomainEvent.Create(id, 1)];
        Require((await events.AppendAsync(id, AggregateType, seed, -1, CancellationToken.None).ConfigureAwait(false)).Success,
            "The initial plain append must succeed.");
        var persisted = await events.LoadAsync(id, AggregateType, CancellationToken.None).ConfigureAwait(false);
        Require(persisted.Count == 2, "The seed history must be durable before the transactional append.");
        var expectedTotal = persisted.Sum(row => row.EventData!.Length) + 1;
        var result = await Capability(events).AppendWithProjectionsAsync(id, AggregateType, Batch(id, 2), 1,
            async (transaction, token) =>
            {
                Require(transaction.History.Count == 2, "The callback must receive all prior events, excluding pending events.");
                for (var index = 0; index < seed.Count; index++)
                {
                    var row = transaction.History[index];
                    var original = persisted[index];
                    Require(row.EventId == seed[index].EventId && row.AggregateId == id
                        && row.AggregateType == AggregateType && row.Version == index
                        && row.TenantId == transaction.TenantId && row.EventType == original.EventType
                        && row.Timestamp == original.Timestamp,
                        "History must preserve the ordered persisted event envelope and tenant.");
                    Require(row.EventData is not null && original.EventData is not null
                        && row.EventData.AsSpan().SequenceEqual(original.EventData),
                        "History must preserve the original event payload bytes.");
                }
                var total = transaction.History.Sum(row => row.EventData!.Length) + 1;
                await WriteAsync(projection, transaction, id, total, token).ConfigureAwait(false);
            }, CancellationToken.None).ConfigureAwait(false);
        Require(result.Success && result.Outcome != AppendOutcome.AlreadyCommitted, "A new append must be acknowledged as newly committed.");
        Require((await events.LoadAsync(id, AggregateType, CancellationToken.None).ConfigureAwait(false)).Count == 3, "All three events must be durable.");
        Require((await projection.GetByIdAsync(id, CancellationToken.None).ConfigureAwait(false))?.Total == expectedTotal, "The model derived from history must be durable.");
    }

    /// <summary>Verifies rollback of multiple enlisted destinations and the event append after a handler failure.</summary>
    /// <returns>The verification task.</returns>
    public virtual async Task Roll_back_every_destination_when_handler_fails()
    {
        RecordArmExecuted(nameof(Roll_back_every_destination_when_handler_fails));
        var events = await CreateEventStoreAsync().ConfigureAwait(false);
        var first = await CreateProjectionStoreAsync().ConfigureAwait(false);
        var second = await CreateProjectionStoreAsync().ConfigureAwait(false);
        var id = Guid.NewGuid().ToString("N");
        var failure = new InvalidOperationException("Injected failure after both enlisted writes");
        var observed = false;
        try
        {
            await Capability(events).AppendWithProjectionsAsync(id, AggregateType, Batch(id, 0), -1,
                async (transaction, token) =>
                {
                    await WriteAsync(first, transaction, id, 1, token).ConfigureAwait(false);
                    await WriteAsync(second, transaction, id, 1, token).ConfigureAwait(false);
                    throw failure;
                }, CancellationToken.None).ConfigureAwait(false);
        }
        catch (InvalidOperationException exception) when (ReferenceEquals(exception, failure)) { observed = true; }
        Require(observed, "The original handler failure must propagate.");
        Require((await events.LoadAsync(id, AggregateType, CancellationToken.None).ConfigureAwait(false)).Count == 0, "Failed append must leave no events.");
        Require(await first.GetByIdAsync(id, CancellationToken.None).ConfigureAwait(false) is null, "First enlisted write must roll back.");
        Require(await second.GetByIdAsync(id, CancellationToken.None).ConfigureAwait(false) is null, "Second enlisted write must roll back.");
    }

    /// <summary>Verifies stale optimistic concurrency rejects before invoking projection handlers.</summary>
    /// <returns>The verification task.</returns>
    public virtual async Task Reject_stale_version_without_running_handler()
    {
        RecordArmExecuted(nameof(Reject_stale_version_without_running_handler));
        var events = await CreateEventStoreAsync().ConfigureAwait(false);
        var id = Guid.NewGuid().ToString("N");
        Require((await events.AppendAsync(id, AggregateType, Batch(id, 0), -1, CancellationToken.None).ConfigureAwait(false)).Success, "Seed append failed.");
        var called = false;
        var result = await Capability(events).AppendWithProjectionsAsync(id, AggregateType, Batch(id, 1), -1,
            (_, _) => { called = true; return ValueTask.CompletedTask; }, CancellationToken.None).ConfigureAwait(false);
        Require(result.IsConcurrencyConflict && !called, "Stale version must conflict without executing the callback.");
        Require((await events.LoadAsync(id, AggregateType, CancellationToken.None).ConfigureAwait(false)).Count == 1, "Rejected append changed the stream.");
    }

    /// <summary>Verifies recognized event identities do not attest participation of a new projection.</summary>
    /// <returns>The verification task.</returns>
    public virtual async Task Refuse_to_attest_projection_for_prior_plain_append()
    {
        RecordArmExecuted(nameof(Refuse_to_attest_projection_for_prior_plain_append));
        var events = await CreateEventStoreAsync().ConfigureAwait(false);
        var id = Guid.NewGuid().ToString("N");
        var batch = Batch(id, 0);
        Require((await events.AppendAsync(id, AggregateType, batch, -1, CancellationToken.None).ConfigureAwait(false)).Success, "Seed append failed.");
        var called = false;
        var result = await Capability(events).AppendWithProjectionsAsync(id, AggregateType, batch, -1,
            (_, _) => { called = true; return ValueTask.CompletedTask; }, CancellationToken.None).ConfigureAwait(false);
        Require(result.Outcome == AppendOutcome.Unknown && !called, "Prior event identities cannot certify the current projection participated.");
        Require((await events.LoadAsync(id, AggregateType, CancellationToken.None).ConfigureAwait(false)).Count == 1, "Retry duplicated the original event.");
    }

    /// <summary>Verifies retirement prevents a later transactional save from recreating the model.</summary>
    /// <returns>The verification task.</returns>
    public virtual async Task Preserve_retirement_and_roll_back_append()
    {
        RecordArmExecuted(nameof(Preserve_retirement_and_roll_back_append));
        var events = await CreateEventStoreAsync().ConfigureAwait(false);
        var projection = await CreateProjectionStoreAsync().ConfigureAwait(false);
        var id = Guid.NewGuid().ToString("N");
        await projection.DeleteAsync(id, CancellationToken.None).ConfigureAwait(false);
        var rejected = false;
        try
        {
            var result = await Capability(events).AppendWithProjectionsAsync(id, AggregateType, Batch(id, 0), -1,
                (transaction, token) => WriteAsync(projection, transaction, id, 1, token), CancellationToken.None).ConfigureAwait(false);
            rejected = !result.Success && result.Outcome != AppendOutcome.Unknown;
        }
        catch (Exception exception) when (exception is not TestFixtureAssertionException and not OperationCanceledException) { rejected = true; }
        Require(rejected, "Retired destination must explicitly reject the write.");
        Require(await projection.GetByIdAsync(id, CancellationToken.None).ConfigureAwait(false) is null, "Retired payload was resurrected.");
        Require((await events.LoadAsync(id, AggregateType, CancellationToken.None).ConfigureAwait(false)).Count == 0, "Refused projection must roll back the event append.");
    }

    private static ITransactionalProjectionEventStore Capability(IEventStore store) =>
        ValidateCapability<ITransactionalProjectionEventStore>(store, store.GetService(typeof(ITransactionalProjectionEventStore)))
        ?? throw new TestFixtureAssertionException("Transactional event capability is required; this suite cannot skip it.");

    private static ValueTask WriteAsync(IProjectionStore<ConformanceProjection> store, ProjectionTransaction transaction,
        string id, int total, CancellationToken token)
    {
        var capability = ValidateCapability<ITransactionalProjectionStore<ConformanceProjection>>(store,
            store.GetService(typeof(ITransactionalProjectionStore<ConformanceProjection>)))
            ?? throw new TestFixtureAssertionException("Transactional projection capability is required; this suite cannot skip it.");
        return capability.WriteAsync(transaction, id, AggregateType,
            capability.Serialize(new ConformanceProjection { Id = id, Total = total }), token);
    }

    private static IReadOnlyList<IDomainEvent> Batch(string id, long version) => [TestDomainEvent.Create(id, version)];

    private static void Require(bool condition, string message)
    {
        if (!condition) { throw new TestFixtureAssertionException(message); }
    }
}
