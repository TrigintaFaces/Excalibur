// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Hosting.AzureFunctions;

/// <summary>
/// Represents a step definition in a saga orchestration.
/// </summary>
public interface ISagaStepDefinition
{
	/// <summary>
	/// Gets the step name.
	/// </summary>
	/// <value> The step name. </value>
	string Name { get; }

	/// <summary>
	/// Gets the step type.
	/// </summary>
	/// <value> The step type. </value>
	SagaStepType StepType { get; }

	/// <summary>
	/// Gets the activity name to execute.
	/// </summary>
	/// <value> The activity name to execute. </value>
	string? ActivityName { get; }

	/// <summary>
	/// Gets the compensation activity name.
	/// </summary>
	/// <value> The compensation activity name. </value>
	string? CompensationActivityName { get; }

	/// <summary>
	/// Gets the step timeout.
	/// </summary>
	/// <value> The step timeout. </value>
	TimeSpan? Timeout { get; }

	/// <summary>
	/// Gets the retry count.
	/// </summary>
	/// <value> The retry count. </value>
	int RetryCount { get; }

	/// <summary>
	/// Determines if the step should execute based on the current state.
	/// </summary>
	/// <param name="sagaInput"> The saga input. </param>
	/// <param name="state"> The saga state. </param>
	/// <returns> True if the step should execute; otherwise, false. </returns>
	bool ShouldExecute(object sagaInput, SagaState state);

	/// <summary>
	/// Prepares the input for the step.
	/// </summary>
	/// <param name="sagaInput"> The saga input. </param>
	/// <param name="state"> The saga state. </param>
	/// <returns> The step input. </returns>
	object PrepareInput(object sagaInput, SagaState state);

	/// <summary>
	/// Processes the step output.
	/// </summary>
	/// <param name="output"> The step output. </param>
	/// <param name="state"> The saga state. </param>
	void ProcessOutput(object? output, SagaState state);
}

/// <summary>
/// Represents the type of saga step.
/// </summary>
public enum SagaStepType
{
	/// <summary>
	/// A single step that executes an activity.
	/// </summary>
	Single = 0,

	/// <summary>
	/// A parallel group of steps.
	/// </summary>
	ParallelGroup = 1,
}
