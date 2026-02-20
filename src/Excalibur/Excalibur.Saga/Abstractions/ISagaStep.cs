// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Represents a single step in a saga.
/// </summary>
/// <typeparam name="TSagaData"> The type of data flowing through the saga. </typeparam>
public interface ISagaStep<TSagaData>
	where TSagaData : class
{
	/// <summary>
	/// Gets the name of this step.
	/// </summary>
	/// <value> The unique step name. </value>
	string Name { get; }

	/// <summary>
	/// Gets a value indicating whether this step can be compensated.
	/// </summary>
	/// <value> <see langword="true" /> if the step supports compensation; otherwise, <see langword="false" />. </value>
	bool CanCompensate { get; }

	/// <summary>
	/// Gets the timeout for step execution.
	/// </summary>
	/// <value> The timeout duration. </value>
	TimeSpan Timeout { get; }

	/// <summary>
	/// Executes the forward action of this step.
	/// </summary>
	/// <param name="context"> The saga execution context. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The step result. </returns>
	Task<StepResult> ExecuteAsync(ISagaContext<TSagaData> context, CancellationToken cancellationToken);

	/// <summary>
	/// Executes the compensation action for this step.
	/// </summary>
	/// <param name="context"> The saga execution context. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The compensation result. </returns>
	Task<StepResult> CompensateAsync(ISagaContext<TSagaData> context, CancellationToken cancellationToken);
}
