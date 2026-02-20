// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Excalibur.Data.Firestore;

/// <summary>
/// Health check for Google Cloud Firestore connectivity.
/// </summary>
public sealed partial class FirestoreHealthCheck : IHealthCheck
{
	private readonly FirestorePersistenceProvider _provider;
	private readonly ILogger<FirestoreHealthCheck> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreHealthCheck"/> class.
	/// </summary>
	/// <param name="provider">The Firestore persistence provider.</param>
	/// <param name="logger">The logger.</param>
	public FirestoreHealthCheck(
		FirestorePersistenceProvider provider,
		ILogger<FirestoreHealthCheck> logger)
	{
		_provider = provider ?? throw new ArgumentNullException(nameof(provider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		LogHealthCheckStarted();

		try
		{
			var isHealthy = await _provider.TestConnectionAsync(cancellationToken).ConfigureAwait(false);

			if (isHealthy)
			{
				LogHealthCheckCompleted();

				var stats = await _provider.GetDocumentStoreStatisticsAsync(cancellationToken)
					.ConfigureAwait(false);

				return HealthCheckResult.Healthy(
					"Firestore is accessible",
					stats.ToDictionary(kv => kv.Key, kv => kv.Value));
			}

			LogHealthCheckFailed("Connection test returned false", null);
			return HealthCheckResult.Unhealthy("Firestore connection test failed");
		}
		catch (Exception ex)
		{
			LogHealthCheckFailed(ex.Message, ex);
			return HealthCheckResult.Unhealthy(
				$"Firestore health check failed: {ex.Message}",
				ex);
		}
	}
}
