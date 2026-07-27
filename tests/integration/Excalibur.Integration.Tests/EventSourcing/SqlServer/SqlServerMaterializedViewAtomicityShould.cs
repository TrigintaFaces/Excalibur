// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.SqlServer;
using Excalibur.Integration.Tests.Data.Outbox;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.EventSourcing.SqlServer;

/// <summary>
/// Author≠impl exactly-once crash-atomicity regression lock for iqx3x3 — the SQL Server
/// <see cref="SqlServerMaterializedViewStore"/> persists the view mutation and the checkpoint advance in ONE
/// transaction, so a crash between them can never leave the view updated while the position lags (the
/// two-call double-count root cause). Exactly-once = SqlServer/Postgres/Mongo(txn) per SA's per-engine ruling.
/// </summary>
/// <remarks>
/// <b>verify-against-real-infra-not-mock:</b> runs against a real SQL Server (TestContainers) so the atomic
/// transaction commit/rollback is evaluated by the real engine — a mock cannot reproduce it. NON-SKIPPED
/// (<c>DockerAvailable.ShouldBeTrue</c>). Uses the store's internal <c>OnAfterViewBeforePositionAsync</c>
/// fault-hook (SA's fault-seam ruling) to inject a crash AFTER the view upsert and BEFORE the position
/// advance, within the same transaction.
/// <para>
/// <b>RED-on-mutant:</b> revert to a non-atomic two-call save (view upsert committed, THEN a separate
/// position upsert) ⇒ the injected crash leaves the view persisted while the position never advances ⇒
/// <see cref="CrashBetweenViewAndPosition_RollsBackBoth_NoPartialState"/> sees the view on a fresh instance
/// → RED (and a real crash would double-count on replay).
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerMaterializedViewAtomicityShould : IClassFixture<SqlServerOutboxStoreContainerFixture>
{
	private const string ViewType = "CounterView";

	private readonly SqlServerOutboxStoreContainerFixture _fixture;

	public SqlServerMaterializedViewAtomicityShould(SqlServerOutboxStoreContainerFixture fixture) => _fixture = fixture;

	private sealed record CounterView(int Count);

	private SqlServerMaterializedViewStore NewStore() =>
		new(() => new SqlConnection(_fixture.ConnectionString), NullLogger<SqlServerMaterializedViewStore>.Instance);

	private async Task<SqlServerMaterializedViewStore> ReadyStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"iqx3x3 exactly-once atomicity is a data-integrity safety control — this real-SqlServer lock must never be skipped");
		var store = NewStore();
		await store.EnsureSchemaAsync(CancellationToken.None).ConfigureAwait(false);
		return store;
	}

	[Fact]
	public async Task CrashBetweenViewAndPosition_RollsBackBoth_NoPartialState()
	{
		var store = await ReadyStoreAsync().ConfigureAwait(false);
		var viewName = "atomicity_" + Guid.NewGuid().ToString("N");
		var viewId = "agg-1";

		// Inject a crash AFTER the view upsert, BEFORE the position advance — inside the transaction.
		store.OnAfterViewBeforePositionAsync = _ => throw new InvalidOperationException("simulated crash between view and position");

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => store.SaveViewAndPositionAsync(viewName, viewId, new CounterView(1), 5, CancellationToken.None).AsTask())
			.ConfigureAwait(false);

		// Fresh instance (no fault hook): the transaction must have rolled back BOTH writes — neither the view
		// nor the position may be visible. A non-atomic two-call impl would leave the view persisted here.
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

		// A fresh instance observes BOTH the view and the position — committed together.
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
		// A superseded/replayed lower position must not roll the checkpoint backwards.
		await store.SaveViewAndPositionAsync(viewName, viewId, new CounterView(2), 4, CancellationToken.None).ConfigureAwait(false);

		var fresh = NewStore();
		(await fresh.GetPositionAsync(viewName, CancellationToken.None).ConfigureAwait(false))
			.ShouldBe(10, "a lower incoming position must never overwrite a higher stored checkpoint (monotonic)");
	}

	// bd-ir6pn1 regression lock: the STANDALONE position-only writer (SavePositionAsync) must ALSO be
	// monotonic — it is the second writer of the checkpoint row, and a guarantee held by only one of a
	// state's writers is not held. The pre-fix SavePositionAsync overwrote unconditionally, so a delayed or
	// retried lower position rewound the checkpoint and replayed applied events. RED if the MERGE guard
	// `source.Position > target.Position` is removed.
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
}
