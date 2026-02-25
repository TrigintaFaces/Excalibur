// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Hosting.AzureFunctions;

/// <summary>
/// Fluent builder for configuring saga steps.
/// </summary>
/// <typeparam name="TSagaInput"> The saga input type. </typeparam>
/// <typeparam name="TStepInput"> The step input type. </typeparam>
/// <typeparam name="TStepOutput"> The step output type. </typeparam>
/// <remarks> Initializes a new instance of the <see cref="SagaStepBuilder{TSagaInput, TStepInput, TStepOutput}" /> class. </remarks>
/// <param name="stepName"> The step name. </param>
/// <param name="condition"> Optional condition for conditional steps. </param>
#pragma warning disable CA1005 // Intentional: 3 type parameters required for saga/step input/output type safety (Sprint 329)
public sealed class SagaStepBuilder<TSagaInput, TStepInput, TStepOutput>(string stepName, Func<TSagaInput, SagaState, bool>? condition = null)
#pragma warning restore CA1005
{
	private string? _activityName;
	private string? _compensationActivityName;
	private TimeSpan? _timeout;
	private int _retryCount;
	private Func<TSagaInput, SagaState, TStepInput>? _inputMapper;
	private Action<TStepOutput?, SagaState>? _outputHandler;

	/// <summary>
	/// Specifies the activity to execute for this step.
	/// </summary>
	/// <param name="activityName"> The activity name. </param>
	/// <returns> The step builder instance for fluent chaining. </returns>
	public SagaStepBuilder<TSagaInput, TStepInput, TStepOutput> ExecuteActivity(string activityName)
	{
		_activityName = activityName;
		return this;
	}

	/// <summary>
	/// Specifies the input mapping for the step.
	/// </summary>
	/// <param name="inputMapper"> The input mapping function. </param>
	/// <returns> The step builder instance for fluent chaining. </returns>
	public SagaStepBuilder<TSagaInput, TStepInput, TStepOutput> WithInput(
		Func<TSagaInput, SagaState, TStepInput> inputMapper)
	{
		_inputMapper = inputMapper;
		return this;
	}

	/// <summary>
	/// Specifies the output handler for the step.
	/// </summary>
	/// <param name="outputHandler"> The output handler function. </param>
	/// <returns> The step builder instance for fluent chaining. </returns>
	public SagaStepBuilder<TSagaInput, TStepInput, TStepOutput> WithOutput(
		Action<TStepOutput?, SagaState> outputHandler)
	{
		_outputHandler = outputHandler;
		return this;
	}

	/// <summary>
	/// Specifies the compensation activity for the step.
	/// </summary>
	/// <param name="compensationActivityName"> The compensation activity name. </param>
	/// <returns> The step builder instance for fluent chaining. </returns>
	public SagaStepBuilder<TSagaInput, TStepInput, TStepOutput> WithCompensation(
		string compensationActivityName)
	{
		_compensationActivityName = compensationActivityName;
		return this;
	}

	/// <summary>
	/// Specifies the retry count for the step.
	/// </summary>
	/// <param name="retryCount"> The retry count. </param>
	/// <returns> The step builder instance for fluent chaining. </returns>
	public SagaStepBuilder<TSagaInput, TStepInput, TStepOutput> WithRetry(int retryCount)
	{
		_retryCount = retryCount;
		return this;
	}

	/// <summary>
	/// Specifies the timeout for the step.
	/// </summary>
	/// <param name="timeout"> The timeout duration. </param>
	/// <returns> The step builder instance for fluent chaining. </returns>
	public SagaStepBuilder<TSagaInput, TStepInput, TStepOutput> WithTimeout(TimeSpan timeout)
	{
		_timeout = timeout;
		return this;
	}

	/// <summary>
	/// Builds the step definition.
	/// </summary>
	/// <returns> The built step definition. </returns>
	internal ISagaStepDefinition Build() =>
		new SagaStepDefinition<TSagaInput, TStepInput, TStepOutput>(
			stepName,
			_activityName,
			_compensationActivityName,
			_timeout,
			_retryCount,
			condition,
			_inputMapper,
			_outputHandler);
}
