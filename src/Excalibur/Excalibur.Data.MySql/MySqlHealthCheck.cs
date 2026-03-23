// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.MySql;

/// <summary>
/// Health check for MySQL persistence provider connectivity.
/// </summary>
internal sealed class MySqlHealthCheck : IHealthCheck
{
	private readonly MySqlPersistenceProvider _provider;
	private readonly MySqlProviderOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="MySqlHealthCheck"/> class.
	/// </summary>
	/// <param name="provider">The MySQL persistence provider.</param>
	/// <param name="options">The MySQL provider options.</param>
	public MySqlHealthCheck(
		MySqlPersistenceProvider provider,
		IOptions<MySqlProviderOptions> options)
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
					"MySQL connection is healthy.",
					data);
			}

			return HealthCheckResult.Unhealthy("Unable to connect to MySQL.");
		}
		catch (Exception ex)
		{
			return HealthCheckResult.Unhealthy(
				$"MySQL health check failed: {ex.Message}",
				ex);
		}
	}
}
