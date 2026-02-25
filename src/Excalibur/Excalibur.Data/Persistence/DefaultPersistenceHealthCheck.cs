// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Diagnostics;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Excalibur.Data.Persistence;

/// <summary>
/// Default implementation of persistence health checks.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="DefaultPersistenceHealthCheck" /> class. </remarks>
internal sealed partial class DefaultPersistenceHealthCheck(
	IPersistenceProviderFactory providerFactory,
	ILogger<DefaultPersistenceHealthCheck> logger) : IPersistenceHealthCheck
{
	private readonly IPersistenceProviderFactory _providerFactory =
		providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));

	private readonly ILogger<DefaultPersistenceHealthCheck> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private TimeSpan _timeout = TimeSpan.FromSeconds(30);

	/// <inheritdoc />
	public string HealthCheckName => "PersistenceHealthCheck";

	/// <inheritdoc />
	public IEnumerable<string> Tags => new[] { "persistence", "database", "ready" };

	/// <inheritdoc />
	public TimeSpan Timeout
	{
		get => _timeout;
		set => _timeout = value > TimeSpan.Zero ? value : throw new ArgumentException("Timeout must be greater than zero.", nameof(value));
	}

	/// <inheritdoc />
	public async Task<PersistenceHealthStatus> CheckHealthAsync(string providerName, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

		try
		{
			var provider = _providerFactory.GetProvider(providerName);
			if (provider == null)
			{
				return new PersistenceHealthStatus
				{
					ProviderName = providerName,
					IsHealthy = false,
					Message = $"Provider '{providerName}' not found",
				};
			}

			var health = (IPersistenceProviderHealth?)provider.GetService(typeof(IPersistenceProviderHealth));
			if (health == null)
			{
				return new PersistenceHealthStatus
				{
					ProviderName = providerName,
					IsHealthy = false,
					Message = $"Provider '{providerName}' does not support health checks",
				};
			}

			// Perform a simple connectivity check
			var isHealthy = await health.TestConnectionAsync(cancellationToken).ConfigureAwait(false);

			return new PersistenceHealthStatus
			{
				ProviderName = providerName,
				IsHealthy = isHealthy,
				Message = isHealthy ? "Healthy" : "Connection test failed",
			};
		}
		catch (Exception ex)
		{
			LogHealthCheckFailed(_logger, ex, providerName);
			return new PersistenceHealthStatus { ProviderName = providerName, IsHealthy = false, Message = ex.Message };
		}
	}

	/// <inheritdoc />
	public async Task<DetailedHealthCheckResult> CheckDetailedHealthAsync(
		IPersistenceProvider provider,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(provider);

		var startTime = DateTimeOffset.UtcNow;
		HealthStatus status;
		string description;
		Exception? exception = null;
		var data = new Dictionary<string, object>(StringComparer.Ordinal);
		var metrics = new Dictionary<string, double>(StringComparer.Ordinal);

		try
		{
			var health = (IPersistenceProviderHealth?)provider.GetService(typeof(IPersistenceProviderHealth));

			data["ProviderName"] = provider.Name;
			data["CheckedAt"] = DateTimeOffset.UtcNow;

			if (health == null)
			{
				status = HealthStatus.Degraded;
				description = "Provider does not support health checks";
				data["IsConnected"] = false;
			}
			else
			{
				// Test basic connectivity
				var isConnected = await health.TestConnectionAsync(cancellationToken).ConfigureAwait(false);
				data["IsConnected"] = isConnected;

				if (isConnected)
				{
					status = HealthStatus.Healthy;
					description = "Provider is healthy and responsive";
				}
				else
				{
					status = HealthStatus.Unhealthy;
					description = "Provider connection test failed";
				}
			}
		}
		catch (Exception ex)
		{
			LogDetailedHealthCheckFailed(_logger, ex, provider.Name);
			status = HealthStatus.Unhealthy;
			description = $"Health check failed: {ex.Message}";
			exception = ex;
			data["ProviderName"] = provider.Name;
			data["CheckedAt"] = DateTimeOffset.UtcNow;
		}

		var responseTime = DateTimeOffset.UtcNow - startTime;
		metrics["ResponseTimeMs"] = responseTime.TotalMilliseconds;

		return new DetailedHealthCheckResult(
			status,
			description,
			exception,
			data,
			responseTime,
			metrics);
	}

	/// <inheritdoc />
	public async Task<IEnumerable<PersistenceHealthStatus>> CheckAllProvidersAsync(CancellationToken cancellationToken)
	{
		var results = new List<PersistenceHealthStatus>();
		var providerNames = _providerFactory.GetProviderNames();

		foreach (var name in providerNames)
		{
			var status = await CheckHealthAsync(name, cancellationToken).ConfigureAwait(false);
			results.Add(status);
		}

		return results;
	}

	/// <inheritdoc />
	public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
	{
		var statuses = await CheckAllProvidersAsync(cancellationToken).ConfigureAwait(false);
		var unhealthyProviders = statuses.Where(static s => !s.IsHealthy).ToList();

		if (unhealthyProviders.Count == 0)
		{
			return HealthCheckResult.Healthy("All persistence providers are healthy");
		}

		var data = new Dictionary<string, object>(StringComparer.Ordinal);
		foreach (var status in statuses)
		{
			data[$"provider_{status.ProviderName}"] = status.IsHealthy ? "Healthy" : status.Message;
		}

		if (unhealthyProviders.Count == statuses.Take(unhealthyProviders.Count + 1).Count())
		{
			return HealthCheckResult.Unhealthy("All persistence providers are unhealthy", data: data);
		}

		return HealthCheckResult.Degraded(
			$"{unhealthyProviders.Count} provider(s) unhealthy: {string.Join(", ", unhealthyProviders.Select(static p => p.ProviderName))}",
			data: data);
	}

	[LoggerMessage(DataEventId.HealthCheckFailed, LogLevel.Error, "Health check failed for provider {ProviderName}")]
	private static partial void LogHealthCheckFailed(ILogger logger, Exception exception, string providerName);

	[LoggerMessage(DataEventId.DetailedHealthCheckFailed, LogLevel.Error, "Detailed health check failed for provider {ProviderName}")]
	private static partial void LogDetailedHealthCheckFailed(ILogger logger, Exception exception, string providerName);
}
