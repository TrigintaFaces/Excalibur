// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Abstractions;
using Excalibur.Data.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering <see cref="SqlDataRequestResolver"/> with dependency injection.
/// </summary>
public static class SqlDataRequestResolverServiceCollectionExtensions
{
	/// <summary>
	/// Registers a <see cref="SqlDataRequestResolver"/> as the
	/// <see cref="IDataRequestResolver{TConnection}"/> for <see cref="SqlConnection"/>,
	/// using the specified connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlDataRequestResolver(
		this IServiceCollection services,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		services.TryAddSingleton<IDataRequestResolver<SqlConnection>>(
			new SqlDataRequestResolver(connectionString));

		return services;
	}

	/// <summary>
	/// Registers a <see cref="SqlDataRequestResolver"/> as the
	/// <see cref="IDataRequestResolver{TConnection}"/> for <see cref="SqlConnection"/>,
	/// using the specified connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactory">A factory that produces <see cref="SqlConnection"/> instances.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlDataRequestResolver(
		this IServiceCollection services,
		Func<SqlConnection> connectionFactory)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		services.TryAddSingleton<IDataRequestResolver<SqlConnection>>(
			new SqlDataRequestResolver(connectionFactory));

		return services;
	}

	/// <summary>
	/// Registers a keyed <see cref="SqlDataRequestResolver"/> as the
	/// <see cref="IDataRequestResolver{TConnection}"/> for <see cref="SqlConnection"/>,
	/// using the specified connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="serviceKey">The key to associate with this resolver instance.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlDataRequestResolver(
		this IServiceCollection services,
		string serviceKey,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(serviceKey);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		services.TryAddKeyedSingleton<IDataRequestResolver<SqlConnection>>(
			serviceKey,
			new SqlDataRequestResolver(connectionString));

		return services;
	}

	/// <summary>
	/// Registers a keyed <see cref="SqlDataRequestResolver"/> as the
	/// <see cref="IDataRequestResolver{TConnection}"/> for <see cref="SqlConnection"/>,
	/// using the specified connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="serviceKey">The key to associate with this resolver instance.</param>
	/// <param name="connectionFactory">A factory that produces <see cref="SqlConnection"/> instances.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlDataRequestResolver(
		this IServiceCollection services,
		string serviceKey,
		Func<SqlConnection> connectionFactory)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(serviceKey);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		services.TryAddKeyedSingleton<IDataRequestResolver<SqlConnection>>(
			serviceKey,
			new SqlDataRequestResolver(connectionFactory));

		return services;
	}
}
