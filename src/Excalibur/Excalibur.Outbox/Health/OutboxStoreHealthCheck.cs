// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Outbox.Health;

/// <summary>
/// Health check for outbox store connectivity and statistics.
/// </summary>
/// <remarks>
/// <para>
/// Validates that the underlying outbox store is reachable by calling
/// <see cref="IOutboxStoreAdmin.GetStatisticsAsync"/> as a lightweight connectivity probe.
/// Works with all outbox store providers (SqlServer, Postgres, MongoDB, Redis,
/// ElasticSearch, InMemory, CosmosDb, DynamoDb, Firestore) since they all implement
/// <see cref="IOutboxStoreAdmin"/>.
/// </para>
/// <para>
/// This complements <see cref="OutboxHealthCheck"/> which monitors the background
/// processing service state. This health check monitors store connectivity.
/// </para>
/// <list type="bullet">
/// <item><b>Healthy:</b> Store is reachable and responding to queries.</item>
/// <item><b>Degraded:</b> Store is reachable but has elevated failure counts.</item>
/// <item><b>Unhealthy:</b> Store is not reachable or query failed.</item>
/// </list>
/// </remarks>
internal sealed class OutboxStoreHealthCheck : IHealthCheck
{
	private readonly IOutboxStoreAdmin _storeAdmin;

	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxStoreHealthCheck"/> class.
	/// </summary>
	/// <param name="storeAdmin">The outbox store admin interface.</param>
	public OutboxStoreHealthCheck(IOutboxStoreAdmin storeAdmin)
	{
		_storeAdmin = storeAdmin ?? throw new ArgumentNullException(nameof(storeAdmin));
	}

	/// <inheritdoc/>
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var stats = await _storeAdmin.GetStatisticsAsync(cancellationToken).ConfigureAwait(false);

			var data = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["StagedCount"] = stats.StagedMessageCount,
				["SentCount"] = stats.SentMessageCount,
				["FailedCount"] = stats.FailedMessageCount,
				["TotalCount"] = stats.TotalMessageCount,
			};

			if (stats.OldestUnsentMessageAge.HasValue)
			{
				data["OldestUnsentAgeSeconds"] = stats.OldestUnsentMessageAge.Value.TotalSeconds;
			}

			if (stats.FailedMessageCount > 0 && stats.SentMessageCount > 0)
			{
				var failureRate = (double)stats.FailedMessageCount / (stats.SentMessageCount + stats.FailedMessageCount) * 100.0;
				data["FailureRatePercent"] = failureRate;

				if (failureRate > 50.0)
				{
					return HealthCheckResult.Degraded(
						$"Outbox store has elevated failure rate ({failureRate:F1}%).",
						data: data);
				}
			}

			return HealthCheckResult.Healthy(
				$"Outbox store is healthy. Staged: {stats.StagedMessageCount}, Sent: {stats.SentMessageCount}, Failed: {stats.FailedMessageCount}.",
				data: data);
		}
		catch (Exception ex)
		{
			return HealthCheckResult.Unhealthy(
				$"Outbox store connectivity check failed: {ex.Message}",
				ex);
		}
	}
}
