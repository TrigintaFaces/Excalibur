// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.EventSourcing.Health;

/// <summary>
/// Health check that verifies the <see cref="IEventStore"/> is reachable.
/// </summary>
/// <remarks>
/// Performs a lightweight probe by attempting to load events for a non-existent
/// aggregate. A successful (empty) response proves connectivity and schema access.
/// </remarks>
public sealed class EventStoreHealthCheck : IHealthCheck
{
	/// <summary>
	/// Sentinel aggregate ID used for probing. Intentionally non-existent.
	/// </summary>
	internal const string ProbeAggregateId = "__health_probe__";

	/// <summary>
	/// Sentinel aggregate type used for probing.
	/// </summary>
	internal const string ProbeAggregateType = "__health__";

	private readonly IEventStore _eventStore;

	/// <summary>
	/// Initializes a new instance of the <see cref="EventStoreHealthCheck"/> class.
	/// </summary>
	/// <param name="eventStore">The event store to probe.</param>
	public EventStoreHealthCheck(IEventStore eventStore)
	{
		_eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
	}

	/// <inheritdoc />
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		try
		{
			var events = await _eventStore.LoadAsync(
				ProbeAggregateId,
				ProbeAggregateType,
				cancellationToken).ConfigureAwait(false);

			return HealthCheckResult.Healthy(
				$"Event store is reachable (probe returned {events.Count} events).");
		}
		catch (Exception ex)
		{
			return HealthCheckResult.Unhealthy(
				"Event store is unreachable.",
				ex);
		}
	}
}
