// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.CosmosDb.Outbox;
using Excalibur.Dispatch.Abstractions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Cosmos DB outbox services.
/// </summary>
public static class CosmosDbOutboxExtensions
{
	/// <summary>
	/// Adds Cosmos DB outbox store services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the outbox options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddCosmosDbOutbox(options =>
	/// {
	///     options.ConnectionString = "AccountEndpoint=https://...;AccountKey=...";
	///     options.DatabaseName = "mydb";
	///     options.ContainerName = "outbox";
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddCosmosDbOutbox(
		this IServiceCollection services,
		Action<CosmosDbOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<CosmosDbOutboxOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		_ = services.AddSingleton<IOutboxStore, CosmosDbOutboxStore>();

		return services;
	}

	/// <summary>
	/// Adds Cosmos DB outbox store services with a named options configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="name">The name of the options configuration.</param>
	/// <param name="configure">Action to configure the outbox options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCosmosDbOutbox(
		this IServiceCollection services,
		string name,
		Action<CosmosDbOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<CosmosDbOutboxOptions>(name)
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		_ = services.AddSingleton<IOutboxStore, CosmosDbOutboxStore>();

		return services;
	}
}
