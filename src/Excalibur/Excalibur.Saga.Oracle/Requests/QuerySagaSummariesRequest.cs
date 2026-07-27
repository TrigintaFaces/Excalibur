// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Saga.Oracle.Requests;

/// <summary>
/// Queries saga instance summaries matching a filter (completion state, tenant) with pagination,
/// ordered by saga id.
/// </summary>
internal sealed class QuerySagaSummariesRequest : DataRequestBase<IDbConnection, IReadOnlyList<SagaInstanceSummary>>
{
	public QuerySagaSummariesRequest(
		SagaQueryFilter filter,
		string qualifiedTableName,
		TenantScope scope,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(filter);
		ArgumentException.ThrowIfNullOrWhiteSpace(qualifiedTableName);
		SagaSqlValidator.ThrowIfInvalidQualifiedName(qualifiedTableName);

		// The ambient scope is authoritative and is INTERSECTED with the caller's filter — never substituted
		// by it. The filter's tenant can only narrow the ambient scope further; supplying a different tenant
		// yields no rows rather than that tenant's rows, so the filter cannot widen the caller's own scope.
		// The ambient term is UNCONDITIONAL. It previously vanished on the unscoped path, which was written for a
		// single-tenant deployment — and in one, every row carries the untenanted sentinel, so an equality
		// predicate returns exactly the same rows and that use is unaffected. What the omission also did was hand
		// a MULTI-tenant deployment every tenant's summaries whenever the ambient scope happened to be unset.
		var partition = KeyedTenantPartition.FromScope(scope);
		const string tenantPredicate = " AND TenantId = :TenantId";

		// ANSI OFFSET/FETCH paging (Oracle 12c+). :IsCompleted is a nullable NUMBER(1) filter.
		var sql = $"""
			SELECT SagaId, SagaType, IsCompleted, CompletedAt, TenantId, Version
			FROM {qualifiedTableName}
			WHERE (:IsCompleted IS NULL OR IsCompleted = :IsCompleted)
			  AND (:FilterTenantId IS NULL OR TenantId = :FilterTenantId){tenantPredicate}
			ORDER BY SagaId
			OFFSET :Skip ROWS FETCH NEXT :Take ROWS ONLY
			""";

		// OracleDynamicParameters sets OracleCommand.BindByName = true, so these bind by name. The add order
		// below nonetheless mirrors the placeholder order in the SQL above, so the request stays correct if
		// BindByName is ever lost — the positional footgun this package documents elsewhere.
		var dp = new DynamicParameters();
		dp.Add("IsCompleted", filter.IsCompleted is null ? (int?)null : (filter.IsCompleted.Value ? 1 : 0));
		dp.Add("FilterTenantId", filter.TenantId);
		dp.Add("TenantId", partition.TenantId);
		dp.Add("Skip", Math.Max(0, filter.Skip));
		dp.Add("Take", Math.Max(0, filter.MaxResults));

		Command = new CommandDefinition(sql, new OracleDynamicParameters(dp), cancellationToken: cancellationToken);
		ResolveAsync = async conn =>
		{
			var rows = await conn.QueryAsync<OracleSagaSummaryRow>(Command).ConfigureAwait(false);

			IReadOnlyList<SagaInstanceSummary> summaries = rows.Select(static r => r.ToSummary()).ToArray();
			return summaries;
		};
	}
}

/// <summary>
/// Oracle-shaped projection of a saga summary row.
/// </summary>
/// <remarks>
/// Oracle returns <c>NUMBER(19)</c> and <c>NUMBER(1)</c> as <see cref="decimal"/>, which Dapper cannot bind to
/// a positional <c>long</c>/<c>bool</c> constructor parameter — a tuple or positional record materialization
/// throws. Settable properties are converted on assignment, so the provider's native types are read here and
/// narrowed explicitly in <see cref="ToSummary"/>.
/// <para>
/// Scoped to this provider on purpose: a global Dapper type handler would alter <c>long</c> binding for every
/// other database in the same process.
/// </para>
/// </remarks>
internal sealed class OracleSagaSummaryRow
{
	// Oracle returns the RAW(16) SagaId column as raw bytes; Dapper cannot assign byte[] to a Guid property.
	// Read the provider's native byte[] and reconstruct the Guid explicitly in ToSummary — the inverse of the
	// RAW(16) .ToByteArray() bind, so the summary round-trips the Guid byte-for-byte.
	public byte[] SagaId { get; set; } = [];

	public string SagaType { get; set; } = string.Empty;

	public decimal IsCompleted { get; set; }

	public DateTimeOffset? CompletedAt { get; set; }

	public string? TenantId { get; set; }

	public decimal Version { get; set; }

	public SagaInstanceSummary ToSummary() => new()
	{
		SagaId = new Guid(SagaId),
		SagaType = SagaType,
		IsCompleted = IsCompleted != 0,
		CompletedAt = CompletedAt,
		TenantId = TenantId,
		Version = (long)Version,
	};
}

/// <summary>Fetches a single saga instance summary by id (type-agnostic), or null when absent.</summary>
internal sealed class GetSagaSummaryRequest : DataRequestBase<IDbConnection, SagaInstanceSummary?>
{
	public GetSagaSummaryRequest(
		Guid sagaId,
		string qualifiedTableName,
		TenantScope scope,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(qualifiedTableName);
		SagaSqlValidator.ThrowIfInvalidQualifiedName(qualifiedTableName);

		// Unconditional tenant term: this returns saga CONTENT (type, completion state, owning tenant) keyed by
		// SagaId, so an omitted term on the unscoped path resolved a single id to whichever tenant happened to
		// hold it. An unscoped caller now reads the untenanted partition via the reserved sentinel.
		var partition = KeyedTenantPartition.FromScope(scope);
		const string tenantPredicate = " AND TenantId = :TenantId";
		var sql = $"""
			SELECT SagaId, SagaType, IsCompleted, CompletedAt, TenantId, Version
			FROM {qualifiedTableName}
			WHERE SagaId = :SagaId{tenantPredicate}
			""";

		var dp = new DynamicParameters();
		// ODP.NET has no native Guid bind type; a raw Guid throws. Bind the 16 canonical bytes as RAW(16),
		// symmetric with SaveSagaRequest/LoadSagaRequest so this WHERE-clause value matches the stored
		// RAW(16) SagaId byte-for-byte (the third bind site of the g3nxci Guid RAW(16) parity fix).
		dp.Add("SagaId", sagaId.ToByteArray());
		dp.Add("TenantId", partition.TenantId);

		Command = new CommandDefinition(sql, new OracleDynamicParameters(dp), cancellationToken: cancellationToken);
		ResolveAsync = async conn =>
		{
			var row = await conn.QuerySingleOrDefaultAsync<OracleSagaSummaryRow>(Command).ConfigureAwait(false);

			return row?.ToSummary();
		};
	}
}

/// <summary>Computes aggregate saga counts (running / completed / total).</summary>
internal sealed class GetSagaStatisticsRequest : DataRequestBase<IDbConnection, SagaStoreStatistics>
{
	public GetSagaStatisticsRequest(
		string qualifiedTableName,
		TenantScope scope,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(qualifiedTableName);
		SagaSqlValidator.ThrowIfInvalidQualifiedName(qualifiedTableName);

		// The ambient tenant predicate is unconditional: a scoped caller's counts reflect only their own
		// tenant. There is no caller input to compose with here, so scope is emitted directly rather than
		// intersected. An unscoped store emits no predicate and still returns estate-wide counts, which is
		// the legitimate operator diagnostic — scoping it away would break that use rather than secure it.
		var tenantPredicate = scope.IsScoped ? " WHERE TenantId = :TenantId" : string.Empty;

		var sql = $"""
			SELECT COUNT(*) AS Total,
			       COALESCE(SUM(CASE WHEN IsCompleted = 1 THEN 1 ELSE 0 END), 0) AS Completed
			FROM {qualifiedTableName}{tenantPredicate}
			""";

		var dp = new DynamicParameters();

		if (scope.IsScoped)
		{
			dp.Add("TenantId", scope.TenantId);
		}

		Command = new CommandDefinition(sql, new OracleDynamicParameters(dp), cancellationToken: cancellationToken);
		ResolveAsync = async conn =>
		{
			// COUNT and SUM come back from Oracle as NUMBER, which Dapper surfaces as decimal and cannot bind
			// to a positional int tuple. Read the provider's type, then narrow.
			var counts = await conn.QuerySingleAsync<OracleSagaCountsRow>(Command).ConfigureAwait(false);
			var total = (int)counts.Total;
			var completed = (int)counts.Completed;

			return new SagaStoreStatistics
			{
				RunningCount = total - completed,
				CompletedCount = completed,
				TotalCount = total,
				CapturedAt = DateTimeOffset.UtcNow,
			};
		};
	}
}

/// <summary>
/// Oracle-shaped projection of the saga count aggregates.
/// </summary>
/// <remarks>
/// Oracle's <c>COUNT</c> and <c>SUM</c> return <c>NUMBER</c>, surfaced by the provider as <see cref="decimal"/>.
/// Settable properties are converted on assignment; a positional <c>int</c> tuple is not.
/// </remarks>
internal sealed class OracleSagaCountsRow
{
	public decimal Total { get; set; }

	public decimal Completed { get; set; }
}
