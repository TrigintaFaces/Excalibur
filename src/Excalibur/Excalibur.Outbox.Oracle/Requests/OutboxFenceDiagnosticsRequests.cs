// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.Oracle;

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
		var sql = $"SELECT high_water_token FROM {fenceTableName} WHERE scope_key = :ScopeKey";
		var parameters = new DynamicParameters();
		parameters.Add("ScopeKey", scope, direction: ParameterDirection.Input);

		Command = new CommandDefinition(sql, new OracleDynamicParameters(parameters),
			commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.QuerySingleOrDefaultAsync<long?>(Command).ConfigureAwait(false);
	}
}

/// <summary>
/// Administratively sets the fencing high-water mark for a scope in one atomic statement.
/// </summary>
/// <remarks>
/// Without <c>force</c>, the <c>WHEN MATCHED</c> branch carries a <c>WHERE</c> clause requiring the
/// recorded value to be no greater than the new one; a row that already exists with a higher value
/// therefore matches neither <c>WHEN</c> branch and the MERGE affects zero rows, which the caller reads as
/// a refusal. A never-fenced scope always inserts (a row that matches nothing on the join condition always
/// satisfies <c>WHEN NOT MATCHED</c>), so seeding a fresh scope never needs <c>force</c>. With <c>force</c>
/// the <c>WHERE</c> clause is omitted and the row is always written.
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
		var guard = force ? string.Empty : "AND f.high_water_token <= src.new_hw";
		var sql = $"""
		   DECLARE
		       v_updated NUMBER := 0;
		   BEGIN
		       MERGE INTO {fenceTableName} f
		       USING (SELECT :ScopeKey AS scope_key, :NewHighWater AS new_hw FROM dual) src
		       ON (f.scope_key = src.scope_key)
		       WHEN MATCHED THEN UPDATE SET f.high_water_token = src.new_hw WHERE 1 = 1 {guard}
		       WHEN NOT MATCHED THEN INSERT (scope_key, high_water_token) VALUES (src.scope_key, src.new_hw);
		       v_updated := SQL%ROWCOUNT;
		       :OutUpdated := v_updated;
		   END;
		   """;

		var parameters = new DynamicParameters();
		parameters.Add("ScopeKey", scope, direction: ParameterDirection.Input);
		parameters.Add("NewHighWater", newHighWater, direction: ParameterDirection.Input);
		parameters.Add("OutUpdated", dbType: DbType.Int64, direction: ParameterDirection.Output);

		Command = new CommandDefinition(sql, new OracleDynamicParameters(parameters),
			commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn =>
		{
			_ = await conn.ExecuteAsync(Command).ConfigureAwait(false);
			return parameters.Get<long>("OutUpdated") > 0;
		};
	}
}
