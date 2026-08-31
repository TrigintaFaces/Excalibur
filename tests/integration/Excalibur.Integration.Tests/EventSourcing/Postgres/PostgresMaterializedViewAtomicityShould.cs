// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Postgres;
using Excalibur.Integration.Tests.Data.Outbox;

using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.EventSourcing.Postgres;

/// <summary>
/// Author≠impl exactly-once crash-atomicity regression lock for iqx3x3 — the Postgres
/// <see cref="PostgresMaterializedViewStore"/> persists the view mutation and the checkpoint advance in ONE
/// transaction, so a crash between them can never leave the view updated while the position lags. The
/// Postgres sibling of the SqlServer atomicity lock (exactly-once per SA's per-engine ruling).
/// </summary>
/// <remarks>
/// <b>verify-against-real-infra-not-mock:</b> real Postgres (TestContainers); the atomic transaction is
/// evaluated by the real engine. NON-SKIPPED. Uses the store's internal <c>OnAfterViewBeforePositionAsync</c>
/// fault-hook (SA's fault-seam) to inject a crash AFTER the view upsert and BEFORE the position advance,
/// within the same transaction. The store has no schema-init API, so the test creates the two tables.
/// <para>
/// <b>RED-on-mutant:</b> a non-atomic two-call save leaves the view persisted after the injected crash ⇒
/// <see cref="CrashBetweenViewAndPosition_RollsBackBoth_NoPartialState"/> sees it on a fresh instance → RED.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresMaterializedViewAtomicityShould : IClassFixture<PostgresOutboxStoreContainerFixture>, IDisposable
{
	private readonly PostgresOutboxStoreContainerFixture _fixture;
	private NpgsqlDataSource? _dataSource;

	public PostgresMaterializedViewAtomicityShould(PostgresOutboxStoreContainerFixture fixture) => _fixture = fixture;

	private sealed record CounterView(int Count);

	private NpgsqlDataSource DataSource => _dataSource ??= NpgsqlDataSource.Create(_fixture.ConnectionString);

	private PostgresMaterializedViewStore NewStore() =>
		new(DataSource, NullLogger<PostgresMaterializedViewStore>.Instance, TenantViewFixture.SingleTenant);

	private async Task<PostgresMaterializedViewStore> ReadyStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"iqx3x3 exactly-once atomicity is a data-integrity safety control — this real-Postgres lock must never be skipped");

		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		await using (var cmd = connection.CreateCommand())
		{
			cmd.CommandText = """
				CREATE TABLE IF NOT EXISTS materialized_views (
					tenant_id  TEXT NOT NULL,
					view_name  TEXT NOT NULL,
					view_id    TEXT NOT NULL,
					data       JSONB NOT NULL,
					created_at TIMESTAMPTZ NOT NULL,
					updated_at TIMESTAMPTZ NOT NULL,
					PRIMARY KEY (tenant_id, view_name, view_id)
				);
				CREATE TABLE IF NOT EXISTS materialized_view_positions (
					tenant_id  TEXT NOT NULL,
					view_name  TEXT NOT NULL,
					position   BIGINT NOT NULL,
					created_at TIMESTAMPTZ NOT NULL,
					updated_at TIMESTAMPTZ NOT NULL,
					PRIMARY KEY (tenant_id, view_name)
				);
				""";
			_ = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		return NewStore();
	}

	[Fact]
	public async Task CrashBetweenViewAndPosition_RollsBackBoth_NoPartialState()
	{
		var store = await ReadyStoreAsync().ConfigureAwait(false);
		var viewName = "atomicity_" + Guid.NewGuid().ToString("N");
		var viewId = "agg-1";

		store.OnAfterViewBeforePositionAsync = _ => throw new InvalidOperationException("simulated crash between view and position");

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => store.SaveViewAndPositionAsync(viewName, viewId, new CounterView(1), 5, CancellationToken.None).AsTask())
			.ConfigureAwait(false);

		var fresh = NewStore();
		(await fresh.GetAsync<CounterView>(viewName, viewId, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeNull("the view mutation must roll back with the failed transaction (no partial state → no double-count on replay)");
		(await fresh.GetPositionAsync(viewName, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeNull("the checkpoint must not advance when the transaction rolled back");
	}

	[Fact]
	public async Task SaveViewAndPosition_AtomicallyPersistsBoth_OnASingleCall()
	{
		var store = await ReadyStoreAsync().ConfigureAwait(false);
		var viewName = "atomicity_" + Guid.NewGuid().ToString("N");
		var viewId = "agg-2";

		await store.SaveViewAndPositionAsync(viewName, viewId, new CounterView(7), 10, CancellationToken.None).ConfigureAwait(false);

		var fresh = NewStore();
		(await fresh.GetAsync<CounterView>(viewName, viewId, CancellationToken.None).ConfigureAwait(false))
			.ShouldBe(new CounterView(7), "the view must be durably persisted");
		(await fresh.GetPositionAsync(viewName, CancellationToken.None).ConfigureAwait(false))
			.ShouldBe(10, "the checkpoint must advance atomically with the view");
	}

	[Fact]
	public async Task SaveViewAndPosition_IsMonotonic_ALowerPositionNeverLowersTheCheckpoint()
	{
		var store = await ReadyStoreAsync().ConfigureAwait(false);
		var viewName = "atomicity_" + Guid.NewGuid().ToString("N");
		var viewId = "agg-3";

		await store.SaveViewAndPositionAsync(viewName, viewId, new CounterView(1), 10, CancellationToken.None).ConfigureAwait(false);
		await store.SaveViewAndPositionAsync(viewName, viewId, new CounterView(2), 4, CancellationToken.None).ConfigureAwait(false);

		var fresh = NewStore();
		(await fresh.GetPositionAsync(viewName, CancellationToken.None).ConfigureAwait(false))
			.ShouldBe(10, "a lower incoming position must never overwrite a higher stored checkpoint (monotonic)");
	}

	// bd-ir6pn1 regression lock: the STANDALONE position-only writer (SavePositionAsync) must ALSO be
	// monotonic — it is the second writer of the checkpoint row, and a guarantee held by only one of a
	// state's writers is not held. The pre-fix SavePositionAsync overwrote unconditionally, so a delayed or
	// retried lower position rewound the checkpoint and replayed applied events. RED if the ON CONFLICT
	// guard `WHERE position < EXCLUDED.position` is removed.
	[Fact]
	public async Task SavePositionAsync_IsMonotonic_ALowerPositionNeverLowersTheCheckpoint()
	{
		var store = await ReadyStoreAsync().ConfigureAwait(false);
		var viewName = "atomicity_" + Guid.NewGuid().ToString("N");

		await store.SavePositionAsync(viewName, 10, CancellationToken.None).ConfigureAwait(false);
		// A superseded/replayed lower position from the position-only writer must not roll the checkpoint back.
		await store.SavePositionAsync(viewName, 4, CancellationToken.None).ConfigureAwait(false);

		var fresh = NewStore();
		(await fresh.GetPositionAsync(viewName, CancellationToken.None).ConfigureAwait(false))
			.ShouldBe(10, "a lower incoming position via SavePositionAsync must never rewind a higher checkpoint (monotonic)");
	}

	public void Dispose() => _dataSource?.Dispose();

	/// <summary>
	/// The ambient tenant these constructions run under. The store resolves its partition from here rather
	/// than from a parameter, so a caller can neither widen a lookup by omitting a tenant nor redirect it by
	/// naming another.
	/// </summary>
	private sealed class TenantViewFixture : ITenantContext
	{
		public static ITenantContext SingleTenant { get; } = new TenantViewFixture();

		public string? TenantId => "tenant-a";

		public bool HasTenant => true;
	}

}
