// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.CosmosDb;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding Cosmos DB health checks.
/// </summary>
public static class CosmosDbHealthChecksBuilderExtensions
{
	/// <summary>
	/// Adds a health check for Azure Cosmos DB.
	/// </summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="name">The health check name.</param>
	/// <param name="failureStatus">The failure status to report.</param>
	/// <param name="tags">The tags for the health check.</param>
	/// <param name="timeout">The timeout for the health check.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddCosmosDb(
		this IHealthChecksBuilder builder,
		string name = "cosmosdb",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null,
		TimeSpan? timeout = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.Add(new HealthCheckRegistration(
			name,
			sp => sp.GetRequiredService<CosmosDbHealthCheck>(),
			failureStatus,
			tags,
			timeout));
	}
}
