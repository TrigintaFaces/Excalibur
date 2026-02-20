// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Hosting.AzureFunctions;

/// <summary>
/// Implementation of a saga step definition.
/// </summary>
/// <typeparam name="TSagaInput"> The saga input type. </typeparam>
/// <typeparam name="TStepInput"> The step input type. </typeparam>
/// <typeparam name="TStepOutput"> The step output type. </typeparam>
/// <remarks> Initializes a new instance of the <see cref="SagaStepDefinition{TSagaInput, TStepInput, TStepOutput}" /> class. </remarks>
/// <param name="name"> The step name. </param>
/// <param name="activityName"> The activity name. </param>
/// <param name="compensationActivityName"> The compensation activity name. </param>
/// <param name="timeout"> The step timeout. </param>
/// <param name="retryCount"> The retry count. </param>
/// <param name="condition"> The optional condition function. </param>
/// <param name="inputMapper"> The input mapper function. </param>
/// <param name="outputHandler"> The output handler function. </param>
internal sealed class SagaStepDefinition<TSagaInput, TStepInput, TStepOutput>(
	string name,
	string? activityName,
	string? compensationActivityName,
	TimeSpan? timeout,
	int retryCount,
	Func<TSagaInput, SagaState, bool>? condition,
	Func<TSagaInput, SagaState, TStepInput>? inputMapper,
	Action<TStepOutput?, SagaState>? outputHandler) : ISagaStepDefinition
{
	/// <inheritdoc />
	public string Name { get; } = name;

	/// <inheritdoc />
	public SagaStepType StepType => SagaStepType.Single;

	/// <inheritdoc />
	public string? ActivityName { get; } = activityName;

	/// <inheritdoc />
	public string? CompensationActivityName { get; } = compensationActivityName;

	/// <inheritdoc />
	public TimeSpan? Timeout { get; } = timeout;

	/// <inheritdoc />
	public int RetryCount { get; } = retryCount;

	/// <inheritdoc />
	public bool ShouldExecute(object sagaInput, SagaState state)
	{
		if (condition == null)
		{
			return true;
		}

		return condition((TSagaInput)sagaInput, state);
	}

	/// <inheritdoc />
	public object PrepareInput(object sagaInput, SagaState state)
	{
		if (inputMapper == null)
		{
			return sagaInput;
		}

		return inputMapper((TSagaInput)sagaInput, state) ?? throw new InvalidOperationException("Input mapper returned null");
	}

	/// <inheritdoc />
	public void ProcessOutput(object? output, SagaState state)
	{
		if (outputHandler != null && output is TStepOutput typedOutput)
		{
			outputHandler(typedOutput, state);
		}
	}
}
