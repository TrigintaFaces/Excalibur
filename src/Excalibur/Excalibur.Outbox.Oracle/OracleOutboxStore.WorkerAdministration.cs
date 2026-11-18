// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using Excalibur.Data;

using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace Excalibur.Outbox.Oracle;

public sealed partial class OracleOutboxStore
{
	private ValueTask<long?> GetOwnedHighWaterAsync(CancellationToken cancellationToken) =>
		ExecuteOwnedAsync<long?>(async (connection, transaction, scope, token) =>
		{
			await using var command = connection.CreateCommand();
			command.BindByName = true;
			command.Transaction = (OracleTransaction)transaction;
			command.CommandTimeout = DbTimeouts.RegularTimeoutSeconds;
#pragma warning disable CA2100 // Only NAME_RESOLVE-validated, escaped quoted physical identifiers are interpolated; values remain bound.
			command.CommandText = $"SELECT high_water_token FROM {scope.FenceTable} WHERE {OraclePhysicalOutboxScope.Exact("scope_key", ":ScopeKey")}";
#pragma warning restore CA2100
			command.Parameters.Add("ScopeKey", OracleDbType.Varchar2, scope.Key, ParameterDirection.Input);
			await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
			if (!await reader.ReadAsync(token).ConfigureAwait(false)) { return null; }
			var value = checked((long)reader.GetDecimal(0));
			if (await reader.ReadAsync(token).ConfigureAwait(false)) { throw new InvalidDataException("Oracle fence scope is not unique."); }
			return value;
		}, cancellationToken);

	private async Task ResetOwnedHighWaterAsync(long newHighWater, bool force, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(newHighWater);
		var applied = await ExecuteOwnedAsync(async (connection, transaction, scope, token) =>
		{
			await using var command = connection.CreateCommand();
			command.BindByName = true;
			command.Transaction = (OracleTransaction)transaction;
			command.CommandTimeout = DbTimeouts.RegularTimeoutSeconds;
#pragma warning disable CA2100 // Only NAME_RESOLVE-validated, escaped quoted physical identifiers are interpolated; values remain bound.
			command.CommandText = $"""
				BEGIN
				    BEGIN
				        MERGE INTO {scope.FenceTable} f
				        USING (SELECT :ScopeKey scope_key, :NewHighWater new_hw FROM dual) src
				        ON ({OraclePhysicalOutboxScope.Exact("f.scope_key", "src.scope_key")})
				        WHEN MATCHED THEN UPDATE SET f.high_water_token=src.new_hw
				          WHERE :Force=1 OR f.high_water_token<=src.new_hw
				        WHEN NOT MATCHED THEN INSERT(scope_key,high_water_token) VALUES(src.scope_key,src.new_hw);
				        :Updated := SQL%ROWCOUNT;
				    EXCEPTION WHEN DUP_VAL_ON_INDEX THEN
				        UPDATE {scope.FenceTable} SET high_water_token=:NewHighWater
				        WHERE {OraclePhysicalOutboxScope.Exact("scope_key", ":ScopeKey")}
				          AND (:Force=1 OR high_water_token<=:NewHighWater);
				        :Updated := SQL%ROWCOUNT;
				    END;
				END;
				""";
#pragma warning restore CA2100
			command.Parameters.Add("ScopeKey", OracleDbType.Varchar2, scope.Key, ParameterDirection.Input);
			command.Parameters.Add("NewHighWater", OracleDbType.Int64, newHighWater, ParameterDirection.Input);
			command.Parameters.Add("Force", OracleDbType.Int32, force ? 1 : 0, ParameterDirection.Input);
			command.Parameters.Add("Updated", OracleDbType.Decimal).Direction = ParameterDirection.Output;
			await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
			if (command.Parameters["Updated"].Value is not OracleDecimal { IsNull: false } count
				|| count.Value is not (0 or 1))
			{
				throw new InvalidDataException("Oracle returned an unsupported fencing administration result.");
			}
			return count.Value == 1;
		}, cancellationToken).ConfigureAwait(false);
		if (!applied)
		{
			throw new InvalidOperationException("Refusing to lower the Oracle fencing high-water mark without force: true.");
		}
	}
}
