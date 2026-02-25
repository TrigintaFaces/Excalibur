// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.SqlServer;

/// <summary>
/// Provides extension methods for working with <see cref="IServiceProvider" /> to retrieve database connections.
/// </summary>
public static class ServiceProviderExtensions
{
	/// <summary>
	/// Builds and opens a <see cref="SqlConnection" /> based on the specified connection name in the configuration.
	/// </summary>
	/// <param name="provider"> The <see cref="IServiceProvider" /> used to resolve dependencies. </param>
	/// <param name="connectionName"> The name of the connection string in the configuration. </param>
	/// <returns> An open <see cref="SqlConnection" /> instance. </returns>
	/// <exception cref="ArgumentException"> Thrown if <paramref name="connectionName" /> is null, empty, or whitespace. </exception>
	/// <exception cref="InvalidOperationException"> Thrown if <see cref="IConfiguration" /> is not registered in the service provider. </exception>
	public static SqlConnection BuildConnection(this IServiceProvider provider, string connectionName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

		var config = provider.GetRequiredService<IConfiguration>();
		var connectionString = config.GetConnectionString(connectionName) ??
							   throw new InvalidOperationException($"Connection string '{connectionName}' not found in configuration.");

		var connection = new SqlConnection(connectionString);
		connection.Open();
		return connection;
	}
}
