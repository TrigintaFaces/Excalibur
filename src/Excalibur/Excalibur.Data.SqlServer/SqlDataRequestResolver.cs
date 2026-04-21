// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Abstractions;

using Microsoft.Data.SqlClient;

namespace Excalibur.Data.SqlServer;

/// <summary>
/// SQL Server implementation of <see cref="IDataRequestResolver{TConnection}"/>.
/// Creates, opens, and disposes a <see cref="SqlConnection"/> per call.
/// </summary>
public sealed class SqlDataRequestResolver : IDataRequestResolver<SqlConnection>
{
	private readonly Func<SqlConnection> _connectionFactory;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlDataRequestResolver"/> class
	/// using a connection string.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <exception cref="ArgumentException">Thrown when <paramref name="connectionString"/> is null or whitespace.</exception>
	public SqlDataRequestResolver(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		_connectionFactory = () => new SqlConnection(connectionString);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlDataRequestResolver"/> class
	/// using a connection factory.
	/// </summary>
	/// <param name="connectionFactory">A factory that produces <see cref="SqlConnection"/> instances.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="connectionFactory"/> is null.</exception>
	public SqlDataRequestResolver(Func<SqlConnection> connectionFactory)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);
		_connectionFactory = connectionFactory;
	}

	/// <inheritdoc />
	public async Task<TModel> QueryAsync<TModel>(
		IDataRequest<SqlConnection, TModel> request,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		return await request.ResolveAsync(connection).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task ExecuteAsync(
		IDataRequest<SqlConnection, int> request,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await request.ResolveAsync(connection).ConfigureAwait(false);
	}
}
