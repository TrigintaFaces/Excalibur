// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Data.Redis;

/// <summary>
/// Health check for Redis persistence provider connectivity.
/// </summary>
internal sealed class RedisHealthCheck : IHealthCheck
{
	private readonly RedisPersistenceProvider _provider;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisHealthCheck"/> class.
	/// </summary>
	/// <param name="provider">The Redis persistence provider.</param>
	public RedisHealthCheck(RedisPersistenceProvider provider)
	{
		_provider = provider ?? throw new ArgumentNullException(nameof(provider));
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
					"Redis connection is healthy.",
					data);
			}

			return HealthCheckResult.Unhealthy("Unable to connect to Redis.");
		}
		catch (Exception ex)
		{
			return HealthCheckResult.Unhealthy(
				$"Redis health check failed: {ex.Message}",
				ex);
		}
	}
}
