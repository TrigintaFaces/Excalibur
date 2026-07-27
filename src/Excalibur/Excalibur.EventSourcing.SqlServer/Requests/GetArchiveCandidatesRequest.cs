// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.SqlServer.Requests;

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
	/// <param name="schema">The schema name for the event store table. Default: "dbo".</param>
	/// <param name="table">The event store table name. Default: "EventStoreEvents".</param>
	public GetArchiveCandidatesRequest(
		ArchivePolicy policy,
		int batchSize,
		DateTimeOffset utcNow,
		CancellationToken cancellationToken,
		string schema = "dbo",
		string table = "EventStoreEvents")
	{
		ArgumentNullException.ThrowIfNull(policy);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

		var qualifiedTable = SqlTableName.Format(schema, table);

		// Each policy term narrows the archivable ceiling; an unset term contributes nothing. When no term is
		// set the HAVING clause below still applies, so a policy that enables nothing yields no candidates
		// rather than proposing every event for deletion.
		var conditions = new List<string>();
		var parameters = new DynamicParameters();

		if (policy.MaxAge is { } maxAge)
		{
			conditions.Add("Timestamp < @MaxAgeCutoff");
			parameters.Add("@MaxAgeCutoff", utcNow - maxAge);
		}

		if (policy.MaxPosition is { } maxPosition)
		{
			conditions.Add("Version <= @MaxPosition");
			parameters.Add("@MaxPosition", maxPosition);
		}

		var eligibility = conditions.Count > 0
			? string.Join(" AND ", conditions)
			: "1 = 0";

		// RetainRecentCount keeps the newest N versions per aggregate out of the archivable range. It is
		// applied against the aggregate's true maximum version, which is computed per (tenant, aggregate)
		// group — the tenant is part of the grouping key, so two tenants sharing an aggregate identifier are
		// never folded into one candidate.
		var retainClause = policy.RetainRecentCount is { } retain && retain > 0
			? "HAVING MAX(Version) - MAX(CASE WHEN " + eligibility + " THEN Version ELSE -1 END) >= @RetainRecentCount"
			: string.Empty;

		if (policy.RetainRecentCount is { } retainCount && retainCount > 0)
		{
			parameters.Add("@RetainRecentCount", retainCount);
		}

		parameters.Add("@BatchSize", batchSize);
		parameters.Add("@UntenantedSentinel", KeyedTenantPartition.Untenanted.TenantId);

		// Folds every value that cannot name a real tenant — NULL, empty, and whitespace-only, all of which
		// occur on schemas predating the NOT NULL/CHECK constraints — onto the one reserved sentinel, so the
		// untenanted rows form a single group instead of one group per blank spelling. A real tenant term is
		// projected verbatim: the trim is the emptiness *test* only, never a normalization of the identifier.
		const string TenantTerm =
			"CASE WHEN LTRIM(RTRIM(COALESCE(TenantId, ''))) = '' THEN @UntenantedSentinel ELSE TenantId END";

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in SqlTableName.Format
		var sql = $"""
			SELECT TOP (@BatchSize)
			       {TenantTerm} AS TenantId,
			       AggregateId,
			       AggregateType,
			       MAX(CASE WHEN {eligibility} THEN Version ELSE -1 END) AS ArchivableUpToVersion,
			       SUM(CASE WHEN {eligibility} THEN 1 ELSE 0 END) AS EventCount
			FROM {qualifiedTable}
			GROUP BY {TenantTerm}, AggregateId, AggregateType
			{retainClause}
			ORDER BY AggregateId ASC
			""";
#pragma warning restore CA2100

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var rows = await connection.QueryAsync<ArchiveCandidateRow>(Command).ConfigureAwait(false);

			var candidates = new List<ArchiveCandidate>();
			foreach (var row in rows)
			{
				// A group with nothing eligible yields -1 / 0 from the CASE aggregates; skip it rather than
				// proposing a candidate whose archivable range is empty.
				if (row.EventCount <= 0 || row.ArchivableUpToVersion < 0)
				{
					continue;
				}

				// An ABSENT column and an untenanted ROW are different failures and must not converge. The
				// projection above folds null/empty/whitespace onto the sentinel, so it can never yield a
				// blank tenant term — therefore a blank one here means the column was not supplied at all
				// (an alias dropped, a GROUP BY refactored) and Dapper left the property untouched. That is
				// a broken query, not a legacy row: fail loud rather than silently archiving every tenant's
				// events under the untenanted key and deleting them from hot.
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
