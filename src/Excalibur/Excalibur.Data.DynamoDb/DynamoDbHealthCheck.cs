// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Excalibur.Data.DynamoDb;

/// <summary>
/// Health check for AWS DynamoDB connectivity.
/// </summary>
public sealed partial class DynamoDbHealthCheck : IHealthCheck
{
	private readonly DynamoDbPersistenceProvider _provider;
	private readonly ILogger<DynamoDbHealthCheck> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbHealthCheck"/> class.
	/// </summary>
	/// <param name="provider">The DynamoDB persistence provider.</param>
	/// <param name="logger">The logger.</param>
	public DynamoDbHealthCheck(
		DynamoDbPersistenceProvider provider,
		ILogger<DynamoDbHealthCheck> logger)
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
					"DynamoDB is accessible",
					stats.ToDictionary(kv => kv.Key, kv => kv.Value));
			}

			LogHealthCheckFailed("Connection test returned false", null);
			return HealthCheckResult.Unhealthy("DynamoDB connection test failed");
		}
		catch (Exception ex)
		{
			LogHealthCheckFailed(ex.Message, ex);
			return HealthCheckResult.Unhealthy(
				$"DynamoDB health check failed: {ex.Message}",
				ex);
		}
	}
}
