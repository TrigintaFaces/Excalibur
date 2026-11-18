// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Dapper;

using Excalibur.Compliance.Erasure;

using Npgsql;

namespace Excalibur.Compliance.Postgres.Erasure;

/// <summary>
/// Answers whether an existing PostgreSQL compliance schema is one the erasure store's statements
/// can rely on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own type rather than a member of the store, for two reasons.</b> It reads the catalogue and
/// not the rows, so it shares no state with the store's query paths; and it is the runtime half of a
/// question the shipped provisioning scripts also answer, so the definition of "compatible" is worth
/// keeping in one readable place where the two can be compared.
/// </para>
/// <para>
/// Constructed where it is used rather than injected: it is called once, during initialization, and
/// holds nothing worth keeping afterwards.
/// </para>
/// </remarks>
/// <param name="options">The store's options, which carry the configured table names.</param>
internal sealed class PostgresErasureSchemaVerifier(PostgresErasureStoreOptions options)
{
	private readonly PostgresErasureStoreOptions _options = options;

	/// <summary>
	/// Confirms the required tables exist when automatic provisioning is disabled.
	/// </summary>
	/// <remarks>
	/// Initialization must never complete without either creating the schema or verifying it. Marking the store
	/// initialized after doing neither would defer the failure to the first query, where it surfaces as a raw
	/// provider error far from its cause. This method is the verification half of that guarantee.
	/// </remarks>
	/// <exception cref="ErasureStoreNotProvisionedException">
	/// A required table is absent, or is present in a shape this store's statements cannot rely on:
	/// a missing column, a primary key that is not the declared one, a compared column whose collation
	/// is not deterministic, a missing enumeration index, or an unverifiable fingerprint write-once guard.
	/// Deliberately outside the <see cref="InvalidOperationException"/> hierarchy: that is the hierarchy a
	/// duplicate request identifier uses, and a caller cannot be left unable to tell "this request is
	/// already on file" from "this database was never provisioned".
	/// </exception>
	internal async Task VerifyAsync(CancellationToken cancellationToken)
	{
		// Reading the COLUMN catalogue rather than the table catalogue is the whole point of this method.
		// A probe that asks only whether the table exists reports healthy on precisely the database that is
		// broken: one provisioned before a column was added, where the table is present and the wrong shape.
		// The consumer then gets a dead store plus a check that told them it was fine, and the real failure
		// arrives later as a raw undefined_column far from its cause. Automatic schema creation does not
		// repair that database either -- CREATE TABLE IF NOT EXISTS only creates tables that are absent.
		//
		// Resolved through to_regclass rather than by splitting the configured name: the option already
		// carries a qualified identifier and to_regclass parses it the way the statements do. attnum > 0
		// excludes system columns; NOT attisdropped excludes columns dropped but not yet vacuumed, which
		// still occupy a pg_attribute row and would otherwise read as present.
		//
		// THE COLLATION COMES BACK WITH THE NAME, because a name on its own answers a narrower question
		// than this method claims to answer. A LEFT JOIN rather than an inner one: attcollation is 0 for a
		// type that cannot be collated at all (uuid, int, timestamptz), and those columns must not drop out
		// of the existence check just because they have no collation to report.
		const string ColumnsSql =
			"SELECT a.attname AS Name, co.collname AS CollationName, " +
			"COALESCE(co.collisdeterministic, TRUE) AS IsDeterministic " +
			"FROM pg_attribute a LEFT JOIN pg_collation co ON co.oid = a.attcollation " +
			"WHERE a.attrelid = to_regclass(@TableName) AND a.attnum > 0 AND NOT a.attisdropped";

		// indkey holds the index's columns, key columns first; indnkeyatts bounds the KEY columns, so an
		// INCLUDE column cannot be mistaken for one the index can be searched by. WITH ORDINALITY keeps the
		// declared order, which the primary-key comparison needs and a set comparison would discard.
		const string IndexesSql =
			"SELECT i.indexrelid::text AS IndexId, i.indisprimary AS IsPrimaryKey, " +
			"k.ord AS KeyOrdinal, a.attname AS ColumnName " +
			"FROM pg_index i " +
			"CROSS JOIN LATERAL unnest(i.indkey) WITH ORDINALITY AS k(attnum, ord) " +
			"JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = k.attnum " +
			"WHERE i.indrelid = to_regclass(@TableName) AND k.ord <= i.indnkeyatts " +
			"ORDER BY i.indexrelid, k.ord";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		foreach (var table in RequiredSchema)
		{
			var actualColumns = (await connection.QueryAsync<SchemaColumnRow>(
				new CommandDefinition(
					ColumnsSql,
					new { TableName = table.TableName },
					cancellationToken: cancellationToken,
					commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false)).ToList();

			// No columns at all means no such table: to_regclass returns NULL for a table that does not
			// exist, so the same query answers both questions and they stay in step.
			if (actualColumns.Count == 0)
			{
				throw new ErasureStoreNotProvisionedException(
					$"Required table '{table.TableName}' does not exist and automatic schema creation is disabled. " +
					$"Either create the schema out of band, or set {nameof(PostgresErasureStoreOptions)}."
					+ $"{nameof(PostgresErasureStoreOptions.AutoCreateSchema)} to true to provision it on startup.")
				{
					TableName = table.TableName,
				};
			}

			// Named, not counted. An operator reading this at startup needs to know WHICH columns are absent
			// to choose the migration; "the schema is stale" sends them to diff it by hand.
			var missing = table.RequiredColumns
				.Where(required => !actualColumns.Any(actual => string.Equals(actual.Name, required, StringComparison.Ordinal)))
				.ToList();

			if (missing.Count > 0)
			{
				throw new ErasureStoreNotProvisionedException(
					$"Table '{table.TableName}' exists but is missing {missing.Count} column(s) that this store's "
					+ $"statements bind: {string.Join(", ", missing)}. This is a schema provisioned before those "
					+ "columns were introduced. Enabling automatic schema creation will NOT repair it, because "
					+ "that path only creates tables that are absent. WE SHIP NO ALTER SCRIPT for this: the "
					+ "create scripts already declare these columns, so a freshly provisioned database is "
					+ "correct and only a pre-existing one reaches this. Provision a new compliance schema from the shipped "
					+ "create script. Preserve the earlier schema and its destruction evidence; "
					+ "arbitrary defaults cannot safely backfill tenant identity. Then restart.")
				{
					TableName = table.TableName,
				};
			}

			await VerifyRepresentationAsync(connection, table, actualColumns, IndexesSql, cancellationToken)
				.ConfigureAwait(false);
		}

		await VerifyTokenFingerprintGuardAsync(connection, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Verifies that the fingerprint's provisioned write-once guard covers ordinary writes.</summary>
	/// <param name="connection">An open connection to the compliance database.</param>
	/// <param name="cancellationToken">A token that cancels the catalogue read.</param>
	/// <returns>A task that completes when the enabled table-bound guard is present.</returns>
	private async Task VerifyTokenFingerprintGuardAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
	{
		// The function is installed by the shipped provisioning path. Resolve its unquoted identifier
		// through PostgreSQL itself, preserving the same case folding and truncation as CREATE FUNCTION.
		// Metadata binds that function to this table and proves enabled, unconditional UPDATE/DELETE
		// coverage; it does not prove arbitrary replacement function bodies or prevent privileged DDL.
		const string GuardSql = """
			SELECT EXISTS (
				SELECT 1 FROM pg_catalog.pg_trigger t
				WHERE t.tgrelid = to_regclass(@TableName)
				AND t.tgfoid = to_regprocedure(@FunctionName)::oid
				AND NOT t.tgisinternal
				AND t.tgenabled IN ('O', 'A')
				AND (t.tgtype::integer & 27) = 27
				AND (t.tgtype::integer & 64) = 0
				AND t.tgattr = ''::pg_catalog.int2vector
				AND t.tgqual IS NULL
			)
			""";

		// pg_trigger.h defines ROW=1, BEFORE=2, DELETE=8, UPDATE=16 and INSTEAD=64.
		var present = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
			GuardSql,
			new
			{
				TableName = _options.FullTokenFingerprintTableName,
				FunctionName = $"\"{_options.SchemaName}\".{_options.TokenFingerprintTableName}_is_write_once()",
			},
			cancellationToken: cancellationToken,
			commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		if (!present)
		{
			throw new ErasureStoreNotProvisionedException(
				$"Fingerprint table '{_options.FullTokenFingerprintTableName}' must have an enabled write-once "
				+ "guard covering every UPDATE and DELETE. Its table-bound BEFORE ROW trigger and provisioned "
				+ "guard function could not be verified. Apply the shipped fingerprint provisioning script "
				+ "(006_AddDataSubjectTokenFingerprint.sql), adapting its identifiers to the configured table, "
				+ "and restart. Changing or deleting the recorded fingerprint is not a safe repair.")
			{
				TableName = _options.FullTokenFingerprintTableName,
			};
		}
	}

	/// <summary>
	/// Verifies the properties of a table that make this store's statements mean what they say, beyond the
	/// presence of the column names they bind.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Why a name check was not enough, stated as the two predicates side by side.</b> What the name
	/// check asserts is <i>these columns exist</i>. What a caller of the schema validator needs is <i>this
	/// table can satisfy the statements this store issues</i>. Nothing in a passing name check distinguishes
	/// them, and the shipped create script has always declared more than names: a primary key, a collation
	/// per compared column, and an index for the enumeration an erasure destroys from. The provisioning
	/// scripts verify all three on a pre-existing table; until this method existed the runtime path verified
	/// none of them, so the same database was compatible or incompatible depending on which of our own two
	/// paths looked at it.
	/// </para>
	/// <para>
	/// <b>The collation is the one with a silent failure.</b> An erasure enumerates a subject's handles by
	/// (tenant, subject hash) and the service compares handles ordinally. Under a collation that folds, one
	/// owner's rows answer for another's: the enumeration returns handles the requesting tenant never minted,
	/// the erasure destroys them, and a completion certificate is signed over the damage. Nothing throws.
	/// PostgreSQL makes this narrower than it is elsewhere -- equality under a <i>deterministic</i> collation
	/// is byte equality, so a folding database default is not reachable through the shipped create script,
	/// which collates these columns explicitly. What this check refuses is the hand-provisioned table whose
	/// compared columns carry no explicit collation, because then their determinism is a property of the
	/// database we cannot read off the column and must not assume.
	/// </para>
	/// <para>
	/// <b>Scope, so a passing check is not read as more than it is.</b> It verifies the declared primary key
	/// in order, the declared ordinally-compared columns, and that some single index's key columns cover each
	/// declared enumeration key. It does NOT verify column types, widths, nullability, or the performance
	/// indexes the create path adds for querying -- a table can pass this and still be slow, and a NOT NULL
	/// that should be nullable is checked by the provisioning scripts rather than here.
	/// </para>
	/// </remarks>
	/// <param name="connection">An open connection to the compliance database.</param>
	/// <param name="table">The table's declared contract.</param>
	/// <param name="actualColumns">The table's columns as the catalogue reports them.</param>
	/// <param name="indexesSql">The catalogue query returning the table's index key columns in order.</param>
	/// <param name="cancellationToken">A token that cancels the catalogue reads.</param>
	/// <returns>A task that completes when every declared property holds.</returns>
	/// <exception cref="ErasureStoreNotProvisionedException">A declared property does not hold.</exception>
	private async Task VerifyRepresentationAsync(
		NpgsqlConnection connection,
		TableContract table,
		List<SchemaColumnRow> actualColumns,
		string indexesSql,
		CancellationToken cancellationToken)
	{
		foreach (var ordinalColumn in table.OrdinallyComparedColumns)
		{
			var column = actualColumns.First(actual => string.Equals(actual.Name, ordinalColumn, StringComparison.Ordinal));

			// 'default' is the pseudo-collation a column carries when the create statement named none. It
			// resolves to the database's own collation, whose determinism is not a property of this column,
			// so it is refused rather than assumed: the shipped create script names a collation here.
			var named = column.CollationName is not null
				&& !string.Equals(column.CollationName, "default", StringComparison.Ordinal);

			if (named && column.IsDeterministic)
			{
				continue;
			}

			throw new ErasureStoreNotProvisionedException(
				$"Column '{table.TableName}.{ordinalColumn}' must carry an explicit deterministic collation, "
				+ $"and it reports '{column.CollationName ?? "none"}'"
				+ (named ? " (non-deterministic)" : " (the database default, whose determinism is not a "
					+ "property of this column)")
				+ ". This store compares that column ordinally and an erasure enumerates a data subject's "
				+ "keys by it. A collation that folds case would let one tenant's rows answer for another's, "
				+ "so an erasure would destroy keys it does not own and sign a completion certificate over "
				+ "the result, with nothing raised. The shipped create script declares COLLATE \"C\" here. "
				+ "Provision a new compliance schema from it and restart.")
			{
				TableName = table.TableName,
			};
		}

		if (table.PrimaryKeyColumns.Count == 0 && table.EnumerationKeys.Count == 0)
		{
			return;
		}

		var indexColumns = (await connection.QueryAsync<SchemaIndexColumnRow>(
			new CommandDefinition(
				indexesSql,
				new { TableName = table.TableName },
				cancellationToken: cancellationToken,
				commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false)).ToList();

		var indexes = indexColumns
			.GroupBy(row => row.IndexId, StringComparer.Ordinal)
			.Select(group => new
			{
				IsPrimaryKey = group.Any(row => row.IsPrimaryKey),
				Columns = group.OrderBy(row => row.KeyOrdinal).Select(row => row.ColumnName).ToList(),
			})
			.ToList();

		// IN ORDER, because a primary key is an ordered tuple: (handle, generation) and (generation, handle)
		// admit the same rows but are different indexes, and the provisioning script compares the ordered
		// list for the same reason.
		if (table.PrimaryKeyColumns.Count > 0)
		{
			var primaryKey = indexes.SingleOrDefault(index => index.IsPrimaryKey);
			if (primaryKey is null || !primaryKey.Columns.SequenceEqual(table.PrimaryKeyColumns, StringComparer.Ordinal))
			{
				throw new ErasureStoreNotProvisionedException(
					$"Table '{table.TableName}' must have primary key "
					+ $"({string.Join(", ", table.PrimaryKeyColumns)}), and it reports "
					+ $"({(primaryKey is null ? "none" : string.Join(", ", primaryKey.Columns))}). The primary "
					+ "key is what makes this store's conflict handling a single-row decision; without it the "
					+ "same fact can be recorded twice and the two rows can disagree. Provision a new "
					+ "compliance schema from the shipped create script and restart.")
				{
					TableName = table.TableName,
				};
			}
		}

		// MEMBERSHIP IN ONE INDEX, not across several. Two single-column indexes do not give the server a
		// way to seek on the pair, which is the access this enumeration is. The provisioning script checks
		// it the same way, so there stays one definition of compatible.
		foreach (var enumerationKey in table.EnumerationKeys)
		{
			if (indexes.Any(index => enumerationKey.All(
				required => index.Columns.Contains(required, StringComparer.Ordinal))))
			{
				continue;
			}

			throw new ErasureStoreNotProvisionedException(
				$"Table '{table.TableName}' has no single index whose key columns cover "
				+ $"({string.Join(", ", enumerationKey)}). That is the only key the rows this store reads "
				+ "from this table can be found by, so without it every read scans the table. Provision a "
				+ "new compliance schema from the shipped create script and restart.")
			{
				TableName = table.TableName,
			};
		}
	}

	/// <summary>
	/// The properties of one table that this store's statements rely on.
	/// </summary>
	/// <param name="TableName">The configured, qualified table name.</param>
	/// <param name="RequiredColumns">Every column any statement this store issues binds.</param>
	/// <param name="PrimaryKeyColumns">
	/// The primary key's columns in declaration order, or empty where this store issues no statement whose
	/// correctness depends on the key.
	/// </param>
	/// <param name="OrdinallyComparedColumns">
	/// The columns a WHERE clause compares and the framework compares ordinally, which therefore need a
	/// deterministic collation. Only the columns the shipped create script collates explicitly appear here:
	/// declaring one the create script leaves to the database default would refuse every correctly
	/// provisioned database, which is the opposite of the failure this check exists to prevent.
	/// </param>
	/// <param name="EnumerationKeys">
	/// Each set of columns some read locates rows by, every one of which must be covered by the key columns
	/// of a single index. Deliberately NOT every index the create path adds: an index that only makes a
	/// query faster is not a correctness property, and refusing startup over one would be a worse failure
	/// than the slow query.
	/// </param>
	private sealed record TableContract(
		string TableName,
		IReadOnlyList<string> RequiredColumns,
		IReadOnlyList<string> PrimaryKeyColumns,
		IReadOnlyList<string> OrdinallyComparedColumns,
		IReadOnlyList<string[]> EnumerationKeys);

	/// <summary>A column as the catalogue reports it.</summary>
	/// <param name="Name">The column's name.</param>
	/// <param name="CollationName">
	/// The collation's name, 'default' where the create statement named none, and null for a type that
	/// cannot be collated.
	/// </param>
	/// <param name="IsDeterministic">
	/// Whether comparisons under that collation also check byte equality. True for a type that carries no
	/// collation, because there is nothing there that could fold.
	/// </param>
	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed record SchemaColumnRow(string Name, string? CollationName, bool IsDeterministic);

	/// <summary>One key column of one index, as the catalogue reports it.</summary>
	/// <param name="IndexId">The index's identity, so columns of different indexes are not merged.</param>
	/// <param name="IsPrimaryKey">Whether this index is the table's primary key.</param>
	/// <param name="KeyOrdinal">The column's one-based position among the index's key columns.</param>
	/// <param name="ColumnName">The column's name.</param>
	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed record SchemaIndexColumnRow(string IndexId, bool IsPrimaryKey, long KeyOrdinal, string ColumnName);

	/// <summary>
	/// Gets what every table must be for this store's statements to mean what they say.
	/// </summary>
	/// <remarks>
	/// The request columns are the union of the INSERT and the several UPDATE paths, not the INSERT alone.
	/// A request is created with a subset and then mutated through execution, completion and cancellation,
	/// so the columns only an UPDATE names -- the outcome and cancellation fields -- are exactly the ones a
	/// schema predating those features would lack, and the ones whose absence would otherwise surface at the
	/// end of an erasure rather than at startup. Compared with <see cref="StringComparer.Ordinal"/> because
	/// PostgreSQL folds unquoted identifiers to lower case and stores them that way, so these are written as
	/// the catalogue holds them rather than relying on a case-insensitive match to paper over a mismatch.
	/// </remarks>
	private IEnumerable<TableContract> RequiredSchema =>
	[
		// The requests table names no ordinally-compared column, and that is a KNOWN GAP rather than a
		// statement that none exists: tenant_id and tenant_partition are compared by every scoped read, and
		// the shipped create script leaves both to the database collation, so requiring one here would
		// refuse every database the script provisions. The SQL Server sibling collates them, which is why
		// its contract declares them and this one cannot.
		new TableContract(
			_options.FullRequestsTableName,
			[
				"request_id", "data_subject_id_hash", "id_type", "tenant_id", "tenant_partition", "scope", "legal_basis_v2",
				"external_reference", "requested_by", "requested_at", "scheduled_execution_at",
				"executed_at", "completed_at", "cancelled_at", "cancellation_reason", "cancelled_by",
				"status", "keys_deleted", "records_affected", "certificate_id", "error_message",
				"data_categories", "created_at", "updated_at",
				// Custody. Required rather than optional: without them the claim cannot record who holds
				// it, so an execution interrupted by a shutdown stays in-progress forever and every query
				// that looks for work correctly excludes it. A deployment provisioned before these
				// columns existed fails closed here, naming the script, instead of failing later on an
				// undefined column far from its cause.
				"claim_identity", "claim_expires_at",
			],
			["request_id"],
			[],
			[]),
		new TableContract(
			_options.FullCertificatesTableName,
			[
				"certificate_id", "request_id", "data_subject_reference", "request_received_at", "completed_at",
				"method", "summary", "verification", "legal_basis_v2", "signature", "retain_until",
				"exceptions", "generated_at", "version", "created_at", "payload",
			],
			["certificate_id"],
			[],
			[]),

		// Verified like the others, so a database provisioned before this table existed fails at STARTUP with
		// the table named, rather than mid-erasure. The failure is loud on purpose: without this table a
		// retried erasure silently attests less coverage than the request achieved, and the subject's
		// completion becomes unreachable with nothing reporting why.
		//
		// The PAIR is the key, and the order is part of it. The request index is an enumeration key rather
		// than a performance one: a retry reads this table BY REQUEST to learn which handles its earlier
		// passes destroyed, and that is not a prefix of the primary key.
		new TableContract(
			_options.FullDestroyedKeysTableName,
			["key_generation", "request_id", "key_handle", "destroyed_at", "recorded_by"],
			["key_handle", "key_generation"],
			["key_generation", "key_handle"],
			[["request_id", "key_handle"]]),

		// Verified for the same reason, and the failure it prevents is worse. Without this table a destruction
		// proceeds with nowhere to stage the generation it is about to annihilate, so a crash between the
		// destroy and the record leaves the subject's ciphertext permanently undecryptable AND their erasure
		// permanently unreportable, with no repair available at all. Naming the table at STARTUP is the only
		// place that is still cheap.
		new TableContract(
			_options.FullDestructionIntentsTableName,
			["request_id", "key_handle", "key_generation", "staged_at"],
			["request_id", "key_handle", "key_generation"],
			["key_handle", "key_generation"],
			[]),

		// Verified for the same reason as the two above, and the failure it prevents is the one this whole
		// subsystem exists to remove. Without this table the write path cannot record a mint, so either
		// personal-data writes fail loudly (the fail-closed direction, which is correct) or -- if a future
		// edit made recording tolerant -- an erasure enumerates nothing, destroys only the handles it can
		// derive, and signs a completion certificate over whatever it missed. Naming the table at STARTUP
		// is the only place that is still cheap.
		//
		// All three of its representation properties are declared, because all three are load-bearing: the
		// handle is the conflict target the mint row's insert names, the three compared columns are what an
		// enumeration folds across if a collation folds, and (tenant_id, data_subject_id_hash) is the only
		// key a subject's handles can be found by, a handle being an opaque digest.
		new TableContract(
			_options.FullMintedKeysTableName,
			["key_handle", "tenant_id", "data_subject_id_hash", "intent_at", "minted_at"],
			["key_handle"],
			["key_handle", "tenant_id", "data_subject_id_hash"],
			[["tenant_id", "data_subject_id_hash"]]),

		// Verified like the others, so a database provisioned before this table existed fails at STARTUP
		// with the table named. Without it the store cannot say which token function produced its records,
		// and the only remaining option would be to assume nothing had changed — which is precisely the
		// silence this table exists to remove.
		//
		// IT DECLARES NO TENANT COLUMN, AND THAT ABSENCE IS ASSERTED RATHER THAN ASSUMED. The column list
		// here is the contract, so a database whose operator "fixed" this table by adding a tenant column
		// to match its siblings still verifies — the verifier checks that required columns are PRESENT, not
		// that extra ones are absent. What the absence buys is that the framework's own statements cannot
		// bind a tenant term, because there is nothing in this contract to bind. The create script carries
		// the reason at the column list, and the trigger it installs is what refuses a rewrite.
		//
		// No ordinally-compared column is declared: the fingerprint is compared by this store in managed
		// code after being read back whole, never folded by a database collation, and the id is a framework
		// constant rather than consumer data. Declaring a collation here would refuse every database the
		// shipped script provisions, for no property anybody relies on.
		new TableContract(
			_options.FullTokenFingerprintTableName,
			["id", "fingerprint", "recorded_at"],
			["id"],
			[],
			[]),

		// The tenant is in the KEY and nowhere else, which is what makes the staleness comparison's
		// tenant filter a key lookup rather than a predicate. Both terms are compared ordinally by the
		// framework, so both need a deterministic collation: a database whose default folds case would
		// make two distinct aggregate types one row, and the comparison would then report a declaration
		// as recorded when the deployment recorded a different one.
		new TableContract(
			_options.FullRetentionDeclarationsTableName,
			["tenant_id", "aggregate_type", "recorded_at", "withdrawn_at", "withdrawn_by", "withdrawal_reason"],
			["tenant_id", "aggregate_type"],
			["tenant_id", "aggregate_type"],
			[["tenant_id"]]),
	];
}
