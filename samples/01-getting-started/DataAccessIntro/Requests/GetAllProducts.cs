// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Data;

namespace DataAccessIntro.Requests;

/// <summary>
/// A sample data request that retrieves all products.
/// </summary>
[NoTenantTerm(
	TenantConfinement.NoTenantDimension,
	"this introductory sample is single-tenant: the Products table has no tenant column, so there is no tenant term this query could filter on and the read covers every row in the table. Do not copy this declaration into a multi-tenant application. There, the table carries a tenant column, the tenant arrives as an explicit constructor parameter rather than from ambient state, the WHERE clause filters on it, and the request declares Scoped so the predicate and the declaration say the same thing")]
public sealed class GetAllProducts : DataRequest<IEnumerable<Product>>
{

	public GetAllProducts(int timeoutSeconds = 30, CancellationToken cancellationToken = default)
	{
		const string sql = "SELECT Id, Name, Price FROM Products ORDER BY Name";

		Command = CreateCommand(sql, commandTimeout: timeoutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.QueryAsync<Product>(Command).ConfigureAwait(false);
	}
}
