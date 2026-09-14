// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.Postgres;

/// <summary>
/// Reads the recorded fencing high-water mark for a scope, or <see langword="null"/> when the scope has
/// never been fenced.
/// </summary>
[NoTenantTerm(
	TenantConfinement.EstateWide,
	"the fence is keyed on the outbox table, not on a tenant: a fencing token orders competing processors of one table and has no tenant dimension to scope to")]
internal sealed class GetOutboxFenceHighWaterRequest : DataRequest<long?>
{
	public GetOutboxFenceHighWaterRequest(
		string fenceTableName,
		string scope,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		var sql = $"SELECT high_water_token FROM {fenceTableName} WHERE scope_key = @ScopeKey";
		var parameters = new DynamicParameters();
		parameters.Add("ScopeKey", scope);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds,
			cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.QuerySingleOrDefaultAsync<long?>(Command).ConfigureAwait(false);
	}
}

/// <summary>
/// Administratively sets the fencing high-water mark for a scope in one atomic statement.
/// </summary>
/// <remarks>
/// Without <c>force</c>, <c>ON CONFLICT DO UPDATE</c> carries a <c>WHERE</c> clause requiring the recorded
/// value to be no greater than the new one; when that fails, Postgres treats the insert as a no-op for
/// that row and <c>RETURNING</c> yields no row, which the caller reads as a refusal. A never-fenced scope
/// always inserts cleanly (no conflict to guard). With <c>force</c> the <c>WHERE</c> clause is omitted and
/// the row is always written.
/// </remarks>
[NoTenantTerm(
	TenantConfinement.EstateWide,
	"the fence is keyed on the outbox table, not on a tenant: a fencing token orders competing processors of one table and has no tenant dimension to scope to")]
internal sealed class ResetOutboxFenceHighWaterRequest : DataRequest<bool>
{
	public ResetOutboxFenceHighWaterRequest(
		string fenceTableName,
		string scope,
		long newHighWater,
		bool force,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		var guard = force ? string.Empty : "WHERE f.high_water_token <= @NewHighWater";
		var sql = $"""
		   INSERT INTO {fenceTableName} AS f (scope_key, high_water_token)
		       VALUES (@ScopeKey, @NewHighWater)
		       ON CONFLICT (scope_key) DO UPDATE
		           SET high_water_token = @NewHighWater
		           {guard}
		   RETURNING TRUE;
		   """;

		var parameters = new DynamicParameters();
		parameters.Add("ScopeKey", scope);
		parameters.Add("NewHighWater", newHighWater);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds,
			cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.QuerySingleOrDefaultAsync<bool?>(Command).ConfigureAwait(false) == true;
	}
}
