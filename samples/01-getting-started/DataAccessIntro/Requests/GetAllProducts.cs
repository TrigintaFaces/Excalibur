// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Data.Abstractions;

namespace DataAccessIntro.Requests;

/// <summary>
/// A sample data request that retrieves all products.
/// </summary>
public sealed class GetAllProducts : DataRequest<IEnumerable<Product>>
{
	public GetAllProducts(int timeoutSeconds = 30, CancellationToken cancellationToken = default)
	{
		const string sql = "SELECT Id, Name, Price FROM Products ORDER BY Name";

		Command = CreateCommand(sql, commandTimeout: timeoutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.QueryAsync<Product>(Command).ConfigureAwait(false);
	}
}
