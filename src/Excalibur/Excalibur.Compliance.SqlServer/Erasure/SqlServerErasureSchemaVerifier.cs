// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Dapper;

using Excalibur.Compliance.Erasure;

using Microsoft.Data.SqlClient;

namespace Excalibur.Compliance.SqlServer.Erasure;

/// <summary>
/// Answers whether an existing SQL Server compliance schema is one the erasure store's statements
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
internal sealed class SqlServerErasureSchemaVerifier(SqlServerErasureStoreOptions options)
{
	private readonly SqlServerErasureStoreOptions _options = options;

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
	/// a missing column, a primary key that is not the declared one, a compared column whose collation is
	/// not binary, a missing enumeration index, or an unverifiable fingerprint write-once guard.
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
		// arrives later as a raw "Invalid column name" far from its cause. Automatic schema creation does not
		// repair that database either -- it only creates tables that are absent.
		//
		// THE COLLATION COMES BACK WITH THE NAME, and on this provider that is not a refinement. The server
		// default collation is case-INSENSITIVE, and the create path supplies a binary collation only when
		// the table is ABSENT, so a pre-existing table with the right names inherits folding equality while
		// every check we used to run passed.
		const string ColumnsSql =
			"SELECT c.name AS Name, c.collation_name AS CollationName " +
			"FROM sys.columns c WHERE c.object_id = OBJECT_ID(@TableName, 'U')";

		// key_ordinal > 0 keeps the KEY columns and drops both an INCLUDE column and a heap's index_id 0,
		// so an included column cannot be mistaken for one the index can be seeked on.
		const string IndexesSql =
			"SELECT CONVERT(NVARCHAR(32), i.index_id) AS IndexId, i.is_primary_key AS IsPrimaryKey, " +
			// key_ordinal is TINYINT, and converting it here keeps that an engine detail rather than
			// something the contract type has to know about.
			"CONVERT(INT, ic.key_ordinal) AS KeyOrdinal, c.name AS ColumnName " +
			"FROM sys.indexes i " +
			"JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id " +
			"JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id " +
			"WHERE i.object_id = OBJECT_ID(@TableName, 'U') AND ic.key_ordinal > 0 " +
			"ORDER BY i.index_id, ic.key_ordinal";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		foreach (var table in RequiredSchema)
		{
			var actualColumns = (await connection.QueryAsync<SchemaColumnRow>(
				new CommandDefinition(
					ColumnsSql,
					new { TableName = table.TableName },
					cancellationToken: cancellationToken,
					commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false)).ToList();

			// No columns at all means no such table: sys.columns joins on OBJECT_ID, which is NULL for a
			// table that does not exist, so the same query answers both questions and they stay in step.
			if (actualColumns.Count == 0)
			{
				throw new ErasureStoreNotProvisionedException(
					$"Required table '{table.TableName}' does not exist and automatic schema creation is disabled. " +
					$"Either create the schema out of band, or set {nameof(SqlServerErasureStoreOptions)}."
					+ $"{nameof(SqlServerErasureStoreOptions.AutoCreateSchema)} to true to provision it on startup.")
				{
					TableName = table.TableName,
				};
			}

			// Named, not counted. An operator reading this at startup needs to know WHICH columns are absent
			// to choose the migration; "the schema is stale" sends them to diff it by hand.
			var missing = table.RequiredColumns
				.Where(required => !actualColumns.Any(actual => string.Equals(actual.Name, required, StringComparison.OrdinalIgnoreCase)))
				.ToList();

			if (missing.Count > 0)
			{
				throw new ErasureStoreNotProvisionedException(
					$"Table '{table.TableName}' exists but is missing {missing.Count} column(s) that this store's "
					+ $"statements bind: {string.Join(", ", missing)}. This is a schema provisioned before those "
					+ "columns were introduced. Enabling automatic schema creation will NOT repair it, because "
					+ "that path only creates tables that are absent. If the missing column names end in V2, "
					+ "the package ships the migration that adds them -- apply "
					+ "'003_ReplaceLegalBasisColumns.sql' and restart. For any other missing column we ship no "
					+ "alter script: the create scripts already declare them, so a freshly provisioned database "
					+ "is correct and only a pre-existing one reaches this. Provision a new compliance schema from the shipped "
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
	private async Task VerifyTokenFingerprintGuardAsync(SqlConnection connection, CancellationToken cancellationToken)
	{
		// The named trigger is installed by the shipped provisioning path. Validate its binding, enabled
		// status and both DML events, rather than accepting a same-named disabled or UPDATE-only object.
		// These properties do not prove arbitrary replacement trigger bodies or prevent privileged DDL.
		const string GuardSql = """
			SELECT CAST(CASE WHEN EXISTS (
				SELECT 1 FROM sys.triggers t
				WHERE t.object_id = OBJECT_ID(@TriggerName, N'TR')
				AND t.parent_class = 1
				AND t.parent_id = OBJECT_ID(@TableName, N'U')
				AND t.is_disabled = 0
				AND t.is_instead_of_trigger = 0
				AND EXISTS (SELECT 1 FROM sys.trigger_events e WHERE e.object_id = t.object_id AND e.type_desc = N'UPDATE')
				AND EXISTS (SELECT 1 FROM sys.trigger_events e WHERE e.object_id = t.object_id AND e.type_desc = N'DELETE')
			) THEN 1 ELSE 0 END AS INT)
			""";

		var present = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
			GuardSql,
			new
			{
				TableName = _options.FullTokenFingerprintTableName,
				TriggerName = $"[{_options.SchemaName}].[TR_{_options.TokenFingerprintTableName}_WriteOnce]",
			},
			cancellationToken: cancellationToken,
			commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		if (present == 0)
		{
			throw new ErasureStoreNotProvisionedException(
				$"Fingerprint table '{_options.FullTokenFingerprintTableName}' must have an enabled write-once "
				+ "guard covering every UPDATE and DELETE. Its table-bound AFTER trigger could not be verified. "
				+ "Apply the shipped fingerprint provisioning script (007_AddDataSubjectTokenFingerprint.sql), "
				+ "adapting its identifiers to the configured table, and restart. Changing or deleting the "
				+ "recorded fingerprint is not a safe repair.")
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
	/// them, and the shipped create script has always declared more than names: a primary key, a binary
	/// collation per compared column, and an index for the enumeration an erasure destroys from. The
	/// provisioning scripts verify all three on a pre-existing table; until this method existed the runtime
	/// path verified none of them, so the same database was compatible or incompatible depending on which of
	/// our own two paths looked at it.
	/// </para>
	/// <para>
	/// <b>The collation is the one with a silent failure, and on this engine it is reachable.</b> An erasure
	/// enumerates a subject's handles by (tenant, subject hash) and the service compares handles ordinally.
	/// This server's default collation is case-insensitive, and the create path supplies a binary one only
	/// when the table is absent -- so a table provisioned by hand, or inherited from an earlier schema,
	/// compares those columns with folding equality. One owner's rows then answer for another's: the
	/// enumeration returns handles the requesting tenant never minted, the erasure destroys them, and a
	/// completion certificate is signed over the damage. Nothing throws.
	/// </para>
	/// <para>
	/// <b>Binary, not merely case-sensitive.</b> A CS_AS collation still compares linguistically, so it can
	/// call two byte-different strings equal; only a binary collation agrees with the ordinal comparison the
	/// framework performs on the same values. That is also what the provisioning scripts require, so there
	/// stays one definition of compatible.
	/// </para>
	/// <para>
	/// <b>Scope, so a passing check is not read as more than it is.</b> It verifies the declared primary key
	/// in order, the declared ordinally-compared columns, and that some single index's key columns cover each
	/// declared enumeration key. It does NOT verify column types, widths, nullability, or the performance
	/// indexes the create path adds for querying -- a table can pass this and still be slow, and a NOT NULL
	/// that should be nullable is checked by the provisioning scripts rather than here. Trailing-blank
	/// insignificance in this engine's equality is a separate property, handled at each statement by a
	/// length term rather than by the collation.
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
		SqlConnection connection,
		TableContract table,
		List<SchemaColumnRow> actualColumns,
		string indexesSql,
		CancellationToken cancellationToken)
	{
		foreach (var ordinalColumn in table.OrdinallyComparedColumns)
		{
			var column = actualColumns.First(actual => string.Equals(actual.Name, ordinalColumn, StringComparison.OrdinalIgnoreCase));
			var collation = column.CollationName;

			if (collation is not null
				&& (collation.EndsWith("_BIN2", StringComparison.OrdinalIgnoreCase)
					|| collation.EndsWith("_BIN", StringComparison.OrdinalIgnoreCase)))
			{
				continue;
			}

			throw new ErasureStoreNotProvisionedException(
				$"Column '{table.TableName}.{ordinalColumn}' must carry a binary collation, and it reports "
				+ $"'{collation ?? "none"}'. This store compares that column ordinally and an erasure "
				+ "enumerates a data subject's keys by it. This server's default collation is "
				+ "case-insensitive, so a column that inherited it folds case: one tenant's rows answer for "
				+ "another's, an erasure destroys keys it does not own, and a completion certificate is "
				+ "signed over the result with nothing raised. The shipped create script declares COLLATE "
				+ "Latin1_General_BIN2 here, and supplies it only when creating the table -- so enabling "
				+ "automatic schema creation will NOT repair an existing one. Provision a new compliance "
				+ "schema from the shipped create script and restart.")
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
			if (primaryKey is null
				|| !primaryKey.Columns.SequenceEqual(table.PrimaryKeyColumns, StringComparer.OrdinalIgnoreCase))
			{
				throw new ErasureStoreNotProvisionedException(
					$"Table '{table.TableName}' must have primary key "
					+ $"({string.Join(", ", table.PrimaryKeyColumns)}), and it reports "
					+ $"({(primaryKey is null ? "none" : string.Join(", ", primaryKey.Columns))}). The primary "
					+ "key is what makes this store's insert-if-absent a single-row decision; without it the "
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
				required => index.Columns.Contains(required, StringComparer.OrdinalIgnoreCase))))
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
	/// binary collation. Only the columns the shipped create script collates explicitly appear here:
	/// declaring one the create script leaves to the server default would refuse every correctly provisioned
	/// database, which is the opposite of the failure this check exists to prevent.
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
	/// <param name="CollationName">The collation's name, and null for a type that cannot be collated.</param>
	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed record SchemaColumnRow(string Name, string? CollationName);

	/// <summary>One key column of one index, as the catalogue reports it.</summary>
	/// <param name="IndexId">The index's identity, so columns of different indexes are not merged.</param>
	/// <param name="IsPrimaryKey">Whether this index is the table's primary key.</param>
	/// <param name="KeyOrdinal">The column's one-based position among the index's key columns.</param>
	/// <param name="ColumnName">The column's name.</param>
	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed record SchemaIndexColumnRow(string IndexId, bool IsPrimaryKey, int KeyOrdinal, string ColumnName);

	/// <summary>
	/// Gets what every table must be for this store's statements to mean what they say.
	/// </summary>
	/// <remarks>
	/// The request columns are the union of the INSERT and the several UPDATE paths, not the INSERT alone.
	/// A request is created with a subset and then mutated through execution, completion and cancellation,
	/// so the columns only an UPDATE names -- the outcome and cancellation fields -- are exactly the ones a
	/// schema predating those features would lack, and the ones whose absence would otherwise surface at the
	/// end of an erasure rather than at startup.
	/// </remarks>
	private IEnumerable<TableContract> RequiredSchema =>
	[
		// TenantId and TenantPartition are declared because the create script collates them. DataSubjectIdHash
		// is NOT, and that is a KNOWN GAP rather than a statement that it needs no collation: the create
		// script leaves it to the server default, so requiring a binary collation here would refuse every
		// database the script provisions. Its values are framework-produced hex, so two distinct subjects'
		// hashes never differ only in case -- which is why the gap has no reachable consequence today and is
		// recorded rather than closed from this side.
		new TableContract(
			_options.FullRequestsTableName,
			[
				"RequestId", "DataSubjectIdHash", "IdType", "TenantId", "TenantPartition", "Scope", "LegalBasisV2",
				"ExternalReference", "RequestedBy", "RequestedAt", "ScheduledExecutionAt",
				"ExecutedAt", "CompletedAt", "CancelledAt", "CancellationReason", "CancelledBy",
				"Status", "KeysDeleted", "RecordsAffected", "CertificateId", "ErrorMessage",
				"DataCategories", "CreatedAt", "UpdatedAt",
				// Custody. Required rather than optional: without them the claim cannot record who holds
				// it, so an execution interrupted by a shutdown stays in-progress forever and every query
				// that looks for work correctly excludes it. A deployment provisioned before these
				// columns existed fails closed here, naming the script, instead of failing later on an
				// invalid column name far from its cause.
				"ClaimIdentity", "ClaimExpiresAt",
			],
			["RequestId"],
			["TenantId", "TenantPartition"],
			[]),
		new TableContract(
			_options.FullCertificatesTableName,
			[
				"CertificateId", "RequestId", "DataSubjectReference", "RequestReceivedAt", "CompletedAt",
				"Method", "Summary", "Verification", "LegalBasisV2", "Signature", "RetainUntil",
				"Exceptions", "UnreachedData", "GeneratedAt", "Version", "CreatedAt",
			],
			["CertificateId"],
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
			["KeyGeneration", "RequestId", "KeyHandle", "DestroyedAt", "RecordedBy"],
			["KeyHandle", "KeyGeneration"],
			["KeyGeneration", "KeyHandle"],
			[["RequestId", "KeyHandle"]]),

		// Verified for the same reason, and the failure it prevents is worse. Without this table a destruction
		// proceeds with nowhere to stage the generation it is about to annihilate, so a crash between the
		// destroy and the record leaves the subject's ciphertext permanently undecryptable AND their erasure
		// permanently unreportable, with no repair available at all. Naming the table at STARTUP is the only
		// place that is still cheap.
		new TableContract(
			_options.FullDestructionIntentsTableName,
			["RequestId", "KeyHandle", "KeyGeneration", "StagedAt"],
			["RequestId", "KeyHandle", "KeyGeneration"],
			["KeyHandle", "KeyGeneration"],
			[]),

		// Verified for the same reason as the two above, and the failure it prevents is the one this whole
		// subsystem exists to remove. Without this table the write path cannot record a mint, so either
		// personal-data writes fail loudly (the fail-closed direction, which is correct) or -- if a future
		// edit made recording tolerant -- an erasure enumerates nothing, destroys only the handles it can
		// derive, and signs a completion certificate over whatever it missed. Naming the table at STARTUP
		// is the only place that is still cheap.
		//
		// All three of its representation properties are declared, because all three are load-bearing: the
		// handle is what makes the mint row's insert-if-absent a single-row decision, the three compared
		// columns are what an enumeration folds across if a collation folds, and (TenantId,
		// DataSubjectIdHash) is the only key a subject's handles can be found by, a handle being an opaque
		// digest.
		new TableContract(
			_options.FullMintedKeysTableName,
			["KeyHandle", "TenantId", "DataSubjectIdHash", "IntentAt", "MintedAt"],
			["KeyHandle"],
			["KeyHandle", "TenantId", "DataSubjectIdHash"],
			[["TenantId", "DataSubjectIdHash"]]),

		// Verified like the others, so a database provisioned before this table existed fails at STARTUP
		// with the table named. Without it the store cannot say which token function produced its records,
		// and the only remaining option would be to assume nothing had changed — which is precisely the
		// silence this table exists to remove.
		//
		// THE FINGERPRINT IS COMPARED ORDINALLY IN MANAGED CODE, after being read back whole, so no
		// collation is declared here and none is required of the column. The Id is a framework constant
		// rather than consumer data. Declaring a collation would refuse databases the shipped script
		// provisions for a property nothing relies on.
		//
		// NO TENANT COLUMN IS DECLARED, and its absence is the design rather than an omission: the token
		// function belongs to the process, not to a tenant, and a per-tenant fingerprint would record a
		// rotated key as correct for a tenant onboarded afterwards while every earlier tenant was already
		// orphaned. The shipped script carries the reason, and the trigger it installs refuses a rewrite.
		new TableContract(
			_options.FullTokenFingerprintTableName,
			["Id", "Fingerprint", "RecordedAt"],
			["Id"],
			[],
			[]),

		// The tenant is in the KEY and nowhere else, which is what makes the staleness comparison's tenant
		// filter a key lookup rather than a predicate. Both key terms are compared ordinally by the
		// framework, so both carry an explicit binary collation: a database whose default folds case would
		// collapse two distinct aggregate types onto one row, and the comparison would then report a
		// declaration as recorded when the deployment had recorded a different one.
		new TableContract(
			_options.FullRetentionDeclarationsTableName,
			["TenantId", "AggregateType", "RecordedAt", "WithdrawnAt", "WithdrawnBy", "WithdrawalReason"],
			["TenantId", "AggregateType"],
			["TenantId", "AggregateType"],
			[["TenantId"]]),
	];
}
