// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using SagaOrchestration.Sagas;

namespace SagaOrchestration.Steps;

/// <summary>
/// Interface for a saga step that supports compensation.
/// </summary>
/// <remarks>
/// <para>
/// Each saga step must implement both Execute and Compensate methods.
/// The Execute method performs the forward action, while Compensate
/// reverses it during saga rollback.
/// </para>
/// <para>
/// Steps are executed in order (A → B → C) and compensated in reverse
/// order (C → B → A) when a failure occurs.
/// </para>
/// </remarks>
public interface ISagaStep
{
	/// <summary>
	/// Gets the step name for identification and logging.
	/// </summary>
	string Name { get; }

	/// <summary>
	/// Executes the forward action of this step.
	/// </summary>
	/// <param name="data">The saga data to operate on.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True if the step succeeded; false if it failed (triggers compensation).</returns>
	Task<bool> ExecuteAsync(OrderSagaData data, CancellationToken cancellationToken);

	/// <summary>
	/// Compensates (reverses) this step during saga rollback.
	/// </summary>
	/// <param name="data">The saga data to operate on.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True if compensation succeeded; false if it failed (may retry or go to DLQ).</returns>
	Task<bool> CompensateAsync(OrderSagaData data, CancellationToken cancellationToken);
}
