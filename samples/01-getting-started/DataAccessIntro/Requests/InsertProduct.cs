// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace DataAccessIntro.Requests;

/// <summary>
/// A sample data request that inserts a new product and returns the number of affected rows.
/// </summary>
public sealed class InsertProduct : DataRequest<int>
{
	public InsertProduct(string name, decimal price, int timeoutSeconds = 30, CancellationToken cancellationToken = default)
	{
		const string sql = "INSERT INTO Products (Name, Price) VALUES (@Name, @Price)";

		var parameters = new DynamicParameters();
		parameters.Add("Name", name, DbType.String, ParameterDirection.Input);
		parameters.Add("Price", price, DbType.Decimal, ParameterDirection.Input);

		Command = CreateCommand(sql, parameters, commandTimeout: timeoutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
