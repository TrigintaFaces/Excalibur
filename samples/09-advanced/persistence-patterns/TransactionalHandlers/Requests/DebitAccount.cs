// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace TransactionalHandlers.Requests;

/// <summary>
/// Debits an account balance. Uses DataRequest which auto-enlists in ambient TransactionScope.
/// </summary>
public sealed class DebitAccount : DataRequest<int>
{
	public DebitAccount(Guid accountId, decimal amount, int timeoutSeconds = 30, CancellationToken cancellationToken = default)
	{
		const string sql = "UPDATE Accounts SET Balance = Balance - @Amount WHERE AccountId = @AccountId AND Balance >= @Amount";

		var parameters = new DynamicParameters();
		parameters.Add("AccountId", accountId, DbType.Guid, ParameterDirection.Input);
		parameters.Add("Amount", amount, DbType.Decimal, ParameterDirection.Input);

		Command = CreateCommand(sql, parameters, commandTimeout: timeoutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
