// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Provides operational visibility into saga instances for monitoring and diagnostics.
/// </summary>
/// <remarks>
/// <para>
/// This interface enables monitoring of saga health, stuck detection, and failure analysis.
/// Implementations should provide efficient queries optimized for operational dashboards.
/// </para>
/// <para>
/// Common implementations:
/// <list type="bullet">
/// <item><description>SqlServerSagaMonitoringService - SQL Server with Dapper</description></item>
/// <item><description>InMemorySagaMonitoringService - For testing scenarios</description></item>
/// </list>
/// </para>
/// </remarks>
public interface ISagaMonitoringService
{
	/// <summary>
	/// Gets the count of currently running (non-completed) saga instances.
	/// </summary>
	/// <param name="sagaType">
	/// Optional saga type filter. Pass <see langword="null"/> to count all saga types.
	/// Uses the fully qualified type name (e.g., "OrderSaga").
	/// </param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>The number of running saga instances.</returns>
	Task<int> GetRunningCountAsync(
		string? sagaType,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the count of completed saga instances since a given date.
	/// </summary>
	/// <param name="sagaType">
	/// Optional saga type filter. Pass <see langword="null"/> to count all saga types.
	/// </param>
	/// <param name="since">
	/// Optional date filter. Only counts sagas completed on or after this date.
	/// Pass <see langword="null"/> to count all completed sagas.
	/// </param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>The number of completed saga instances matching the criteria.</returns>
	Task<int> GetCompletedCountAsync(
		string? sagaType,
		DateTime? since,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets saga instances that are stuck (not updated within the specified threshold).
	/// </summary>
	/// <param name="threshold">
	/// The time threshold for considering a saga stuck. A saga is stuck if its
	/// last update time is older than (now - threshold).
	/// </param>
	/// <param name="limit">Maximum number of stuck sagas to return.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// A read-only list of stuck saga instances, ordered by last updated time ascending
	/// (oldest stuck sagas first).
	/// </returns>
	/// <remarks>
	/// Stuck sagas may indicate processing failures, deadlocks, or resource contention.
	/// Consider alerting when stuck saga count exceeds a threshold.
	/// </remarks>
	Task<IReadOnlyList<SagaInstanceInfo>> GetStuckSagasAsync(
		TimeSpan threshold,
		int limit,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets saga instances that have failed.
	/// </summary>
	/// <param name="limit">Maximum number of failed sagas to return.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// A read-only list of failed saga instances, ordered by last updated time descending
	/// (most recently failed sagas first).
	/// </returns>
	/// <remarks>
	/// Failed sagas have a non-null <see cref="SagaInstanceInfo.FailureReason"/>.
	/// Use this for failure analysis and alerting.
	/// </remarks>
	Task<IReadOnlyList<SagaInstanceInfo>> GetFailedSagasAsync(
		int limit,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the average completion time for a saga type over a time period.
	/// </summary>
	/// <param name="sagaType">The saga type to measure. Required.</param>
	/// <param name="since">
	/// Only consider sagas completed on or after this date.
	/// </param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// The average time between saga creation and completion, or <see langword="null"/>
	/// if no completed sagas exist for the criteria.
	/// </returns>
	/// <remarks>
	/// Use this for performance monitoring and SLA tracking. Large average completion
	/// times may indicate performance issues or external service delays.
	/// </remarks>
	Task<TimeSpan?> GetAverageCompletionTimeAsync(
		string sagaType,
		DateTime since,
		CancellationToken cancellationToken);
}
