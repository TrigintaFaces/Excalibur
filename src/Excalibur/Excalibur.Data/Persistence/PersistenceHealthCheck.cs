// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Excalibur.Data.Persistence;

/// <summary>
/// Health check that probes a keyed persistence provider's connectivity.
/// </summary>
/// <remarks>
/// The provider is resolved from keyed dependency injection under <paramref name="providerName" />,
/// the same key its package registers it under (for example <c>"sqlserver"</c> or <c>"default"</c>).
/// </remarks>
internal sealed partial class PersistenceHealthCheck(
	IServiceProvider serviceProvider,
	string providerName,
	ILogger<PersistenceHealthCheck> logger) : IHealthCheck
{
	private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
	private readonly string _providerName = providerName ?? throw new ArgumentNullException(nameof(providerName));
	private readonly ILogger<PersistenceHealthCheck> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		try
		{
			var provider = _serviceProvider.GetKeyedService<IPersistenceProvider>(_providerName);
			if (provider == null)
			{
				return HealthCheckResult.Unhealthy(
					$"No persistence provider is registered for key '{_providerName}'. " +
					$"Register the provider before probing it (e.g. call the provider package's Add…Persistence extension).");
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
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
