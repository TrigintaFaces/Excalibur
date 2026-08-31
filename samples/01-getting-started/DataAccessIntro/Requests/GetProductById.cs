// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data;

namespace DataAccessIntro.Requests;

/// <summary>
/// A sample data request that retrieves a product by its identifier.
/// </summary>
/// <remarks>
/// Demonstrates the <see cref="DataRequest{TModel}"/> pattern:
/// <list type="number">
///   <item>Subclass <see cref="DataRequest{TModel}"/> with your return type</item>
///   <item>Build the SQL + parameters in the constructor</item>
///   <item>Set <see cref="DataRequestBase{TConnection,TModel}.ResolveAsync"/> to execute via Dapper</item>
/// </list>
/// </remarks>
[NoTenantTerm(
	TenantConfinement.NoTenantDimension,
	"this introductory sample is single-tenant: the Products table has no tenant column, so the identifier alone addresses the row. Note the shape, because it is the one that turns into a cross-tenant read the moment the table gains a tenant column: a caller-supplied identifier and no tenant term means any caller who can guess an identifier reads the row behind it. In a multi-tenant application this request takes the tenant as an explicit parameter, adds it to the WHERE clause, and declares Scoped")]
public sealed class GetProductById : DataRequest<Product?>
{

	public GetProductById(int productId, int timeoutSeconds = 30, CancellationToken cancellationToken = default)
	{
		const string sql = "SELECT Id, Name, Price FROM Products WHERE Id = @Id";

		var parameters = new DynamicParameters();
		parameters.Add("Id", productId, DbType.Int32, ParameterDirection.Input);

		Command = CreateCommand(sql, parameters, commandTimeout: timeoutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.QuerySingleOrDefaultAsync<Product?>(Command).ConfigureAwait(false);
	}
}
