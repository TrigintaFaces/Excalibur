// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.CosmosDb;

/// <summary>
/// Health check for Azure Cosmos DB connectivity.
/// </summary>
public sealed class CosmosDbHealthCheck : IHealthCheck
{
	private readonly CosmosDbPersistenceProvider _provider;
	private readonly CosmosDbOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbHealthCheck"/> class.
	/// </summary>
	/// <param name="provider">The Cosmos DB provider.</param>
	/// <param name="options">The Cosmos DB options.</param>
	public CosmosDbHealthCheck(
		CosmosDbPersistenceProvider provider,
		IOptions<CosmosDbOptions> options)
	{
		_provider = provider ?? throw new ArgumentNullException(nameof(provider));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		try
		{
			var connected = await _provider.TestConnectionAsync(cancellationToken).ConfigureAwait(false);

			if (connected)
			{
				var data = new Dictionary<string, object>(StringComparer.Ordinal)
				{
					["database"] = _options.DatabaseName ?? "Unknown",
					["provider"] = _provider.Name,
					["isAvailable"] = _provider.IsAvailable,
					["supportsChangeFeed"] = _provider.SupportsChangeFeed,
					["supportsMultiRegionWrites"] = _provider.SupportsMultiRegionWrites
				};

				return HealthCheckResult.Healthy(
					$"Cosmos DB connection to database '{_options.DatabaseName}' is healthy.",
					data);
			}

			return HealthCheckResult.Unhealthy(
				$"Unable to connect to Cosmos DB database '{_options.DatabaseName}'.");
		}
		catch (Exception ex)
		{
			return HealthCheckResult.Unhealthy(
				$"Cosmos DB health check failed: {ex.Message}",
				ex);
		}
	}
}
