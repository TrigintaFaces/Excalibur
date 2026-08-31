// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Saga.SqlServer.Requests;

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

		// The ambient scope is authoritative and is INTERSECTED with the caller's filter — it is never
		// substituted by it. The filter's tenant can only narrow the ambient scope further; supplying a
		// different tenant yields no rows rather than that tenant's rows, so the filter cannot be used to
		// widen the caller's own scope.
		//
		// The ambient term is UNCONDITIONAL. It previously vanished on the unscoped path, which was written
		// for a single-tenant deployment — and in one, every row carries the untenanted sentinel, so an
		// equality predicate against it returns exactly the same rows and that use is unaffected. What the
		// omission also did, however, was hand a MULTI-tenant deployment every tenant's summaries (saga type,
		// completion state, tenant id) whenever the ambient scope happened to be unset. Emitting the term
		// always means an unscoped caller reads the untenanted partition instead of the estate.
		var partition = KeyedTenantPartition.FromScope(scope);
		const string tenantPredicate = " AND TenantId = @TenantId";

		var sql = $"""
			SELECT SagaId, SagaType, IsCompleted, CompletedAt, TenantId, Version
			FROM {qualifiedTableName}
			WHERE (@IsCompleted IS NULL OR IsCompleted = @IsCompleted)
			  AND (@FilterTenantId IS NULL OR TenantId = @FilterTenantId){tenantPredicate}
			ORDER BY SagaId
			OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
			""";

		Parameters.Add("IsCompleted", filter.IsCompleted);
		Parameters.Add("FilterTenantId", filter.TenantId);
		Parameters.Add("Skip", Math.Max(0, filter.Skip));
		Parameters.Add("Take", Math.Max(0, filter.MaxResults));
		Parameters.Add("TenantId", partition.TenantId);

		Command = CreateCommand(sql, cancellationToken: cancellationToken);
		ResolveAsync = async conn =>
		{
			var rows = await conn.QueryAsync<(Guid SagaId, string SagaType, bool IsCompleted, DateTimeOffset? CompletedAt, string? TenantId, long Version)>(Command)
				.ConfigureAwait(false);

			IReadOnlyList<SagaInstanceSummary> summaries = rows.Select(static r => new SagaInstanceSummary
			{
				SagaId = r.SagaId,
				SagaType = r.SagaType,
				IsCompleted = r.IsCompleted,
				CompletedAt = r.CompletedAt,
				TenantId = r.TenantId,
				Version = r.Version,
			}).ToArray();
			return summaries;
		};
	}
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
		var sql = $"""
			SELECT SagaId, SagaType, IsCompleted, CompletedAt, TenantId, Version
			FROM {qualifiedTableName}
			WHERE SagaId = @SagaId AND TenantId = @TenantId;
			""";

		Parameters.Add("SagaId", sagaId);
		Parameters.Add("TenantId", partition.TenantId);

		Command = CreateCommand(sql, cancellationToken: cancellationToken);
		ResolveAsync = async conn =>
		{
			// Project into a nullable reference type so "no row" is unambiguously null. A value-tuple
			// `== default` conflates an absent row with a row whose columns all equal their defaults.
			var row = await conn.QuerySingleOrDefaultAsync<SagaSummaryRow>(Command).ConfigureAwait(false);

			return row is null
				? null
				: new SagaInstanceSummary
				{
					SagaId = row.SagaId,
					SagaType = row.SagaType,
					IsCompleted = row.IsCompleted,
					CompletedAt = row.CompletedAt,
					TenantId = row.TenantId,
					Version = row.Version,
				};
		};
	}
}

/// <summary>Computes aggregate saga counts (running / completed / total).</summary>
internal sealed class GetSagaStatisticsRequest : DataRequestBase<IDbConnection, SagaStoreStatistics>
{
	public GetSagaStatisticsRequest(
		string qualifiedTableName,
		TenantScope scope,
		CancellationToken cancellationToken,
		bool allTenants = false)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(qualifiedTableName);
		SagaSqlValidator.ThrowIfInvalidQualifiedName(qualifiedTableName);

		// Two intents, neither reachable from the other by omission:
		//
		//   allTenants    no discriminator          the operator diagnostic -- named at the call site
		//   otherwise     TenantId = @TenantId    exactly the ambient partition
		//
		// The scoped predicate is UNCONDITIONAL. It used to be emitted only when scope.IsScoped, on the belief
		// that an unscoped store would fall through to estate-wide counts -- but a scope resolved from an
		// ITenantContext is always scoped, so that branch could never be taken and the estate-wide read had no
		// reachable caller at all. The untenanted partition is a real partition addressed by the same equality
		// predicate as a real tenant (the column is NOT NULL and an untenanted row carries the reserved
		// sentinel), so routing through KeyedTenantPartition makes one predicate serve both.
		var partition = KeyedTenantPartition.FromScope(scope);
		var tenantPredicate = allTenants ? string.Empty : " WHERE TenantId = @TenantId";

		var sql = $"""
			SELECT COUNT(*) AS Total,
			       COALESCE(SUM(CASE WHEN IsCompleted = 1 THEN 1 ELSE 0 END), 0) AS Completed
			FROM {qualifiedTableName}{tenantPredicate};
			""";

		if (!allTenants)
		{
			Parameters.Add("TenantId", partition.TenantId);
		}

		Command = CreateCommand(sql, cancellationToken: cancellationToken);
		ResolveAsync = async conn =>
		{
			var (total, completed) = await conn.QuerySingleAsync<(int Total, int Completed)>(Command).ConfigureAwait(false);
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

/// <summary>Row projection for a saga summary query. A null instance means no matching row exists.</summary>
internal sealed record SagaSummaryRow(
	Guid SagaId,
	string SagaType,
	bool IsCompleted,
	DateTimeOffset? CompletedAt,
	string? TenantId,
	long Version);
