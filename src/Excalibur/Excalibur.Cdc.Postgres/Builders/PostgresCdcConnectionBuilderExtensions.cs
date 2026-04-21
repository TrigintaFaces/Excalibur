// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Npgsql;

namespace Excalibur.Cdc.Postgres;

/// <summary>
/// Extension methods for <see cref="IPostgresCdcConnectionBuilder"/>.
/// </summary>
public static class PostgresCdcConnectionBuilderExtensions
{
	/// <summary>
	/// Sets a factory function that creates Postgres connections for the CDC state store.
	/// </summary>
	/// <param name="builder">The connection builder.</param>
	/// <param name="stateConnectionFactory">A factory function that creates state store Postgres connections.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="stateConnectionFactory"/> is null.
	/// </exception>
	public static IPostgresCdcBuilder StateConnectionFactory(
		this IPostgresCdcConnectionBuilder builder,
		Func<IServiceProvider, Func<NpgsqlConnection>> stateConnectionFactory)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(stateConnectionFactory);

		var concreteBuilder = (PostgresCdcBuilder)builder;
		return concreteBuilder.StateConnectionFactory(stateConnectionFactory);
	}
}
