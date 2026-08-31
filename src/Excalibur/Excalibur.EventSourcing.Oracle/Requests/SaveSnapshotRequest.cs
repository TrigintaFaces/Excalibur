// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Text.Json;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;

namespace Excalibur.EventSourcing.Oracle.Requests;

/// <summary>
/// Data request to save (upsert) a snapshot for an aggregate. Uses Oracle <c>MERGE</c> for atomic
/// insert-or-update semantics, keyed on the aggregate identity, and only advances to a newer version.
/// </summary>
public sealed class SaveSnapshotRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SaveSnapshotRequest"/> class.
	/// </summary>
	/// <param name="snapshot">The snapshot to save.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="scope">
	/// The tenant scope. The tenant predicate is emitted unconditionally: an untenanted scope binds the
	/// reserved sentinel rather than omitting the term, so the statement is always restricted to a single
	/// partition's rows.
	/// </param>
	/// <param name="schema">The schema name for the snapshot store table.</param>
	/// <param name="table">The snapshot store table name.</param>
	public SaveSnapshotRequest(
		ISnapshot snapshot,
		TenantScope scope,
		CancellationToken cancellationToken,
		string schema = "EXCALIBUR",
		string table = "EVENTSTORESNAPSHOTS")
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		var qualifiedTable = OracleTableName.Format(schema, table);
		// The tenant belongs in the MERGE MATCH KEY, not merely in the inserted columns: matching on the
		// aggregate alone means a second tenant's save MATCHES the first tenant's row and overwrites it.
		// Three coupled fragments, all or none. The ON clause references source.TENANTID, so the USING
		// select MUST project it or Oracle raises ORA-00904 on every scoped save; and the INSERT must
		// WRITE it or a keyed-but-unwritten column puts every tenant on one row -- the defect this fix
		// exists to remove. Bind order matters: ODP.NET binds by position, so :TenantIdK sits exactly
		// where it appears in the USING list and :TenantIdI last in the INSERT values.
		// UNCONDITIONAL, every fragment. The schema keys on the triple, so matching on only the
		// first two columns matches a SUBSET of the key: an unscoped save could MATCH a tenant's
		// row and overwrite it.
		//
		// The tenant is bound through KeyedTenantPartition, so an untenanted save carries the reserved
		// '__untenanted__' value rather than NULL. An even earlier revision wrote NVL(:TenantIdK, '') to
		// "produce the sentinel explicitly" -- but NVL's replacement value IS '', which Oracle converts to
		// NULL, so it produced NULL while the read side compared against it with `=`, and untenanted rows
		// became unreadable. The lesson survives the fix: a sentinel must never be manufactured in SQL on
		// this provider. It is a real, non-empty value supplied by the caller, or it is NULL by accident.
		const string tenantSourceColumn = ", :TenantIdK AS TENANTID";

		// Plain equality. This was previously a NULL-SAFE match, and the reason is worth recording because
		// it is the same shape as the function-based index this convergence also removed:
		//
		//   While TENANTID was nullable, an untenanted save had NULL on both sides, and `NULL = NULL` is
		//   UNKNOWN in SQL -- never true. A bare equality would have taken WHEN NOT MATCHED on every
		//   unscoped save and INSERTed another row, forever, silently: a duplicate-row leak that no read
		//   predicate can fix and that reports success every time. The OR-arm made two untenanted rows
		//   match each other so the upsert stayed an upsert.
		//
		// With TENANTID NOT NULL there are no NULLs on either side, so the OR-arm can never fire and the
		// three-valued-logic hazard cannot arise. It is deleted rather than kept "for safety": a dead arm
		// that silently re-admits NULL semantics is how the nullable assumption would outlive the column.
		const string tenantKeyPredicate = " AND target.TENANTID = source.TENANTID";
		const string tenantInsertColumn = ", TENANTID";
		const string tenantInsertValue = ", source.TENANTID";

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in OracleTableName.Format
		// ODP.NET binds by position by default; every placeholder is unique and parameters are added in
		// the exact left-to-right order they appear in the SQL text so the statement binds correctly
		// regardless of BindByName. A MERGE reuses each value in both branches, hence the _k/_u/_i suffixes.
		var sql = $"""
			MERGE INTO {qualifiedTable} target
			USING (SELECT :AggregateIdK AS AGGREGATEID, :AggregateTypeK AS AGGREGATETYPE{tenantSourceColumn} FROM DUAL) source
			ON (target.AGGREGATEID = source.AGGREGATEID AND target.AGGREGATETYPE = source.AGGREGATETYPE{tenantKeyPredicate})
			WHEN MATCHED THEN
			    UPDATE SET target.SNAPSHOTID = :SnapshotIdU,
			               target.VERSION = :VersionU,
			               target.DATA = :DataU,
			               target.CREATEDAT = :CreatedAtU,
			               target.METADATA = :MetadataU
			    WHERE :VersionCmpU > target.VERSION
			WHEN NOT MATCHED THEN
			    INSERT (SNAPSHOTID, AGGREGATEID, AGGREGATETYPE, VERSION, DATA, CREATEDAT, METADATA{tenantInsertColumn})
			    VALUES (:SnapshotIdI, :AggregateIdI, :AggregateTypeI, :VersionI, :DataI, :CreatedAtI, :MetadataI{tenantInsertValue})
			""";
#pragma warning restore CA2100

		var data = snapshot.Data.ToArray();
		var metadata = SerializeMetadata(snapshot.Metadata);

		var parameters = new DynamicParameters();
		// USING (key)
		parameters.Add(":AggregateIdK", snapshot.AggregateId);
		parameters.Add(":AggregateTypeK", snapshot.AggregateType);
		parameters.Add(":TenantIdK", KeyedTenantPartition.FromScope(scope).TenantId);
		// WHEN MATCHED (update)
		parameters.Add(":SnapshotIdU", snapshot.SnapshotId);
		parameters.Add(":VersionU", snapshot.Version);
		parameters.Add(":DataU", new OracleBlobParameter(data));
		parameters.Add(":CreatedAtU", snapshot.CreatedAt);
		parameters.Add(":MetadataU", new OracleBlobParameter(metadata));
		parameters.Add(":VersionCmpU", snapshot.Version);
		// WHEN NOT MATCHED (insert)
		parameters.Add(":SnapshotIdI", snapshot.SnapshotId);
		parameters.Add(":AggregateIdI", snapshot.AggregateId);
		parameters.Add(":AggregateTypeI", snapshot.AggregateType);
		parameters.Add(":VersionI", snapshot.Version);
		parameters.Add(":DataI", new OracleBlobParameter(data));
		parameters.Add(":CreatedAtI", snapshot.CreatedAt);
		parameters.Add(":MetadataI", new OracleBlobParameter(metadata));

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}

	/// <summary>
	/// Serializes the snapshot metadata dictionary to a binary payload for storage so that the
	/// schema-version entry consumed by snapshot upgrading round-trips. Null metadata is stored as
	/// SQL NULL; an empty dictionary is preserved as empty.
	/// </summary>
	private static byte[]? SerializeMetadata(IDictionary<string, object>? metadata)
	{
		if (metadata is null)
		{
			return null;
		}

#pragma warning disable IL2026, IL3050 // Metadata serialization inherently uses reflection (matches SqlServerEventStore precedent)
		return JsonSerializer.SerializeToUtf8Bytes(metadata);
#pragma warning restore IL2026, IL3050
	}
}
