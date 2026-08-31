// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Dapper;

using Excalibur.Dispatch;

using Microsoft.Data.Sqlite;

namespace Excalibur.EventSourcing.Sqlite;

/// <summary>
/// Creates event sourcing tables on first use, tracked per physical (database, table) pair and per
/// logical role so a second store with a different table name, database file, or purpose still creates
/// its own table. Thread-safe via <see cref="SemaphoreSlim"/>.
/// </summary>
internal static class SqliteTableInitializer
{
	private static readonly SemaphoreSlim InitLock = new(1, 1);

	// Keyed by (connection string, table name, role) so initialization is tracked per physical table
	// AND per logical purpose, not once per process. Rationale for each component:
	//  * connection string - distinguishes database files and named shared-cache in-memory DBs (the
	//    same table name in two files must each be created).
	//  * table name - distinguishes tables within one file.
	//  * role - distinguishes the events table from the snapshots table so a consumer that legitimately
	//    configures the SAME table name for both does not have one initialization slot clobber the other
	//    (events and snapshots have different DDL; a single shared slot would create only the first and
	//    leave the second uninitialized, then fail with "no such table" at runtime).
	// A ValueTuple key is used instead of a delimiter-joined string: string joining is ambiguous because
	// a SQLite connection string can itself contain the delimiter (e.g. "Data Source=..."), so distinct
	// inputs could collide into one key. Structural tuple equality (ordinal for each string component)
	// makes the composition unambiguous.
	private static readonly ConcurrentDictionary<(string ConnectionString, string Table, string Role), bool> Initialized =
		new();

	private const string EventsRole = "events";
	private const string SnapshotsRole = "snapshots";

	// The reserved key for rows that belong to no tenant, read from its single canonical declaration.
	// Deliberately NOT a const: a const is inlined at compile time, which would reintroduce a second
	// spelling of the untenanted partition here the moment the canonical value changed.
	private static readonly string UntenantedTenantId = TenantScope.UntenantedSentinel;

	internal static async Task EnsureEventsTableAsync(
		SqliteConnection connection,
		string table,
		bool requireTenant,
		CancellationToken cancellationToken)
	{
		var key = BuildKey(connection, table, EventsRole);
		if (Initialized.ContainsKey(key))
		{
			return;
		}

		await InitLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (Initialized.ContainsKey(key))
			{
				return;
			}

			await connection.ExecuteAsync(
				new CommandDefinition(EventsTableDdl(table, ifNotExists: true), cancellationToken: cancellationToken))
				.ConfigureAwait(false);

			// An existing table is NOT touched by CREATE TABLE IF NOT EXISTS, so a database created by an
			// earlier version keeps whatever shape it had while this store emits SQL for the current one.
			// Reconcile it before declaring the table initialized -- same two-stage shape as the snapshots
			// table below: bring an untenanted table onto the current schema, then converge legacy rows.
			await ReconcileEventsTableAsync(connection, table, cancellationToken).ConfigureAwait(false);
			await ConvergeUntenantedEventsToDefaultTenantAsync(connection, table, requireTenant, cancellationToken)
				.ConfigureAwait(false);

			Initialized[key] = true;
		}
		finally
		{
			InitLock.Release();
		}
	}

	internal static async Task EnsureSnapshotsTableAsync(
		SqliteConnection connection,
		string table,
		bool requireTenant,
		CancellationToken cancellationToken)
	{
		var key = BuildKey(connection, table, SnapshotsRole);
		if (Initialized.ContainsKey(key))
		{
			return;
		}

		await InitLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (Initialized.ContainsKey(key))
			{
				return;
			}

			await connection.ExecuteAsync(
				new CommandDefinition(SnapshotsTableDdl(table, ifNotExists: true), cancellationToken: cancellationToken))
				.ConfigureAwait(false);

			// An existing table is NOT touched by CREATE TABLE IF NOT EXISTS, so a database created by an
			// earlier version keeps whatever shape it had while this store emits SQL for the current one.
			// Reconcile it before declaring the table initialized.
			await ReconcileSnapshotsTableAsync(connection, table, cancellationToken).ConfigureAwait(false);
			await ConvergeUntenantedRowsToDefaultTenantAsync(connection, table, requireTenant, cancellationToken)
				.ConfigureAwait(false);

			Initialized[key] = true;
		}
		finally
		{
			InitLock.Release();
		}
	}

	// Distinguishes physical tables AND logical roles: the same table name in two different database
	// files (or two distinctly-named in-memory DBs) yields two distinct keys, so each is created; and
	// the same table name used for both events and snapshots yields two distinct keys (different role),
	// so both are initialized. The tuple key avoids the delimiter-ambiguity of a joined string (a
	// connection string can itself contain any chosen delimiter).
	private static (string ConnectionString, string Table, string Role) BuildKey(
		SqliteConnection connection,
		string table,
		string role)
		=> (connection.ConnectionString, table, role);

	/// <summary>
	/// The current events schema. Single source for both the create path and the rebuild path so the two
	/// cannot drift: a rebuild that reproduced the schema separately would silently diverge from the table
	/// new databases get. Mirrors <c>Scripts/001_CreateEventStoreSchema.sql</c> -- the two are required to
	/// stay identical (see that file's header).
	/// </summary>
	private static string EventsTableDdl(string table, bool ifNotExists) =>
		$"""
		CREATE TABLE {(ifNotExists ? "IF NOT EXISTS " : string.Empty)}[{table}] (
			GlobalPosition INTEGER PRIMARY KEY AUTOINCREMENT,
			EventId TEXT NOT NULL,
			AggregateId TEXT NOT NULL,
			AggregateType TEXT NOT NULL,
			EventType TEXT NOT NULL,
			EventData BLOB,
			Metadata BLOB,
			Version INTEGER NOT NULL,
			Timestamp TEXT NOT NULL,
			-- Untenanted rows use the reserved '__untenanted__' tenant key BY DESIGN, matching Snapshots
			-- (see ARCHITECTURE.md, tenant isolation). NOT NULL is load-bearing for the same reason it is
			-- on Snapshots: SQLite treats NULLs as DISTINCT in a UNIQUE constraint, so a nullable tenant
			-- would never conflict and two writers appending the same version for the same untenanted
			-- aggregate would both succeed -- optimistic concurrency silently gone for exactly the rows a
			-- pre-tenancy database is made of.
			--
			-- No DEFAULT, matching the PostgreSQL and Oracle schemas: TenantId is part of the UNIQUE key,
			-- and a key column is not defaulted. The store binds the value explicitly on every insert.
			TenantId TEXT NOT NULL,
			-- The tenant participates in stream IDENTITY, not merely in read filters, so optimistic
			-- concurrency is per-tenant rather than global -- the same shape PostgreSQL converges to in
			-- 005_MakeEventStreamIdentityTenantScoped.sql. Without the term, two tenants sharing a natural
			-- aggregate id collide: tenant B's version probe reports -1 ("does not exist") while an append
			-- of version 0 hits tenant A's row and fails as a duplicate -- a conflict that never converges
			-- on retry, because the probe keeps reporting -1.
			UNIQUE(AggregateId, AggregateType, Version, TenantId)
		);
		""";

	/// <summary>
	/// The current snapshots schema. Single source for both the create path and the rebuild path so the
	/// two cannot drift: a rebuild that reproduced the schema separately would silently diverge from the
	/// table new databases get.
	/// </summary>
	private static string SnapshotsTableDdl(string table, bool ifNotExists) =>
		$"""
		CREATE TABLE {(ifNotExists ? "IF NOT EXISTS " : string.Empty)}[{table}] (
			Id INTEGER PRIMARY KEY AUTOINCREMENT,
			SnapshotId TEXT NOT NULL,
			AggregateId TEXT NOT NULL,
			AggregateType TEXT NOT NULL,
			Version INTEGER NOT NULL,
			Data BLOB NOT NULL,
			CreatedAt TEXT NOT NULL,
			-- Untenanted rows use the reserved '__untenanted__' tenant key BY DESIGN (see
			-- ARCHITECTURE.md, tenant isolation). NOT NULL is load-bearing: SQLite treats NULLs as
			-- DISTINCT in a UNIQUE constraint, so a nullable tenant would never conflict and every save
			-- would insert a duplicate row instead of updating the snapshot. The sentinel is
			-- collision-proof: Scoped() rejects it, so no real tenant can claim the untenanted
			-- partition. It replaced '', which is not portable - Oracle stores the empty string as
			-- NULL, so the same intent became a different value on that provider.
			--
			-- No DEFAULT, matching the PostgreSQL and Oracle schemas. TenantId is part of the UNIQUE
			-- key, and you do not default a key column: with a default, an INSERT that omitted the
			-- tenant would silently land the row in the untenanted partition, making "I forgot to
			-- supply the tenant" indistinguishable from "this row is deliberately untenanted." The
			-- store writes the sentinel explicitly on every save.
			TenantId TEXT NOT NULL,
			UNIQUE(AggregateId, AggregateType, TenantId)
		);
		""";

	/// <summary>
	/// Brings an already-existing events table onto the current stored representation, or refuses loudly
	/// if it cannot be brought there without discarding a row.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Same two shapes as <see cref="ReconcileSnapshotsTableAsync"/>, for the same reasons. The one
	/// material difference is <c>GlobalPosition</c>: unlike the snapshots table's surrogate <c>Id</c>
	/// (never read by any query this store issues), <c>GlobalPosition</c> is the global stream order
	/// returned to callers via <c>AppendResult.FirstEventPosition</c> and compared across processes. A
	/// rebuild MUST preserve the exact value for every existing row, and MUST leave SQLite's own
	/// AUTOINCREMENT high-water mark (<c>sqlite_sequence</c>) at least as high as the largest value
	/// carried over, or a value freed by the rebuild could be reused by a later append -- precisely the
	/// hazard the events schema's AUTOINCREMENT choice exists to prevent (see
	/// <c>Scripts/001_CreateEventStoreSchema.sql</c>). <see cref="RebuildEventsTableWithTenantAsync"/>
	/// copies <c>GlobalPosition</c> explicitly rather than letting the staging table assign fresh values,
	/// which is sufficient: an explicit INSERT of a rowid into an AUTOINCREMENT table advances
	/// <c>sqlite_sequence</c> to that value like any other insert, and SQLite carries the
	/// <c>sqlite_sequence</c> row over automatically when the table itself is renamed.
	/// </para>
	/// <para>
	/// NO TENANT COLUMN - the table predates tenant-aware event storage. The column cannot be added in
	/// place because it belongs to the UNIQUE key and SQLite cannot alter a constraint, so the table is
	/// rebuilt and every existing row is stamped with the untenanted sentinel. Collision-free by
	/// construction: the previous schema constrained UNIQUE(AggregateId, AggregateType, Version), so
	/// stamping one single tenant value keeps the resulting 4-tuple unique.
	/// </para>
	/// <para>
	/// TENANT COLUMN HOLDING THE EMPTY STRING - the table was written by a version that stored the
	/// untenanted partition as the empty string. Converging those rows CAN collide: an aggregate holding
	/// both an empty-string row and a sentinel row at the SAME version has two rows that would become one
	/// key, so the pre-flight refuses rather than letting the UPDATE half-apply.
	/// </para>
	/// <para>
	/// Both paths are idempotent - a second run finds nothing to do - and the destructive step is bounded
	/// by the <paramref name="table"/> argument, never by ambient state.
	/// </para>
	/// </remarks>
	/// <param name="connection">An open connection to the database holding the table.</param>
	/// <param name="table">The events table to reconcile.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <exception cref="InvalidOperationException">
	/// The table holds both representations of the untenanted partition for one aggregate at one version,
	/// so it cannot be converged without discarding an event.
	/// </exception>
	private static async Task ReconcileEventsTableAsync(
		SqliteConnection connection,
		string table,
		CancellationToken cancellationToken)
	{
		var columns = (await connection.QueryAsync<string>(
			new CommandDefinition(
				"SELECT name FROM pragma_table_info(@Table);",
				new { Table = table },
				cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

		if (columns.Count == 0)
		{
			return;
		}

		if (!columns.Contains("TenantId", StringComparer.Ordinal))
		{
			await RebuildEventsTableWithTenantAsync(connection, table, cancellationToken).ConfigureAwait(false);
			return;
		}

		await ConvergeEmptyStringEventTenantRowsAsync(connection, table, cancellationToken).ConfigureAwait(false);
	}

	private static async Task RebuildEventsTableWithTenantAsync(
		SqliteConnection connection,
		string table,
		CancellationToken cancellationToken)
	{
		var staging = $"{table}_tenant_migration";

		await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		// Rebuild rather than ALTER: TenantId joins the UNIQUE key, and SQLite cannot alter a constraint.
		// GlobalPosition is copied EXPLICITLY (not omitted, unlike the snapshots rebuild's surrogate Id) --
		// see the remarks on ReconcileEventsTableAsync for why that is load-bearing here. Every row is
		// preserved and stamped with the untenanted sentinel, which is where rows written before the store
		// had a tenant concept belong.
		var sql = $"""
			DROP TABLE IF EXISTS [{staging}];
			{EventsTableDdl(staging, ifNotExists: false)}
			INSERT INTO [{staging}] (GlobalPosition, EventId, AggregateId, AggregateType, EventType, EventData, Metadata, Version, Timestamp, TenantId)
				SELECT GlobalPosition, EventId, AggregateId, AggregateType, EventType, EventData, Metadata, Version, Timestamp, '{UntenantedTenantId}'
				FROM [{table}];
			DROP TABLE [{table}];
			ALTER TABLE [{staging}] RENAME TO [{table}];
			CREATE INDEX IF NOT EXISTS IX_{table}_AggregateId
				ON [{table}] (AggregateId, AggregateType, Version);
			""";

		_ = await connection.ExecuteAsync(
			new CommandDefinition(sql, transaction: transaction, cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
	}

	private static async Task ConvergeEmptyStringEventTenantRowsAsync(
		SqliteConnection connection,
		string table,
		CancellationToken cancellationToken)
	{
		// Guard, then converge. Unlike the one-row-per-(aggregate,tenant) snapshots table, an events table
		// holds many rows per aggregate (one per version) -- so the collision check is scoped to the
		// (AggregateId, AggregateType, Version) triple, not the aggregate alone. Two rows collide only if
		// they would become the SAME stream position under the sentinel; different versions of the same
		// aggregate never collide with each other.
		var collision = await connection.QueryFirstOrDefaultAsync<CollidingEvent>(
			new CommandDefinition(
				$"""
				SELECT older.AggregateId, older.AggregateType, older.Version
				FROM [{table}] AS older
				JOIN [{table}] AS converged
					ON converged.AggregateId = older.AggregateId
					AND converged.AggregateType = older.AggregateType
					AND converged.Version = older.Version
					AND converged.TenantId = @Sentinel
				WHERE older.TenantId = ''
				LIMIT 1;
				""",
				new { Sentinel = UntenantedTenantId },
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		if (collision is not null)
		{
			throw new InvalidOperationException(
				$"Events table '{table}' holds two representations of the untenanted partition for " +
				$"aggregate '{collision.AggregateId}' of type '{collision.AggregateType}' at version " +
				$"{collision.Version}: one row stores the tenant as an empty string and another stores " +
				$"the reserved untenanted key. They cannot be merged automatically because both would " +
				$"occupy the same stream position. Delete or re-key whichever event is stale, then start " +
				$"the application again.");
		}

		_ = await connection.ExecuteAsync(
			new CommandDefinition(
				$"UPDATE [{table}] SET TenantId = @Sentinel WHERE TenantId = '';",
				new { Sentinel = UntenantedTenantId },
				cancellationToken: cancellationToken)).ConfigureAwait(false);
	}

	/// <summary>
	/// Moves a single-tenant deployment's legacy untenanted rows onto the single-tenant identity, so rows
	/// written before the tenant context became a required dependency stay readable.
	/// </summary>
	/// <remarks>
	/// Same shape and same reason as <see cref="ConvergeUntenantedRowsToDefaultTenantAsync"/>: a
	/// single-tenant host used to write the reserved untenanted term because no tenant context was
	/// registered, and now resolves the framework's single-tenant identity instead -- without this, the
	/// earlier events become unreachable, and a stream that should replay from its history instead
	/// replays as if it never existed. Gated on the DEPLOYMENT MODE: a multi-tenant deployment's
	/// untenanted partition is a live partition, and converging it would move genuinely untenanted rows
	/// into the default tenant's stream.
	/// </remarks>
	private static async Task ConvergeUntenantedEventsToDefaultTenantAsync(
		SqliteConnection connection,
		string table,
		bool requireTenant,
		CancellationToken cancellationToken)
	{
		if (requireTenant)
		{
			return;
		}

		// Guard, then converge -- scoped to (AggregateId, AggregateType, Version), the same reasoning as
		// ConvergeEmptyStringEventTenantRowsAsync above.
		var collision = await connection.QueryFirstOrDefaultAsync<CollidingEvent>(
			new CommandDefinition(
				$"""
				SELECT untenanted.AggregateId, untenanted.AggregateType, untenanted.Version
				FROM [{table}] AS untenanted
				JOIN [{table}] AS defaulted
					ON defaulted.AggregateId = untenanted.AggregateId
					AND defaulted.AggregateType = untenanted.AggregateType
					AND defaulted.Version = untenanted.Version
					AND defaulted.TenantId = @DefaultTenant
				WHERE untenanted.TenantId = @Sentinel
				LIMIT 1;
				""",
				new { Sentinel = UntenantedTenantId, DefaultTenant = TenantDefaults.DefaultTenantId },
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		if (collision is not null)
		{
			throw new InvalidOperationException(
				$"Events table '{table}' holds events for aggregate '{collision.AggregateId}' of type " +
				$"'{collision.AggregateType}' at version {collision.Version} under both the reserved " +
				$"untenanted key and the single-tenant identity '{TenantDefaults.DefaultTenantId}'. This " +
				$"deployment is configured as single-tenant, so the untenanted rows would be moved onto " +
				$"the single-tenant identity - but that stream position already has one there, and both " +
				$"would occupy the same key. Delete or re-key whichever event is stale, then start the " +
				$"application again. If this host is actually multi-tenant, call AddMultiTenancy() so its " +
				$"untenanted rows are left alone.");
		}

		_ = await connection.ExecuteAsync(
			new CommandDefinition(
				$"UPDATE [{table}] SET TenantId = @DefaultTenant WHERE TenantId = @Sentinel;",
				new { Sentinel = UntenantedTenantId, DefaultTenant = TenantDefaults.DefaultTenantId },
				cancellationToken: cancellationToken)).ConfigureAwait(false);
	}

	/// <summary>
	/// Brings an already-existing snapshots table onto the current stored representation, or refuses
	/// loudly if it cannot be brought there without discarding a row.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Two shapes reach this method, and they are reconciled differently because only one of them can
	/// collide.
	/// </para>
	/// <para>
	/// NO TENANT COLUMN - the table predates tenant-aware snapshots. Every read and write this store
	/// issues names <c>TenantId</c>, so without this step the store throws "no such column: TenantId"
	/// on every operation against the database. The column cannot be added in place because it belongs
	/// to the UNIQUE key and SQLite cannot alter a constraint, so the table is rebuilt and every
	/// existing row is stamped with the untenanted sentinel. This case is collision-free by
	/// construction: the previous schema constrained UNIQUE(AggregateId, AggregateType), so stamping
	/// one single tenant value keeps the resulting triple unique.
	/// </para>
	/// <para>
	/// TENANT COLUMN HOLDING THE EMPTY STRING - the table was written by a version that stored the
	/// untenanted partition as the empty string. Those rows are unreachable to the current equality
	/// predicate, which looks for the sentinel. Converging them CAN collide: a table holding both an
	/// empty-string row and a sentinel row for one (AggregateId, AggregateType) has two rows that would
	/// become one key, so the pre-flight refuses rather than letting the UPDATE half-apply and leave the
	/// table in a state neither representation describes.
	/// </para>
	/// <para>
	/// Both paths are idempotent - a second run finds nothing to do - and the destructive step is
	/// bounded by the <paramref name="table"/> argument, never by ambient state.
	/// </para>
	/// </remarks>
	/// <param name="connection">An open connection to the database holding the table.</param>
	/// <param name="table">The snapshots table to reconcile.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <exception cref="InvalidOperationException">
	/// The table holds both representations of the untenanted partition for one aggregate, so it cannot
	/// be converged without discarding a snapshot.
	/// </exception>
	private static async Task ReconcileSnapshotsTableAsync(
		SqliteConnection connection,
		string table,
		CancellationToken cancellationToken)
	{
		var columns = (await connection.QueryAsync<string>(
			new CommandDefinition(
				"SELECT name FROM pragma_table_info(@Table);",
				new { Table = table },
				cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

		if (columns.Count == 0)
		{
			return;
		}

		if (!columns.Contains("TenantId", StringComparer.Ordinal))
		{
			await RebuildSnapshotsTableWithTenantAsync(connection, table, cancellationToken).ConfigureAwait(false);
			return;
		}

		await ConvergeEmptyStringTenantRowsAsync(connection, table, cancellationToken).ConfigureAwait(false);
	}

	private static async Task RebuildSnapshotsTableWithTenantAsync(
		SqliteConnection connection,
		string table,
		CancellationToken cancellationToken)
	{
		var staging = $"{table}_tenant_migration";

		await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		// Rebuild rather than ALTER: TenantId joins the UNIQUE key, and SQLite cannot alter a constraint.
		// Every row is preserved and stamped with the untenanted sentinel, which is where rows written
		// before the store had a tenant concept belong.
		var sql = $"""
			DROP TABLE IF EXISTS [{staging}];
			{SnapshotsTableDdl(staging, ifNotExists: false)}
			INSERT INTO [{staging}] (SnapshotId, AggregateId, AggregateType, Version, Data, CreatedAt, TenantId)
				SELECT SnapshotId, AggregateId, AggregateType, Version, Data, CreatedAt, '{UntenantedTenantId}'
				FROM [{table}];
			DROP TABLE [{table}];
			ALTER TABLE [{staging}] RENAME TO [{table}];
			""";

		_ = await connection.ExecuteAsync(
			new CommandDefinition(sql, transaction: transaction, cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
	}

	private static async Task ConvergeEmptyStringTenantRowsAsync(
		SqliteConnection connection,
		string table,
		CancellationToken cancellationToken)
	{
		// Guard, then converge. An aggregate holding BOTH representations cannot be converged: the two
		// rows would become one UNIQUE key. Fail naming the table and the colliding aggregate so the
		// operator can resolve it, rather than half-applying the UPDATE.
		var collision = await connection.QueryFirstOrDefaultAsync<CollidingAggregate>(
			new CommandDefinition(
				$"""
				SELECT older.AggregateId, older.AggregateType
				FROM [{table}] AS older
				JOIN [{table}] AS converged
					ON converged.AggregateId = older.AggregateId
					AND converged.AggregateType = older.AggregateType
					AND converged.TenantId = @Sentinel
				WHERE older.TenantId = ''
				LIMIT 1;
				""",
				new { Sentinel = UntenantedTenantId },
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		if (collision is not null)
		{
			throw new InvalidOperationException(
				$"Snapshot table '{table}' holds two representations of the untenanted partition for " +
				$"aggregate '{collision.AggregateId}' of type '{collision.AggregateType}': one row stores " +
				$"the tenant as an empty string and another stores the reserved untenanted key. They cannot " +
				$"be merged automatically because both would occupy the same key. Delete or re-key whichever " +
				$"snapshot is stale, then start the application again.");
		}

		_ = await connection.ExecuteAsync(
			new CommandDefinition(
				$"UPDATE [{table}] SET TenantId = @Sentinel WHERE TenantId = '';",
				new { Sentinel = UntenantedTenantId },
				cancellationToken: cancellationToken)).ConfigureAwait(false);
	}

	/// <summary>
	/// Moves a single-tenant deployment's legacy untenanted rows onto the single-tenant identity, so rows
	/// written before the tenant context became a required dependency stay readable.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A single-tenant host used to write the reserved untenanted term, because no tenant context was
	/// registered and the store resolved no tenant. It now resolves the framework's single-tenant identity
	/// and reads with that term, so without this the earlier rows are silently unreachable -- the snapshot
	/// is not found, the aggregate replays from its first event, and nothing reports an error.
	/// </para>
	/// <para>
	/// Gated on the DEPLOYMENT MODE, not on what a context happens to resolve. In a multi-tenant deployment
	/// the untenanted partition is a live partition holding rows that genuinely belong to no tenant, and
	/// converging it would move those rows into the default tenant's data. That is the failure this guard
	/// exists to prevent, so a multi-tenant host is left untouched.
	/// </para>
	/// </remarks>
	private static async Task ConvergeUntenantedRowsToDefaultTenantAsync(
		SqliteConnection connection,
		string table,
		bool requireTenant,
		CancellationToken cancellationToken)
	{
		if (requireTenant)
		{
			return;
		}

		// Guard, then converge -- the same shape as the empty-string convergence above. An aggregate holding
		// BOTH terms cannot be converged: the two rows would collapse onto one UNIQUE key. Fail naming the
		// table and the aggregate so an operator can resolve it, rather than half-applying the UPDATE.
		var collision = await connection.QueryFirstOrDefaultAsync<CollidingAggregate>(
			new CommandDefinition(
				$"""
				SELECT untenanted.AggregateId, untenanted.AggregateType
				FROM [{table}] AS untenanted
				JOIN [{table}] AS defaulted
					ON defaulted.AggregateId = untenanted.AggregateId
					AND defaulted.AggregateType = untenanted.AggregateType
					AND defaulted.TenantId = @DefaultTenant
				WHERE untenanted.TenantId = @Sentinel
				LIMIT 1;
				""",
				new { Sentinel = UntenantedTenantId, DefaultTenant = TenantDefaults.DefaultTenantId },
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		if (collision is not null)
		{
			throw new InvalidOperationException(
				$"Snapshot table '{table}' holds snapshots for aggregate '{collision.AggregateId}' of type " +
				$"'{collision.AggregateType}' under both the reserved untenanted key and the single-tenant " +
				$"identity '{TenantDefaults.DefaultTenantId}'. This deployment is configured as single-tenant, " +
				$"so the untenanted rows would be moved onto the single-tenant identity - but that aggregate " +
				$"already has one there, and both would occupy the same key. Delete or re-key whichever " +
				$"snapshot is stale, then start the application again. If this host is actually multi-tenant, " +
				$"call AddMultiTenancy() so its untenanted rows are left alone.");
		}

		_ = await connection.ExecuteAsync(
			new CommandDefinition(
				$"UPDATE [{table}] SET TenantId = @DefaultTenant WHERE TenantId = @Sentinel;",
				new { Sentinel = UntenantedTenantId, DefaultTenant = TenantDefaults.DefaultTenantId },
				cancellationToken: cancellationToken)).ConfigureAwait(false);
	}

	private sealed class CollidingAggregate
	{
		public string AggregateId { get; init; } = string.Empty;

		public string AggregateType { get; init; } = string.Empty;
	}

	private sealed class CollidingEvent
	{
		public string AggregateId { get; init; } = string.Empty;

		public string AggregateType { get; init; } = string.Empty;

		public long Version { get; init; }
	}

	/// <summary>
	/// Resets the initialization state. For testing only.
	/// </summary>
	internal static void Reset() => Initialized.Clear();
}
