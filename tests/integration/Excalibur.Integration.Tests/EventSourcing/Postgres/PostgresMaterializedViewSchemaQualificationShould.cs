// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;

using Excalibur.EventSourcing.Postgres;
using Excalibur.Integration.Tests.Data.Outbox;

using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

using Shouldly;

using Xunit;

// The DDL and the probe queries are composed from compile-time constants plus a GUID-derived view-name
// prefix; the one value that varies per call is bound as the @vn parameter. Same disposition as the
// sibling Postgres fixtures, which carry this pragma for the same reason.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities

namespace Excalibur.Integration.Tests.EventSourcing.Postgres;

/// <summary>
/// Makes the materialized-view store's schema qualification OBSERVABLE: every statement must address the
/// configured schema, not whatever the connection's <c>search_path</c> happens to resolve.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why no existing arm can see this, and it is not an oversight.</b> Every other arm in the tree runs
/// the store at its DEFAULT schema, and at the default schema <c>"public"."materialized_views"</c> and a
/// bare <c>materialized_views</c> resolve to the SAME TABLE — Postgres resolves the unqualified name
/// through <c>search_path</c>, which is <c>public</c>. Stripping the schema segment from the composed name
/// therefore leaves every default-schema arm green: the property is unobservable by construction there.
/// Only a NON-DEFAULT schema separates the two names.
/// </para>
/// <para>
/// <b>The decoy is the load-bearing part.</b> The probe schema holds the real tables; <c>public</c> holds
/// tables with the SAME NAMES. Without the decoy an unqualified store merely fails to find its table and
/// raises a loud error — the arm would then pass for the wrong reason (an exception, not a misdirected
/// write) and would not resemble the shipped failure mode at all. With the decoy present, an unqualified
/// store silently reads and writes the WRONG TABLE: the round trip through the store's own API still
/// succeeds, nothing downstream learns the read model is wrong, and only a direct look at both schemas
/// can tell. That silence is the defect class, so the arm has to assert on both schemas.
/// </para>
/// <para>
/// <b>Safety and liveness, both required.</b> SAFETY: no row for this run's view name lands in the
/// <c>public</c> decoys. LIVENESS: the row DOES land in the probe schema and reads back with the right
/// value. Safety alone is satisfied by a store that writes nothing at all — inaction is the cheapest way
/// to be safe and the most expensive way to be wrong.
/// </para>
/// <para>
/// <b>RED-on-mutant:</b> strip the schema segment from the composed name and both arms here go red on
/// their probe-schema count (0 rows, 1 expected) while the pre-existing default-schema materialized-view
/// arms stay green. The round trip through the store stays green under that mutation, which is exactly why
/// the direct two-schema assertions are the evidence and the round trip is not.
/// </para>
/// <para>
/// <b>verify-against-real-infra-not-mock:</b> real Postgres (TestContainers). Name resolution is decided
/// by the engine's <c>search_path</c>; a mocked connection returns whatever it was told and could not
/// distinguish the two names. NON-SKIPPED — a skip-gated arm here would re-open the gap it closes.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresMaterializedViewSchemaQualificationShould
	: IClassFixture<PostgresOutboxStoreContainerFixture>, IAsyncLifetime
{
	/// <summary>A schema that is NOT on the default <c>search_path</c>. That is the whole point of it.</summary>
	private const string ProbeSchema = "excalibur_mv_probe";

	/// <summary>The store's DEFAULT table names — the decoys must share them, or there is no decoy.</summary>
	private const string ViewTable = "materialized_views";
	private const string PositionTable = "materialized_view_positions";

	private readonly PostgresOutboxStoreContainerFixture _fixture;

	/// <summary>
	/// Unique per run, so the <c>public</c> decoy assertions are scoped to rows only THIS arm could have
	/// written. The decoy tables are also used by the sibling atomicity suite, so "the table is empty" would
	/// be a claim about other people's rows; "no row carries my view name" is a claim about mine.
	/// </summary>
	private readonly string _viewNamePrefix = "schemaprobe_" + Guid.NewGuid().ToString("N");

	private NpgsqlDataSource? _dataSource;

	public PostgresMaterializedViewSchemaQualificationShould(PostgresOutboxStoreContainerFixture fixture)
		=> _fixture = fixture;

	private sealed record CounterView(int Count);

	private NpgsqlDataSource DataSource => _dataSource ??= NpgsqlDataSource.Create(_fixture.ConnectionString);

	/// <summary>
	/// Configured with the PROBE schema. The table names stay at their defaults — identical to the decoys',
	/// which is what makes a missing qualification silent rather than loud.
	/// </summary>
	private PostgresMaterializedViewStore NewStore() =>
		new(
			DataSource,
			NullLogger<PostgresMaterializedViewStore>.Instance,
			ProbeTenant.Instance,
			viewTableName: null,
			positionTableName: null,
			jsonOptions: null,
			schemaName: ProbeSchema);

	public async ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Schema qualification is resolved by the engine's search_path, so this arm runs against a real "
			+ "PostgreSQL. It must never be skipped: a skipped arm leaves the property unobservable, which is "
			+ "the gap it exists to close.");

		await ExecuteAsync(
			"CREATE SCHEMA IF NOT EXISTS " + ProbeSchema + ";\n"
			+ ViewTableDdl(ProbeSchema)
			+ PositionTableDdl(ProbeSchema)
			// DECOYS. Same names, in public, which IS on the default search_path. An unqualified statement
			// lands here instead of raising, which is what makes the misdirection silent rather than loud.
			+ ViewTableDdl("public")
			+ PositionTableDdl("public")).ConfigureAwait(false);
	}

	/// <summary>
	/// Drops the probe schema so the arm is re-runnable, and removes only THIS run's rows from the shared
	/// public decoys. The decoy tables themselves are left standing: the sibling atomicity suite provisions
	/// and uses them, so dropping them would be cleaning up someone else's table.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		try
		{
			await ExecuteAsync(
				"DROP SCHEMA IF EXISTS " + ProbeSchema + " CASCADE;\n"
				+ "DELETE FROM public." + ViewTable + " WHERE view_name LIKE '" + _viewNamePrefix + "%';\n"
				+ "DELETE FROM public." + PositionTable + " WHERE view_name LIKE '" + _viewNamePrefix + "%';\n")
				.ConfigureAwait(false);
		}
		catch (NpgsqlException)
		{
			// Teardown against a torn-down container must not mask a real arm failure.
		}

		_dataSource?.Dispose();
	}

	[Fact]
	public async Task SaveView_LandsInTheConfiguredSchema_AndNeverInTheSameNamedPublicDecoy()
	{
		await AssertDecoysAreRealWriteTargetsAsync().ConfigureAwait(false);

		var store = NewStore();
		var viewName = _viewNamePrefix + "_view";
		const string ViewId = "agg-1";

		await store.SaveAsync(viewName, ViewId, new CounterView(42), CancellationToken.None).ConfigureAwait(false);

		// The store's OWN round trip. Deliberately NOT the evidence: an unqualified store writes the decoy
		// and reads the decoy, so this passes either way. It is here to prove the write happened at all.
		(await NewStore().GetAsync<CounterView>(viewName, ViewId, CancellationToken.None).ConfigureAwait(false))
			.ShouldBe(new CounterView(42), "the store must round-trip the view it was handed");

		// LIVENESS, in the right place: the probe schema holds the row, carrying the right value.
		(await ScalarAsync<long>(
			"SELECT count(*) FROM " + ProbeSchema + "." + ViewTable + " WHERE view_name = @vn", viewName)
			.ConfigureAwait(false))
			.ShouldBe(1L, "the view must be written into the configured schema '" + ProbeSchema + "'");
		(await ScalarAsync<int>(
			"SELECT (data->>'count')::int FROM " + ProbeSchema + "." + ViewTable + " WHERE view_name = @vn", viewName)
			.ConfigureAwait(false))
			.ShouldBe(42, "the row in the configured schema must carry the data, not merely exist");

		// SAFETY: nothing from this run reached the same-named table on the default search_path.
		(await ScalarAsync<long>(
			"SELECT count(*) FROM public." + ViewTable + " WHERE view_name = @vn", viewName)
			.ConfigureAwait(false))
			.ShouldBe(0L, "an unqualified statement resolves through search_path and writes public silently — no row may land there");
	}

	[Fact]
	public async Task SavePosition_LandsInTheConfiguredSchema_AndNeverInTheSameNamedPublicDecoy()
	{
		await AssertDecoysAreRealWriteTargetsAsync().ConfigureAwait(false);

		var store = NewStore();
		var viewName = _viewNamePrefix + "_pos";

		_ = await store.SavePositionAsync(viewName, 17, CancellationToken.None).ConfigureAwait(false);

		(await NewStore().GetPositionAsync(viewName, CancellationToken.None).ConfigureAwait(false))
			.ShouldBe(17, "the store must round-trip the checkpoint it was handed");

		// LIVENESS, in the right place.
		(await ScalarAsync<long>(
			"SELECT count(*) FROM " + ProbeSchema + "." + PositionTable + " WHERE view_name = @vn", viewName)
			.ConfigureAwait(false))
			.ShouldBe(1L, "the checkpoint must be written into the configured schema '" + ProbeSchema + "'");
		(await ScalarAsync<long>(
			"SELECT position FROM " + ProbeSchema + "." + PositionTable + " WHERE view_name = @vn", viewName)
			.ConfigureAwait(false))
			.ShouldBe(17L, "the checkpoint row in the configured schema must carry the position");

		// SAFETY.
		(await ScalarAsync<long>(
			"SELECT count(*) FROM public." + PositionTable + " WHERE view_name = @vn", viewName)
			.ConfigureAwait(false))
			.ShouldBe(0L, "a checkpoint written through search_path into public would silently advance the wrong table");
	}

	/// <summary>
	/// Non-vacuity guard for the safety half. "No row in public" means nothing if no such table exists —
	/// the assertion would then measure the absence of a TABLE rather than the absence of a WRITE, and an
	/// unqualified store would fail loudly instead of silently. This proves both decoys are present and are
	/// therefore genuine alternative destinations.
	/// </summary>
	private async Task AssertDecoysAreRealWriteTargetsAsync()
	{
		(await ScalarAsync<string>("SELECT to_regclass('public." + ViewTable + "')::text", viewName: null)
			.ConfigureAwait(false))
			.ShouldNotBeNull("the public decoy view table must exist, or the safety assertion measures nothing");
		(await ScalarAsync<string>("SELECT to_regclass('public." + PositionTable + "')::text", viewName: null)
			.ConfigureAwait(false))
			.ShouldNotBeNull("the public decoy position table must exist, or the safety assertion measures nothing");
	}

	private static string ViewTableDdl(string schema) =>
		"CREATE TABLE IF NOT EXISTS " + schema + "." + ViewTable + " ("
		+ "tenant_id TEXT NOT NULL, view_name TEXT NOT NULL, view_id TEXT NOT NULL, data JSONB NOT NULL,"
		+ "created_at TIMESTAMPTZ NOT NULL, updated_at TIMESTAMPTZ NOT NULL,"
		+ "PRIMARY KEY (tenant_id, view_name, view_id));\n";

	private static string PositionTableDdl(string schema) =>
		"CREATE TABLE IF NOT EXISTS " + schema + "." + PositionTable + " ("
		+ "tenant_id TEXT NOT NULL, view_name TEXT NOT NULL, position BIGINT NOT NULL,"
		+ "created_at TIMESTAMPTZ NOT NULL, updated_at TIMESTAMPTZ NOT NULL,"
		+ "PRIMARY KEY (tenant_id, view_name));\n";

	private async Task ExecuteAsync(string sql)
	{
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		await using var command = new NpgsqlCommand(sql, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	private async Task<T?> ScalarAsync<T>(string sql, string? viewName)
	{
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		await using var command = new NpgsqlCommand(sql, connection);
		if (viewName is not null)
		{
			_ = command.Parameters.AddWithValue("vn", viewName);
		}

		var value = await command.ExecuteScalarAsync().ConfigureAwait(false);
		return value is null or DBNull
			? default
			: (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
	}

	/// <summary>The ambient tenant these constructions run under; the store resolves its partition from here.</summary>
	private sealed class ProbeTenant : ITenantContext
	{
		public static ITenantContext Instance { get; } = new ProbeTenant();

		public string? TenantId => "tenant-probe";

		public bool HasTenant => true;
	}
}
