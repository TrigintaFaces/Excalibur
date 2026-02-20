// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Diagnostics;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Excalibur.Data.Persistence;

/// <summary>
/// Health check for persistence providers.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="PersistenceHealthCheck" /> class. </remarks>
internal sealed partial class PersistenceHealthCheck(
	IPersistenceProviderFactory providerFactory,
	ILogger<PersistenceHealthCheck> logger,
	string providerName) : IHealthCheck
{
	private readonly IPersistenceProviderFactory _providerFactory =
		providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));

	private readonly ILogger<PersistenceHealthCheck> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly string _providerName = providerName ?? throw new ArgumentNullException(nameof(providerName));

	/// <inheritdoc />
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		try
		{
			// Get the provider
			var provider = _providerFactory.GetProvider(_providerName);
			if (provider == null)
			{
				return HealthCheckResult.Unhealthy($"Provider '{_providerName}' not found");
			}

			var health = (IPersistenceProviderHealth?)provider.GetService(typeof(IPersistenceProviderHealth));
			if (health == null)
			{
				return HealthCheckResult.Degraded($"Provider '{_providerName}' does not support health checks");
			}

			// Test the connection
			var isHealthy = await health.TestConnectionAsync(cancellationToken).ConfigureAwait(false);

			if (isHealthy)
			{
				var metrics = await health.GetMetricsAsync(cancellationToken).ConfigureAwait(false);
				var readOnlyMetadata = new Dictionary<string, object>(metrics, StringComparer.Ordinal);
				return HealthCheckResult.Healthy(
					$"Provider '{_providerName}' is healthy",
					readOnlyMetadata);
			}

			return HealthCheckResult.Unhealthy($"Provider '{_providerName}' connection test failed");
		}
		catch (OperationCanceledException)
		{
			return HealthCheckResult.Unhealthy($"Provider '{_providerName}' health check timed out");
		}
		catch (Exception ex)
		{
			LogHealthCheckFailed(_logger, ex, _providerName);

			return HealthCheckResult.Unhealthy(
				$"Provider '{_providerName}' health check failed",
				ex,
				new Dictionary<string, object>(StringComparer.Ordinal) { ["error"] = ex.Message, ["type"] = ex.GetType().Name });
		}
	}

	[LoggerMessage(DataEventId.HealthCheckFailed, LogLevel.Error, "Health check failed for provider '{ProviderName}'")]
	private static partial void LogHealthCheckFailed(ILogger logger, Exception exception, string providerName);
}
