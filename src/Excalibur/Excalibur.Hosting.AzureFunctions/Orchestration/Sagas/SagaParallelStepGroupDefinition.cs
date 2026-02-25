// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Hosting.AzureFunctions;

/// <summary>
/// Definition for a parallel step group in saga orchestrations.
/// </summary>
/// <typeparam name="TSagaInput"> The saga input type. </typeparam>
/// <remarks> Initializes a new instance of the <see cref="SagaParallelStepGroupDefinition{TSagaInput}" /> class. </remarks>
/// <param name="groupName"> The group name. </param>
/// <param name="steps"> The steps in the group. </param>
internal sealed class SagaParallelStepGroupDefinition<TSagaInput>(string groupName, List<ISagaStepDefinition> steps) : ISagaStepDefinition
{
	/// <inheritdoc />
	public string Name { get; } = groupName;

	/// <inheritdoc />
	public SagaStepType StepType => SagaStepType.ParallelGroup;

	/// <inheritdoc />
	public string? ActivityName => null;

	/// <inheritdoc />
	public string? CompensationActivityName => null;

	/// <inheritdoc />
	public TimeSpan? Timeout => null;

	/// <inheritdoc />
	public int RetryCount => 0;

	/// <summary>
	/// Gets the steps in the parallel group.
	/// </summary>
	/// <value> The steps in the parallel group. </value>
	public IReadOnlyList<ISagaStepDefinition> Steps => steps.AsReadOnly();

	/// <inheritdoc />
	public bool ShouldExecute(object sagaInput, SagaState state) => true; // Parallel groups always execute if reached

	/// <inheritdoc />
	public object PrepareInput(object sagaInput, SagaState state) => sagaInput; // Parallel groups pass through the saga input

	/// <inheritdoc />
	public void ProcessOutput(object? output, SagaState state)
	{
		// Parallel groups don't process output directly Each individual step handles its own output
	}
}
