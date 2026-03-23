// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.MongoDB;

/// <summary>
/// Health check for MongoDB persistence provider connectivity.
/// </summary>
internal sealed class MongoDbHealthCheck : IHealthCheck
{
	private readonly MongoDbPersistenceProvider _provider;
	private readonly MongoDbProviderOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbHealthCheck"/> class.
	/// </summary>
	/// <param name="provider">The MongoDB persistence provider.</param>
	/// <param name="options">The MongoDB provider options.</param>
	public MongoDbHealthCheck(
		MongoDbPersistenceProvider provider,
		IOptions<MongoDbProviderOptions> options)
	{
		_provider = provider ?? throw new ArgumentNullException(nameof(provider));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var connected = await _provider.TestConnectionAsync(cancellationToken).ConfigureAwait(false);

			if (connected)
			{
				var data = new Dictionary<string, object>(StringComparer.Ordinal)
				{
					["provider"] = _provider.Name,
					["providerType"] = _provider.ProviderType,
				};

				return HealthCheckResult.Healthy(
					"MongoDB connection is healthy.",
					data);
			}

			return HealthCheckResult.Unhealthy("Unable to connect to MongoDB.");
		}
		catch (Exception ex)
		{
			return HealthCheckResult.Unhealthy(
				$"MongoDB health check failed: {ex.Message}",
				ex);
		}
	}
}
