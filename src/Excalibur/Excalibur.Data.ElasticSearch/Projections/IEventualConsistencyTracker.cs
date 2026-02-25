// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Tracks and monitors eventual consistency between write and read models.
/// </summary>
public interface IEventualConsistencyTracker
{
	/// <summary>
	/// Records when an event is written to the write model.
	/// </summary>
	/// <param name="eventId"> The unique identifier of the event. </param>
	/// <param name="aggregateId"> The aggregate identifier associated with the event. </param>
	/// <param name="eventType"> The type of event. </param>
	/// <param name="timestamp"> The timestamp when the event was written. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous tracking operation. </returns>
	Task TrackWriteModelEventAsync(
		string eventId,
		string aggregateId,
		string eventType,
		DateTime timestamp,
		CancellationToken cancellationToken);

	/// <summary>
	/// Records when a projection is updated in the read model.
	/// </summary>
	/// <param name="eventId"> The event identifier that triggered the projection update. </param>
	/// <param name="projectionType"> The type of projection updated. </param>
	/// <param name="timestamp"> The timestamp when the projection was updated. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous tracking operation. </returns>
	Task TrackReadModelProjectionAsync(
		string eventId,
		string projectionType,
		DateTime timestamp,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current consistency lag for a specific projection type.
	/// </summary>
	/// <param name="projectionType"> The type of projection to check. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The consistency lag information. </returns>
	Task<ConsistencyLag> GetConsistencyLagAsync(
		string projectionType,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets consistency metrics for all projection types.
	/// </summary>
	/// <param name="fromTime"> The start time for metrics calculation. </param>
	/// <param name="toTime"> The end time for metrics calculation. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A collection of consistency metrics. </returns>
	Task<IEnumerable<ConsistencyMetrics>> GetConsistencyMetricsAsync(
		DateTime fromTime,
		DateTime toTime,
		CancellationToken cancellationToken);

	/// <summary>
	/// Checks if a specific event has been processed by all projections.
	/// </summary>
	/// <param name="eventId"> The event identifier to check. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the event has been fully processed; otherwise, false. </returns>
	Task<bool> IsEventFullyProcessedAsync(
		string eventId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets events that have not been processed within the expected time window.
	/// </summary>
	/// <param name="expectedProcessingTime"> The expected maximum processing time. </param>
	/// <param name="maxResults"> Maximum number of results to return. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A collection of lagging events. </returns>
	Task<IEnumerable<LaggingEvent>> GetLaggingEventsAsync(
		TimeSpan expectedProcessingTime,
		int maxResults,
		CancellationToken cancellationToken);

	/// <summary>
	/// Sets up monitoring alerts for consistency violations.
	/// </summary>
	/// <param name="config"> The alert configuration. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous setup operation. </returns>
	Task ConfigureConsistencyAlertsAsync(
		ConsistencyAlertConfiguration config,
		CancellationToken cancellationToken);
}
