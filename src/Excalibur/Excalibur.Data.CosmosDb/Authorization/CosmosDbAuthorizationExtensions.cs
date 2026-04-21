// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.CosmosDb.Authorization;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Cosmos DB authorization services.
/// </summary>
public static class CosmosDbAuthorizationExtensions
{
	/// <summary>
	/// Adds Cosmos DB-based authorization services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCosmosDbAuthorization(
		this IServiceCollection services,
		Action<CosmosDbAuthorizationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<CosmosDbAuthorizationOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<CosmosDbAuthorizationOptions>, CosmosDbAuthorizationOptionsValidator>());

		services.TryAddSingleton<IGrantStore, CosmosDbGrantStore>();
		services.TryAddSingleton<IActivityGroupGrantStore, CosmosDbActivityGroupGrantStore>();

		return services;
	}

	/// <summary>
	/// Adds Cosmos DB-based authorization services to the service collection with a connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Cosmos DB connection string.</param>
	/// <param name="databaseName">The database name. Defaults to "authorization".</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCosmosDbAuthorization(
		this IServiceCollection services,
		string connectionString,
		string databaseName = "authorization")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return services.AddCosmosDbAuthorization(options =>
		{
			options.Client.ConnectionString = connectionString;
			options.DatabaseName = databaseName;
		});
	}

	/// <summary>
	/// Adds Cosmos DB-based authorization services to the service collection with a connection string and HTTP client factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Cosmos DB connection string.</param>
	/// <param name="httpClientFactory">Factory function to create HttpClient instances. Used for Cosmos DB Emulator SSL bypass.</param>
	/// <param name="databaseName">The database name. Defaults to "authorization".</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// The <paramref name="httpClientFactory"/> is useful for integration testing with Cosmos DB Emulator
	/// where you need to bypass SSL certificate validation.
	/// </para>
	/// <para>
	/// Example usage with SSL bypass for Cosmos DB Emulator:
	/// <code>
	/// services.AddCosmosDbAuthorization(
	///     "AccountEndpoint=https://localhost:8081/;AccountKey=...",
	///     () => new HttpClient(new HttpClientHandler
	///     {
	///         ServerCertificateCustomValidationCallback = (_, _, _, _) => true
	///     }));
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddCosmosDbAuthorization(
		this IServiceCollection services,
		string connectionString,
		Func<HttpClient> httpClientFactory,
		string databaseName = "authorization")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentNullException.ThrowIfNull(httpClientFactory);

		return services.AddCosmosDbAuthorization(options =>
		{
			options.Client.ConnectionString = connectionString;
			options.DatabaseName = databaseName;
			options.Client.HttpClientFactory = httpClientFactory;
		});
	}

	/// <summary>
	/// Adds only the Cosmos DB grant service to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCosmosDbGrantStore(
		this IServiceCollection services,
		Action<CosmosDbAuthorizationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<CosmosDbAuthorizationOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<CosmosDbAuthorizationOptions>, CosmosDbAuthorizationOptionsValidator>());

		services.TryAddSingleton<IGrantStore, CosmosDbGrantStore>();

		return services;
	}

	/// <summary>
	/// Adds only the Cosmos DB grant service to the service collection with a connection string and HTTP client factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Cosmos DB connection string.</param>
	/// <param name="httpClientFactory">Factory function to create HttpClient instances. Used for Cosmos DB Emulator SSL bypass.</param>
	/// <param name="databaseName">The database name. Defaults to "authorization".</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// The <paramref name="httpClientFactory"/> is useful for integration testing with Cosmos DB Emulator
	/// where you need to bypass SSL certificate validation.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddCosmosDbGrantStore(
		this IServiceCollection services,
		string connectionString,
		Func<HttpClient> httpClientFactory,
		string databaseName = "authorization")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentNullException.ThrowIfNull(httpClientFactory);

		return services.AddCosmosDbGrantStore(options =>
		{
			options.Client.ConnectionString = connectionString;
			options.DatabaseName = databaseName;
			options.Client.HttpClientFactory = httpClientFactory;
		});
	}

	/// <summary>
	/// Adds only the Cosmos DB activity group grant service to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCosmosDbActivityGroupGrantStore(
		this IServiceCollection services,
		Action<CosmosDbAuthorizationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<CosmosDbAuthorizationOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<CosmosDbAuthorizationOptions>, CosmosDbAuthorizationOptionsValidator>());

		services.TryAddSingleton<IActivityGroupGrantStore, CosmosDbActivityGroupGrantStore>();

		return services;
	}

	/// <summary>
	/// Adds only the Cosmos DB activity group grant service to the service collection with a connection string and HTTP client factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Cosmos DB connection string.</param>
	/// <param name="httpClientFactory">Factory function to create HttpClient instances. Used for Cosmos DB Emulator SSL bypass.</param>
	/// <param name="databaseName">The database name. Defaults to "authorization".</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// The <paramref name="httpClientFactory"/> is useful for integration testing with Cosmos DB Emulator
	/// where you need to bypass SSL certificate validation.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddCosmosDbActivityGroupGrantStore(
		this IServiceCollection services,
		string connectionString,
		Func<HttpClient> httpClientFactory,
		string databaseName = "authorization")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentNullException.ThrowIfNull(httpClientFactory);

		return services.AddCosmosDbActivityGroupGrantStore(options =>
		{
			options.Client.ConnectionString = connectionString;
			options.DatabaseName = databaseName;
			options.Client.HttpClientFactory = httpClientFactory;
		});
	}
}
