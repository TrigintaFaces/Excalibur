// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb.Outbox;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Cosmos DB outbox change feed services.
/// </summary>
public static class CosmosDbOutboxChangeFeedExtensions
{
	/// <summary>
	/// Adds Cosmos DB outbox change feed processor services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Action to configure the change feed options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddCosmosDbOutboxChangeFeed(
		this IServiceCollection services,
		Action<CosmosDbChangeFeedOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<CosmosDbChangeFeedOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		_ = services.AddSingleton<CosmosDbOutboxChangeFeedProcessor>();

		return services;
	}
}
