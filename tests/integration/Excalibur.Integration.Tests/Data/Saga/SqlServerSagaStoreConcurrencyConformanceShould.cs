// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Threading;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Excalibur.Saga.SqlServer;
using Excalibur.Saga.SqlServer.Requests;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared.Conformance.Saga;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Optimistic-concurrency conformance for the SQL Server saga store (mxozhv / keystone fc1c8a). Author≠impl
/// (TestsDeveloper); runs the shared <see cref="SagaStoreConformanceTestBase"/> contract with
/// optimistic concurrency required of every store, so the version-gated <c>no-overwrite</c> and
/// <c>no-resurrect</c> facts are enforced against a real SQL Server container.
/// </summary>
/// <remarks>
/// The store performs a store-owns-increment compare-and-swap via a version-gated <c>MERGE</c>
/// (<c>SqlServerSagaStore.SaveAsync</c> surfaces a 0-row MERGE as <see cref="ConcurrencyException"/>),
/// and scopes loads to <c>{SagaId, SagaType}</c> for type-isolation — so both the optimistic-concurrency
/// and the type-isolation conformance facts hold. A truncated table per test gives isolation on the
/// shared container.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "SqlServer")]
[Collection("SqlServer SagaStore Integration Tests")]
public sealed class SqlServerSagaStoreConcurrencyConformanceShould : SagaStoreConformanceTestBase, IClassFixture<SqlServerSagaStoreContainerFixture>
{
	private readonly SqlServerSagaStoreContainerFixture _fixture;

	public SqlServerSagaStoreConcurrencyConformanceShould(SqlServerSagaStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}


	/// <inheritdoc/>
	protected override async Task<ISagaStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available — this real-infra conformance lock is never skipped.");

		// The store does NOT auto-create its table; the fixture provisions [dispatch].[sagas] (the store's
		// default schema/table), so the simple connection-string constructor resolves to it.
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		return new SqlServerSagaStore(
			_fixture.ConnectionString,
			NullLogger<SqlServerSagaStore>.Instance,
			new DispatchJsonSerializer(),
			SingleTenantTestContext.Instance);
	}

	/// <inheritdoc/>
	protected override Task CleanupAsync() => _fixture.CleanupTableAsync();

	/// <summary>
	/// c4i8n7 (Lamport audit — find-or-create race): under CONCURRENT first-saves of the SAME new saga id, the
	/// version-gated <c>MERGE</c> must not let two savers both evaluate <c>WHEN NOT MATCHED</c> and both INSERT —
	/// a raw PRIMARY KEY violation. <c>WITH (HOLDLOCK)</c> takes a serializable key-range lock so the saves
	/// serialize: exactly one INSERTs the row; every other saver re-evaluates against the now-present row
	/// (<c>Version 1 ≠ @ExpectedVersion 0</c>) → 0-row MERGE → the graceful <see cref="ConcurrencyException"/>.
	/// </summary>
	/// <remarks>
	/// SAFETY: no loser throws a raw PK-violation <c>SqlException</c> — every failure is
	/// <see cref="ConcurrencyException"/> (the store surfaces a 0-row MERGE as that; a PK violation would
	/// propagate uncaught). LIVENESS (paired): exactly ONE saver wins and persists a single row at
	/// <c>Version 1</c> — the find-or-create really succeeds, never a total stall (which would pass SAFETY
	/// vacuously). RED against the pre-fix MERGE (no HOLDLOCK): the concurrent <c>WHEN NOT MATCHED</c>
	/// double-INSERT surfaces a PRIMARY KEY <c>SqlException</c>, which is not a <see cref="ConcurrencyException"/>.
	/// </remarks>
	[Fact]
	public async Task ConcurrentFindOrCreate_OfTheSameNewSaga_InsertsOneRow_LosersGetConcurrencyException_NeverAPkViolation()
	{
		const int Concurrency = 32;
		var sagaId = Guid.NewGuid();

		// Force TRUE simultaneity so multiple savers hit the WHEN NOT MATCHED window together — the exact
		// interleaving HOLDLOCK must serialize. A naive Task.Run fan-out lets the thread pool ramp slowly and
		// the first save auto-commit before the others read (they then see MATCHED, not a race). So: (1) ensure
		// the pool already has enough threads, and (2) rendezvous all savers at a Barrier and release them at the
		// same instant. Pre-fix (no HOLDLOCK) this reliably drives ≥2 concurrent INSERTs → a PRIMARY KEY violation.
		ThreadPool.GetMinThreads(out var workerThreads, out var completionThreads);
		ThreadPool.SetMinThreads(Math.Max(workerThreads, Concurrency + 4), Math.Max(completionThreads, Concurrency + 4));

		using var startGate = new Barrier(Concurrency);
		var savers = Enumerable.Range(0, Concurrency).Select(_ => Task.Run(async () =>
		{
			var state = CreateTestSagaState(sagaId);
			startGate.SignalAndWait();
			try
			{
				await Store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);
				return (Exception?)null;
			}
			catch (Exception ex)
			{
				return ex;
			}
		})).ToArray();

		var outcomes = await Task.WhenAll(savers).ConfigureAwait(false);

		// SAFETY — no loser hit a raw PRIMARY KEY violation; every failure is the graceful ConcurrencyException.
		foreach (var failure in outcomes.Where(outcome => outcome is not null))
		{
			_ = failure.ShouldBeOfType<ConcurrencyException>(
				"under concurrent find-or-create the losers MUST fail with the graceful 0-row ConcurrencyException — "
				+ "never a raw PRIMARY KEY-violation SqlException. HOLDLOCK serializes the WHEN NOT MATCHED evaluation "
				+ $"so only one INSERT happens (actual failure: {failure}).");
		}

		// LIVENESS — exactly one winner, and exactly one row persisted at Version 1 (find-or-create really succeeded).
		outcomes.Count(outcome => outcome is null).ShouldBe(1,
			"exactly one concurrent find-or-create must win and INSERT the single row — a total stall would satisfy "
			+ "the safety arm vacuously.");

		var loaded = await Store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		_ = loaded.ShouldNotBeNull("the single find-or-create winner's saga must be persisted and loadable.");
		loaded.Version.ShouldBe(1, "the persisted saga is at Version 1 — a single INSERT, no double-write.");
	}

	/// <summary>
	/// c4i8n7 SAFETY arm (SA-ruled deterministic barrier) — the N-thread race relies on a sub-millisecond
	/// window that does not reliably fire, so the double-INSERT is forced with TWO explicit transactions and a
	/// controlled commit order (no timing dependence): session A runs the find-or-create MERGE for <c>X</c> and
	/// holds its transaction open; session B runs the SAME MERGE; then A commits and B is observed.
	/// </summary>
	/// <remarks>
	/// The MERGE executed is the STORE's OWN generated SQL, extracted from <see cref="SaveSagaRequest{TSagaState}"/>,
	/// so the fix-sensitive guard is a mechanism assertion on that SQL (below). <b>Empirical finding</b> (verified
	/// per SA #34919): the 2-session <i>behavioral</i> RED does NOT fire — holding A's transaction open makes B's
	/// existence-check SELECT BLOCK on A's uncommitted-INSERT exclusive lock under READ COMMITTED, so B serializes
	/// and no-ops <i>with or without</i> HOLDLOCK. The true both-not-matched double-INSERT is a sub-millisecond
	/// auto-commit window that no held-transaction barrier can force. So the SAFETY guard is the
	/// <c>WITH (HOLDLOCK)</c> mechanism assertion (RED if removed); the interleave here is a real-infra
	/// no-double-insert LIVENESS exercise, paired with the 32-concurrent forward-regression arm above.
	/// </remarks>
	[Fact]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("AOT", "IL2026:RequiresUnreferencedCode", Justification = "Integration test; JSON serialization of the saga state is intentional.")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Integration test; JSON serialization of the saga state is intentional.")]
	public async Task ConcurrentFindOrCreate_UnderAForcedTwoSessionInterleave_DoesNotPrimaryKeyViolate()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available — this real-infra concurrency lock is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		const string QualifiedTableName = "[dispatch].[sagas]";
		var sagaId = Guid.NewGuid();

		// Extract the STORE's ACTUAL generated find-or-create MERGE.
		var request = new SaveSagaRequest<TestSagaState>(
			CreateTestSagaState(sagaId), new DispatchJsonSerializer(), QualifiedTableName, TenantScope.Untenanted, CancellationToken.None);
		var mergeSql = request.Command.CommandText;
		var mergeParameters = request.Parameters;

		// SAFETY (fix-sensitive RED, per SA's #34919 fallback) — the behavioral 2-session RED does NOT fire:
		// verified empirically that holding A's transaction open makes B's existence-check SELECT BLOCK on A's
		// uncommitted-INSERT exclusive lock (READ COMMITTED) — so B serializes and no-ops WITH OR WITHOUT HOLDLOCK
		// (the true both-not-matched race is a sub-ms auto-commit window no held-transaction barrier can force).
		// The load-bearing guard is therefore asserted on the store's GENERATED SQL: remove WITH (HOLDLOCK) from
		// the find-or-create MERGE and this RED-detects it. The interleave below remains a real-infra no-double-
		// insert LIVENESS exercise, paired with the 32-concurrent forward-regression arm.
		mergeSql.Contains("HOLDLOCK", StringComparison.Ordinal).ShouldBeTrue(
			"the find-or-create MERGE MUST carry WITH (HOLDLOCK) — the serializable key-range lock is the only "
			+ "guard against two concurrent WHEN-NOT-MATCHED savers both INSERTing (a PRIMARY KEY violation). "
			+ "Removing it re-opens the at-most-once-per-claim hole.");

		await using var connectionA = new SqlConnection(_fixture.ConnectionString);
		await using var connectionB = new SqlConnection(_fixture.ConnectionString);
		await connectionA.OpenAsync().ConfigureAwait(false);
		await connectionB.OpenAsync().ConfigureAwait(false);

		var transactionA = (SqlTransaction)await connectionA.BeginTransactionAsync().ConfigureAwait(false);
		var transactionB = (SqlTransaction)await connectionB.BeginTransactionAsync().ConfigureAwait(false);

		// Session A inserts X and holds the transaction open (does NOT commit) — holds the lock.
		_ = await connectionA.ExecuteAsync(new CommandDefinition(mergeSql, mergeParameters, transactionA)).ConfigureAwait(false);

		// Session B runs the SAME MERGE for X. It blocks (HOLDLOCK: at its existence check; pre-fix: at its
		// INSERT on A's PK lock) — either way it cannot complete until A commits.
		var sessionBMerge = connectionB.ExecuteAsync(new CommandDefinition(mergeSql, mergeParameters, transactionB));

		// Controlled commit order (deterministic, not a race): let B reach its blocking point, THEN commit A.
		await Task.Delay(TimeSpan.FromMilliseconds(750)).ConfigureAwait(false);
		await transactionA.CommitAsync().ConfigureAwait(false);

		Exception? sessionBFailure = null;
		try
		{
			_ = await sessionBMerge.ConfigureAwait(false);
			await transactionB.CommitAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			sessionBFailure = ex;
			try { await transactionB.RollbackAsync().ConfigureAwait(false); }
			catch (InvalidOperationException) { /* transaction already resolved by the failed batch */ }
		}
		finally
		{
			await transactionA.DisposeAsync().ConfigureAwait(false);
			await transactionB.DisposeAsync().ConfigureAwait(false);
		}

		// SAFETY — with WITH (HOLDLOCK), B serializes behind A and no-ops; it must NOT hit a PRIMARY KEY
		// violation. A 2627/2601 here is the pre-fix double-INSERT race (RED-on-pre-fix).
		var isPrimaryKeyViolation = sessionBFailure is SqlException sqlException
			&& sqlException.Number is 2627 or 2601;
		isPrimaryKeyViolation.ShouldBeFalse(
			"the forced two-session find-or-create interleave must NOT hit a PRIMARY KEY violation — WITH (HOLDLOCK) "
			+ "makes session B block at its existence check, observe A's committed row, and no-op. A 2627/2601 is the "
			+ "pre-fix double-INSERT race the HOLDLOCK closes. Session B outcome: "
			+ (sessionBFailure?.ToString() ?? "no exception (blocked, then MATCHED)"));

		// LIVENESS — exactly one row persisted for the contended id (A won; B did not duplicate it).
		await using var verifyConnection = new SqlConnection(_fixture.ConnectionString);
		await verifyConnection.OpenAsync().ConfigureAwait(false);
		var rowCount = await verifyConnection.ExecuteScalarAsync<int>(
			$"SELECT COUNT(*) FROM {QualifiedTableName} WHERE SagaId = @SagaId",
			new { SagaId = sagaId }).ConfigureAwait(false);
		rowCount.ShouldBe(1, "exactly one saga row must exist for the contended id — a single find-or-create winner.");
	}
    [Fact]
    public async Task ObserveActualLegacyNonceTwiceThenConditionallyInitializeFirstJournal()
    {
        var id = await InsertActualLegacyStateAsync().ConfigureAwait(false);
        var first = (await Store.LoadAsync<TestSagaState>(id, CancellationToken.None).ConfigureAwait(false)).ShouldNotBeNull();
        var second = (await Store.LoadAsync<TestSagaState>(id, CancellationToken.None).ConfigureAwait(false)).ShouldNotBeNull();
        first.TimerRecoveryIncarnationId.ShouldBe(Guid.Empty);
        second.TimerRecoveryIncarnationId.ShouldBe(Guid.Empty);
        first.Version.ShouldBe(second.Version);
        // This is the coordinator's pre-capture initialization: never mint a new nonce for an already persisted journal.
        first.TimerRecoveryIncarnationId = Guid.NewGuid();
        first.TimerRecoverySequence = 1;
        var journal = new SagaTimerRecoveryJournal(new SagaScopeIdentity(TenantDefaults.DefaultTenantId, nameof(TestSagaState), id),
            first.TimerRecoveryIncarnationId, 1, Guid.NewGuid(), Guid.NewGuid(), 0, 1,
            [1, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 0]);
        first.PendingTimerRecovery = journal;
        await Store.SaveAsync(first, CancellationToken.None).ConfigureAwait(false);
        var persisted = (await Store.LoadAsync<TestSagaState>(id, CancellationToken.None).ConfigureAwait(false)).ShouldNotBeNull();
        persisted.TimerRecoveryIncarnationId.ShouldBe(first.TimerRecoveryIncarnationId);
        persisted.PendingTimerRecovery.ShouldNotBeNull().GetAuthorityDigest().ShouldBe(journal.GetAuthorityDigest());
        await Should.ThrowAsync<ConcurrencyException>(() => Store.SaveAsync(second, CancellationToken.None)).ConfigureAwait(false);
        var afterStale = (await Store.LoadAsync<TestSagaState>(id, CancellationToken.None).ConfigureAwait(false)).ShouldNotBeNull();
        afterStale.Version.ShouldBe(persisted.Version);
        afterStale.PendingTimerRecovery.ShouldNotBeNull().GetAuthorityDigest().ShouldBe(journal.GetAuthorityDigest());
    }

    [Fact]
    public async Task PermitOneOfTwoActualLegacyInitializersAndRefuseTheStaleEmptyObservation()
    {
        var id = await InsertActualLegacyStateAsync().ConfigureAwait(false);
        var first = (await Store.LoadAsync<TestSagaState>(id, CancellationToken.None).ConfigureAwait(false)).ShouldNotBeNull();
        var second = (await Store.LoadAsync<TestSagaState>(id, CancellationToken.None).ConfigureAwait(false)).ShouldNotBeNull();
        first.TimerRecoveryIncarnationId.ShouldBe(Guid.Empty);
        second.TimerRecoveryIncarnationId.ShouldBe(Guid.Empty);
        using var ready = new CountdownEvent(2);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<Exception?> Attempt(TestSagaState state)
        {
            ready.Signal();
            await release.Task.ConfigureAwait(false);
            try { await Store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false); return null; }
            catch (ConcurrencyException error) { return error; }
        }
        var left = Attempt(first); var right = Attempt(second);
        ready.IsSet.ShouldBeTrue();
        release.SetResult();
        var outcomes = await Task.WhenAll(left, right).ConfigureAwait(false);
        outcomes.Count(error => error is null).ShouldBe(1);
        outcomes.Count(error => error is ConcurrencyException).ShouldBe(1);
        var persisted = (await Store.LoadAsync<TestSagaState>(id, CancellationToken.None).ConfigureAwait(false)).ShouldNotBeNull();
        persisted.Version.ShouldBe(2);
        persisted.TimerRecoveryIncarnationId.ShouldNotBe(Guid.Empty);
        var winner = outcomes[0] is null ? first : second;
        persisted.TimerRecoveryIncarnationId.ShouldBe(winner.TimerRecoveryIncarnationId);
    }

    private async Task<Guid> InsertActualLegacyStateAsync()
    {
        var id = Guid.NewGuid();
        using var serializer = new DispatchJsonSerializer();
        var payload = System.Text.Json.Nodes.JsonNode.Parse(serializer.Serialize(new TestSagaState { SagaId = id, Data = "actual-legacy" }))
            .ShouldNotBeNull().AsObject();
        string[] names = [nameof(SagaState.TimerRecoveryIncarnationId), nameof(SagaState.TimerRecoverySequence),
            nameof(SagaState.PendingTimerRecovery), nameof(SagaState.LastTimerRecoveryBatchId)];
        var removed = payload.Select(pair => pair.Key).Where(key => names.Any(name => string.Equals(name, key, StringComparison.OrdinalIgnoreCase))).ToArray();
        removed.Length.ShouldBe(3, "the default serializer omits the null pending journal and writes the other three new properties");
        foreach (var name in removed) { payload.Remove(name).ShouldBeTrue(); }
        payload.Select(pair => pair.Key).ShouldNotContain(key => names.Any(name => string.Equals(name, key, StringComparison.OrdinalIgnoreCase)));
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
        var inserted = await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO [dispatch].[sagas](SagaId,SagaType,StateJson,IsCompleted,TenantId,Version,TimerRecoveryIncarnation) VALUES(@Id,@Type,@Json,0,@Tenant,1,NULL)",
            new { Id = id, Type = nameof(TestSagaState), Json = payload.ToJsonString(), Tenant = TenantDefaults.DefaultTenantId },
            cancellationToken: CancellationToken.None)).ConfigureAwait(false);
        inserted.ShouldBe(1);
        return id;
    }

}
