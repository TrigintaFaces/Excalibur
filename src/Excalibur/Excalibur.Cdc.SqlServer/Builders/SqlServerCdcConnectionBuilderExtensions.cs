// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// Extension methods for <see cref="ISqlServerCdcConnectionBuilder"/>.
/// </summary>
public static class SqlServerCdcConnectionBuilderExtensions
{
	/// <summary>
	/// Sets a factory function that creates SQL connections for the CDC state store.
	/// </summary>
	/// <param name="builder">The connection builder.</param>
	/// <param name="stateConnectionFactory">A factory function that creates state store SQL connections.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="stateConnectionFactory"/> is null.
	/// </exception>
	public static ISqlServerCdcBuilder StateConnectionFactory(
		this ISqlServerCdcConnectionBuilder builder,
		Func<IServiceProvider, Func<SqlConnection>> stateConnectionFactory)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(stateConnectionFactory);

		var concreteBuilder = (SqlServerCdcBuilder)builder;
		return concreteBuilder.StateConnectionFactory(stateConnectionFactory);
	}
}
