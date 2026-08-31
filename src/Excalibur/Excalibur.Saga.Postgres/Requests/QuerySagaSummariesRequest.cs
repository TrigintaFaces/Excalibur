// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Saga.Postgres;

/// <summary>
/// Queries saga instance summaries matching a filter (completion state, tenant) with pagination,
/// ordered by saga id.
/// </summary>
internal sealed class QuerySagaSummariesRequest : DataRequestBase<IDbConnection, IReadOnlyList<SagaInstanceSummary>>
{
	public QuerySagaSummariesRequest(
		SagaQueryFilter filter,
		PostgresSagaOptions options,
		TenantScope scope,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(filter);
		ArgumentNullException.ThrowIfNull(options);
		SagaSqlValidator.ThrowIfInvalidQualifiedName(options.QualifiedTableName);

		// The ambient scope is authoritative and is INTERSECTED with the caller's filter — never substituted
		// by it. The filter's tenant can only narrow the ambient scope further; supplying a different tenant
		// yields no rows rather than that tenant's rows, so the filter cannot widen the caller's own scope.
		// The ambient term is UNCONDITIONAL. It previously vanished on the unscoped path, which was written for
		// a single-tenant deployment — and in one, every row carries the untenanted sentinel, so an equality
		// predicate returns exactly the same rows and that use is unaffected. What the omission also did was
		// hand a MULTI-tenant deployment every tenant's summaries (saga type, completion state, tenant id)
		// whenever the ambient scope happened to be unset. The ambient term binds a non-null value, so it
		// needs no ::text cast — only the nullable filter params do.
		var partition = KeyedTenantPartition.FromScope(scope);
		const string tenantPredicate = " AND tenant_id = @TenantId";

		// Cast the nullable filter params to their concrete PG types: an untyped NULL parameter used only
		// in "@p IS NULL OR col = @p" is indeterminate to the planner (SQLSTATE 42P08). The explicit
		// ::boolean / ::text lets Postgres determine the type even when the bound value is NULL.
		var sql = $"""
			SELECT saga_id, saga_type, is_completed, completed_at, tenant_id, version
			FROM {options.QualifiedTableName}
			WHERE (@IsCompleted::boolean IS NULL OR is_completed = @IsCompleted::boolean)
			  AND (@FilterTenantId::text IS NULL OR tenant_id = @FilterTenantId::text){tenantPredicate}
			ORDER BY saga_id
			LIMIT @Take OFFSET @Skip;
			""";

		Parameters.Add("IsCompleted", filter.IsCompleted);
		Parameters.Add("FilterTenantId", filter.TenantId);
		Parameters.Add("Take", Math.Max(0, filter.MaxResults));
		Parameters.Add("Skip", Math.Max(0, filter.Skip));
		Parameters.Add("TenantId", partition.TenantId);

		Command = CreateCommand(sql, commandTimeout: options.CommandTimeoutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn =>
		{
			var rows = await conn.QueryAsync<SagaSummaryRow>(Command).ConfigureAwait(false);
			IReadOnlyList<SagaInstanceSummary> summaries = rows.Select(static r => new SagaInstanceSummary
			{
				SagaId = r.saga_id,
				SagaType = r.saga_type,
				IsCompleted = r.is_completed,
				CompletedAt = r.completed_at,
				TenantId = r.tenant_id,
				Version = r.version,
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
		PostgresSagaOptions options,
		TenantScope scope,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(options);
		SagaSqlValidator.ThrowIfInvalidQualifiedName(options.QualifiedTableName);

		// Unconditional tenant term: this returns saga CONTENT (type, completion state, owning tenant) keyed by
		// saga_id, so an omitted term on the unscoped path resolved a single id to whichever tenant happened to
		// hold it. An unscoped caller now reads the untenanted partition via the reserved sentinel.
		var partition = KeyedTenantPartition.FromScope(scope);
		const string tenantPredicate = " AND tenant_id = @TenantId";
		var sql = $"""
			SELECT saga_id, saga_type, is_completed, completed_at, tenant_id, version
			FROM {options.QualifiedTableName}
			WHERE saga_id = @SagaId{tenantPredicate};
			""";

		Parameters.Add("SagaId", sagaId);
		Parameters.Add("TenantId", partition.TenantId);

		Command = CreateCommand(sql, commandTimeout: options.CommandTimeoutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn =>
		{
			var row = await conn.QuerySingleOrDefaultAsync<SagaSummaryRow>(Command).ConfigureAwait(false);
			return row is null
				? null
				: new SagaInstanceSummary
				{
					SagaId = row.saga_id,
					SagaType = row.saga_type,
					IsCompleted = row.is_completed,
					CompletedAt = row.completed_at,
					TenantId = row.tenant_id,
					Version = row.version,
				};
		};
	}
}

/// <summary>Computes aggregate saga counts (running / completed / total).</summary>
internal sealed class GetSagaStatisticsRequest : DataRequestBase<IDbConnection, SagaStoreStatistics>
{
	public GetSagaStatisticsRequest(
		PostgresSagaOptions options,
		TenantScope scope,
		CancellationToken cancellationToken,
		bool allTenants = false)
	{
		ArgumentNullException.ThrowIfNull(options);
		SagaSqlValidator.ThrowIfInvalidQualifiedName(options.QualifiedTableName);

		// Two intents, neither reachable from the other by omission:
		//
		//   allTenants    no discriminator          the operator diagnostic -- named at the call site
		//   otherwise     tenant_id = @TenantId    exactly the ambient partition
		//
		// The scoped predicate is UNCONDITIONAL. It used to be emitted only when scope.IsScoped, on the belief
		// that an unscoped store would fall through to estate-wide counts -- but a scope resolved from an
		// ITenantContext is always scoped, so that branch could never be taken and the estate-wide read had no
		// reachable caller at all. The untenanted partition is a real partition addressed by the same equality
		// predicate as a real tenant (the column is NOT NULL and an untenanted row carries the reserved
		// sentinel), so routing through KeyedTenantPartition makes one predicate serve both.
		var partition = KeyedTenantPartition.FromScope(scope);
		var tenantPredicate = allTenants ? string.Empty : " WHERE tenant_id = @TenantId";

		var sql = $"""
			SELECT COUNT(*) AS total,
			       COALESCE(SUM(CASE WHEN is_completed THEN 1 ELSE 0 END), 0) AS completed
			FROM {options.QualifiedTableName}{tenantPredicate};
			""";

		if (!allTenants)
		{
			Parameters.Add("TenantId", partition.TenantId);
		}

		Command = CreateCommand(sql, commandTimeout: options.CommandTimeoutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn =>
		{
			var row = await conn.QuerySingleAsync<SagaStatsRow>(Command).ConfigureAwait(false);

			// COUNT(*) is a 64-bit value; the stats DTO exposes 32-bit counts. Saturate rather than
			// silently wrap on the (unrealistic but possible) >int.MaxValue row count.
			static int Saturate(long value) => value > int.MaxValue ? int.MaxValue : (int)value;
			var total = Saturate(row.total);
			var completed = Saturate(row.completed);
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

// Internal records for mapping Postgres snake_case columns.
// ReSharper disable InconsistentNaming - Column names use snake_case
internal sealed record SagaSummaryRow(Guid saga_id, string saga_type, bool is_completed, DateTimeOffset? completed_at, string? tenant_id, long version);

internal sealed record SagaStatsRow(long total, long completed);
// ReSharper restore InconsistentNaming
