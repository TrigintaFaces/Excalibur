// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data;

namespace DataAccessIntro.Requests;

/// <summary>
/// A sample data request that inserts a new product and returns the number of affected rows.
/// </summary>
[NoTenantTerm(
	TenantConfinement.NoTenantDimension,
	"this introductory sample is single-tenant: the Products table has no tenant column, so the insert has no tenant value to stamp. The write path is where multi-tenant isolation begins — a row inserted without a tenant term cannot be filtered by tenant afterwards, whatever later queries do. In a multi-tenant application this request takes the tenant as an explicit parameter, stamps it into the inserted row, and declares Scoped")]
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
