// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Postgres.Requests;

/// <summary>
/// Data request that discovers aggregates with events eligible for archival to cold storage.
/// </summary>
/// <remarks>
/// Unlike every other request against this table, this query is deliberately <strong>not</strong>
/// partitioned by tenant: the archive service runs as a background pass over all tenants, so scoping the
/// enumeration would stall archival for every tenant except one. Isolation is preserved on the other two
/// legs instead — the tenant is <em>projected</em> here and carried on each candidate, then supplied
/// explicitly to the cold write and the hot delete, both of which are tenant-addressed.
/// </remarks>
[NoTenantTerm(
	TenantConfinement.EstateWide,
	"the archive scan is cross-tenant by design: one archiver serves every tenant, and the projection carries the tenant term so each candidate's owning partition is re-established before it is archived. Scoping this scan to the ambient tenant would leave every other tenant's events unarchived")]
public sealed class GetArchiveCandidatesRequest
	: DataRequestBase<IDbConnection, IReadOnlyList<ArchiveCandidate>>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GetArchiveCandidatesRequest"/> class.
	/// </summary>
	/// <param name="policy">The archive policy criteria.</param>
	/// <param name="batchSize">Maximum number of candidates to return.</param>
	/// <param name="utcNow">The current UTC time, supplied by the caller's time provider.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="schema">The schema name for the event store table. Default: "public".</param>
	/// <param name="table">The event store table name. Default: "event_store_events".</param>
	public GetArchiveCandidatesRequest(
		ArchivePolicy policy,
		int batchSize,
		DateTimeOffset utcNow,
		CancellationToken cancellationToken,
		string schema = "public",
		string table = "event_store_events")
		: this(policy, batchSize, utcNow, null, null, schema, table, cancellationToken)
	{
	}

	internal (string TenantId, string AggregateId, string AggregateType)? LastExamined { get; private set; }

	internal GetArchiveCandidatesRequest(
		ArchivePolicy policy, int batchSize, DateTimeOffset utcNow, long? scanHorizon,
		(string TenantId, string AggregateId, string AggregateType)? after,
		string schema, string table, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(policy);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

		var qualifiedTable = PgTableName.Format(schema, table);

		// Age and global-position triggers are alternatives. Retention overrides both and can stand alone.
		var conditions = new List<string>();
		var parameters = new DynamicParameters();
		if (policy.MaxAge is { } maxAge)
		{
			conditions.Add("EventTimestamp < @MaxAgeCutoff");
			parameters.Add("@MaxAgeCutoff", utcNow - maxAge);
		}
		if (policy.MaxPosition is { } maxPosition)
		{
			conditions.Add("GlobalPosition < @MaxPosition");
			parameters.Add("@MaxPosition", maxPosition);
		}
		var retain = policy.RetainRecentCount.GetValueOrDefault();
		var eligibility = conditions.Count > 0 ? "(" + string.Join(" OR ", conditions) + ")"
			: retain > 0 ? "1 = 1" : "1 = 0";
		parameters.Add("@RetainRecentCount", retain);
		parameters.Add("@ErasedEventType", ErasedEventMarker.EventType);

		if (scanHorizon.HasValue)
		{
			parameters.Add("@ScanHorizon", scanHorizon);
			parameters.Add("@HasAfter", after.HasValue ? 1 : 0);
			parameters.Add("@AfterTenant", after?.TenantId);
			parameters.Add("@AfterId", after?.AggregateId);
			parameters.Add("@AfterType", after?.AggregateType);
		}
		parameters.Add("@BatchSize", batchSize);
		parameters.Add("@UntenantedSentinel", KeyedTenantPartition.Untenanted.TenantId);

		// The scanner matches raw reads/tombstoning: only NULL maps to untenanted; malformed blank
		// identities fail below. Preserve legacy one-shot normalization for its existing callers.
		var tenantTerm = scanHorizon.HasValue ? "COALESCE(tenant_id, @UntenantedSentinel)" :
			"CASE WHEN BTRIM(COALESCE(tenant_id, '')) = '' THEN @UntenantedSentinel ELSE tenant_id END";

		var fullProjection = $"{tenantTerm} AS TenantId, aggregate_id AS AggregateId, aggregate_type AS AggregateType, version AS Version, event_type AS EventType, event_data AS EventData, archived_at AS ArchivedAt, timestamp AS EventTimestamp, position AS GlobalPosition";
		var initialProjection = scanHorizon.HasValue ? $"{tenantTerm} AS TenantId, aggregate_id AS AggregateId, aggregate_type AS AggregateType, position AS GlobalPosition" : fullProjection;

		var scanSelection = scanHorizon.HasValue ? $"""
			, scan_keys AS (
			    SELECT TenantId, AggregateId, AggregateType
			    FROM stream_rows
			    WHERE GlobalPosition <= @ScanHorizon AND
			        (@HasAfter = 0 OR TenantId > @AfterTenant
			         OR (TenantId = @AfterTenant AND AggregateId > @AfterId)
			         OR (TenantId = @AfterTenant AND AggregateId = @AfterId AND AggregateType > @AfterType))
			    GROUP BY TenantId, AggregateId, AggregateType
			    ORDER BY TenantId, AggregateId, AggregateType
			    LIMIT @BatchSize
			), selected_rows AS (
			    SELECT s.* FROM (SELECT {fullProjection} FROM {qualifiedTable}) s INNER JOIN scan_keys k
			      ON s.TenantId = k.TenantId AND s.AggregateId = k.AggregateId AND s.AggregateType = k.AggregateType
			)
			""" : string.Empty;
		var rankingSource = scanHorizon.HasValue ? "selected_rows" : "stream_rows";
		var legacySelection = $"""
			SELECT TenantId, AggregateId, AggregateType,
			       MAX(Version) AS ArchivableUpToVersion,
			       SUM(CASE WHEN EventData IS NOT NULL THEN 1 ELSE 0 END) AS EventCount
			FROM bounded
			WHERE FirstBlockedVersion IS NULL OR Version < FirstBlockedVersion
			GROUP BY TenantId, AggregateId, AggregateType
			HAVING SUM(CASE WHEN EventData IS NOT NULL THEN 1 ELSE 0 END) > 0
			ORDER BY TenantId ASC, AggregateId ASC, AggregateType ASC
			    LIMIT @BatchSize
			""";
		var selection = scanHorizon.HasValue ? """
			, eligible AS (
			    SELECT TenantId, AggregateId, AggregateType, MAX(Version) AS ArchivableUpToVersion,
			           SUM(CASE WHEN EventData IS NOT NULL THEN 1 ELSE 0 END) AS EventCount
			    FROM bounded WHERE FirstBlockedVersion IS NULL OR Version < FirstBlockedVersion
			    GROUP BY TenantId, AggregateId, AggregateType
			)
			SELECT k.TenantId, k.AggregateId, k.AggregateType,
			       COALESCE(e.ArchivableUpToVersion, -1) AS ArchivableUpToVersion,
			       COALESCE(e.EventCount, 0) AS EventCount
			FROM scan_keys k LEFT JOIN eligible e
			  ON k.TenantId = e.TenantId AND k.AggregateId = e.AggregateId AND k.AggregateType = e.AggregateType
			ORDER BY k.TenantId, k.AggregateId, k.AggregateType
			""" : legacySelection;

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in PgTableName.Format
		// A maximum eligible version alone is unsafe when event timestamps are not monotonic.
		// Find the first barrier, then select only the prefix before it. Already archived markers
		// can be crossed but do not consume the batch's work budget. Ranking handles sparse versions.
		var sql = $"""
			WITH stream_rows AS (
			    SELECT {initialProjection}
			    FROM {qualifiedTable}
			)
			{scanSelection}
			, ranked AS (
			    SELECT *, ROW_NUMBER() OVER (
			        PARTITION BY TenantId, AggregateId, AggregateType ORDER BY Version DESC) AS RetentionRank
			    FROM {rankingSource}
			), bounded AS (
			    SELECT *, MIN(CASE
			        WHEN EventType = @ErasedEventType
			          OR (EventData IS NULL AND ArchivedAt IS NULL)
			          OR (EventData IS NOT NULL AND (CASE WHEN {eligibility} THEN 1 ELSE 0 END = 0 OR RetentionRank <= @RetainRecentCount))
			        THEN Version ELSE NULL END) OVER (
			            PARTITION BY TenantId, AggregateId, AggregateType) AS FirstBlockedVersion
			    FROM ranked
			)
			{selection}
			""";
#pragma warning restore CA2100

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var rows = await connection.QueryAsync<ArchiveCandidateRow>(Command).ConfigureAwait(false);

			var candidates = new List<ArchiveCandidate>();
			foreach (var row in rows)
			{
				// Missing projections and malformed legacy identities are errors, never an implicit tenant.
				if (string.IsNullOrWhiteSpace(row.TenantId))
				{
					throw new InvalidOperationException(
						"The archive-candidate projection did not supply a tenant term. Every row must carry "
						+ "one — the reserved untenanted sentinel for rows written before tenancy, never a "
						+ "blank. Treating this as untenanted would archive tenant-owned events under the "
						+ "untenanted key.");
				}

				// Total by construction for every value the store can actually produce, sentinel included.
				var tenant = KeyedTenantPartition.FromStoredValue(row.TenantId);
				LastExamined = (row.TenantId, row.AggregateId, row.AggregateType);
				if (row.EventCount <= 0 || row.ArchivableUpToVersion < 0)
				{
					continue;
				}

				candidates.Add(new ArchiveCandidate(
					tenant,
					row.AggregateId,
					row.AggregateType,
					row.ArchivableUpToVersion,
					row.EventCount));
			}

			return candidates;
		};
	}

	/// <summary>
	/// Row shape returned by the discovery query, mapped to <see cref="ArchiveCandidate"/> once the tenant
	/// term has been routed through <see cref="KeyedTenantPartition"/>.
	/// </summary>
	private sealed class ArchiveCandidateRow
	{
		// Deliberately NOT defaulted. Dapper leaves a property untouched when the result set has no matching
		// column, so an initializer here would turn a broken projection into a well-formed-looking blank
		// that the mapping below folds to Untenanted — silently. Nullable-with-no-default makes that state
		// reachable and therefore rejectable.
		public string? TenantId { get; init; }

		public string AggregateId { get; init; } = string.Empty;

		public string AggregateType { get; init; } = string.Empty;

		public long ArchivableUpToVersion { get; init; }

		public int EventCount { get; init; }
	}
}
