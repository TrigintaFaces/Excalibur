// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Models;

namespace Excalibur.Saga.Inspection;

/// <summary>
/// Provides read-only inspection of saga state for diagnostics and monitoring.
/// </summary>
/// <remarks>
/// <para>
/// Use this interface to query the current state, step history, and active step
/// of saga instances without modifying them. This is useful for:
/// </para>
/// <list type="bullet">
/// <item><description>Health check dashboards</description></item>
/// <item><description>Operational monitoring</description></item>
/// <item><description>Debugging stuck or failed sagas</description></item>
/// </list>
/// <para>
/// Follows the <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck"/>
/// pattern of providing a minimal read-only interface for infrastructure inspection.
/// </para>
/// </remarks>
public interface ISagaStateInspector
{
	/// <summary>
	/// Gets the current state of a saga instance.
	/// </summary>
	/// <param name="sagaId">The saga instance identifier.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>The saga state if found; otherwise, <see langword="null"/>.</returns>
	Task<SagaState?> GetStateAsync(string sagaId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the step execution history for a saga instance.
	/// </summary>
	/// <param name="sagaId">The saga instance identifier.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// A read-only list of step execution records ordered chronologically,
	/// or an empty list if the saga is not found.
	/// </returns>
	Task<IReadOnlyList<StepExecutionRecord>> GetHistoryAsync(string sagaId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the name of the currently active step for a saga instance.
	/// </summary>
	/// <param name="sagaId">The saga instance identifier.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// The name of the active step if the saga is running; otherwise, <see langword="null"/>.
	/// </returns>
	Task<string?> GetActiveStepAsync(string sagaId, CancellationToken cancellationToken);
}
