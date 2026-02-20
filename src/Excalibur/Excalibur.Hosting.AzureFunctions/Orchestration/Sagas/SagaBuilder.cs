// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Hosting.AzureFunctions;

/// <summary>
/// Fluent builder for creating saga orchestrations in Azure Functions.
/// </summary>
/// <typeparam name="TSagaInput"> The input type for the saga. </typeparam>
/// <typeparam name="TSagaOutput"> The output type for the saga. </typeparam>
/// <remarks> Initializes a new instance of the <see cref="SagaBuilder{TSagaInput, TSagaOutput}" /> class. </remarks>
/// <param name="sagaName"> The name of the saga. </param>
/// <exception cref="ArgumentNullException"> Thrown when sagaName is null. </exception>
public sealed class SagaBuilder<TSagaInput, TSagaOutput>(string sagaName)
	where TSagaInput : class
	where TSagaOutput : class
{
	private readonly string _sagaName = sagaName ?? throw new ArgumentNullException(nameof(sagaName));
	private readonly List<ISagaStepDefinition> _steps = [];
	private TimeSpan _timeout = TimeSpan.FromMinutes(30);
	private bool _autoCompensation = true;
	private Func<TSagaInput, Task>? _inputValidator;
	private Func<TSagaInput, SagaState, Task<TSagaOutput>>? _outputBuilder;
	private Action<SagaState, Exception>? _errorHandler;

	private int _maxRetryAttempts = 3;
	private TimeSpan _firstRetryInterval = TimeSpan.FromSeconds(1);
	private double _backoffCoefficient = 2.0;

	/// <summary>
	/// Creates a new saga builder with the specified name.
	/// </summary>
	/// <param name="sagaName"> The name of the saga. </param>
	/// <returns> A new saga builder instance. </returns>
	public static SagaBuilder<TSagaInput, TSagaOutput> Create(string sagaName) =>
		new(sagaName);

	/// <summary>
	/// Configures the timeout for the saga.
	/// </summary>
	/// <param name="timeout"> The timeout duration. </param>
	/// <returns> The saga builder instance for fluent chaining. </returns>
	public SagaBuilder<TSagaInput, TSagaOutput> WithTimeout(TimeSpan timeout)
	{
		_timeout = timeout;
		return this;
	}

	/// <summary>
	/// Configures auto-compensation behavior.
	/// </summary>
	/// <param name="enabled"> Whether auto-compensation is enabled. </param>
	/// <returns> The saga builder instance for fluent chaining. </returns>
	public SagaBuilder<TSagaInput, TSagaOutput> WithAutoCompensation(bool enabled)
	{
		_autoCompensation = enabled;
		return this;
	}

	/// <summary>
	/// Configures the default retry policy.
	/// </summary>
	/// <param name="maxAttempts"> Maximum number of retry attempts. </param>
	/// <param name="firstRetryInterval"> Interval before first retry. </param>
	/// <param name="backoffCoefficient"> Exponential backoff coefficient. </param>
	/// <returns> The saga builder instance for fluent chaining. </returns>
	public SagaBuilder<TSagaInput, TSagaOutput> WithDefaultRetry(
		int maxAttempts,
		TimeSpan firstRetryInterval,
		double backoffCoefficient)
	{
		_maxRetryAttempts = maxAttempts;
		_firstRetryInterval = firstRetryInterval;
		_backoffCoefficient = backoffCoefficient;
		return this;
	}

	/// <summary>
	/// Configures input validation.
	/// </summary>
	/// <param name="validator"> The input validation function. </param>
	/// <returns> The saga builder instance for fluent chaining. </returns>
	public SagaBuilder<TSagaInput, TSagaOutput> WithInputValidation(Func<TSagaInput, Task> validator)
	{
		_inputValidator = validator;
		return this;
	}

	/// <summary>
	/// Configures the output builder function.
	/// </summary>
	/// <param name="outputBuilder"> The output builder function. </param>
	/// <returns> The saga builder instance for fluent chaining. </returns>
	public SagaBuilder<TSagaInput, TSagaOutput> WithOutputBuilder(
		Func<TSagaInput, SagaState, Task<TSagaOutput>> outputBuilder)
	{
		_outputBuilder = outputBuilder;
		return this;
	}

	/// <summary>
	/// Configures the error handler.
	/// </summary>
	/// <param name="errorHandler"> The error handler function. </param>
	/// <returns> The saga builder instance for fluent chaining. </returns>
	public SagaBuilder<TSagaInput, TSagaOutput> WithErrorHandler(Action<SagaState, Exception> errorHandler)
	{
		_errorHandler = errorHandler;
		return this;
	}

	/// <summary>
	/// Adds a step to the saga.
	/// </summary>
	/// <typeparam name="TStepInput"> The step input type. </typeparam>
	/// <typeparam name="TStepOutput"> The step output type. </typeparam>
	/// <param name="stepName"> The name of the step. </param>
	/// <param name="configure"> The step configuration function. </param>
	/// <returns> The saga builder instance for fluent chaining. </returns>
	public SagaBuilder<TSagaInput, TSagaOutput> AddStep<TStepInput, TStepOutput>(
		string stepName,
		Func<SagaStepBuilder<TSagaInput, TStepInput, TStepOutput>, SagaStepBuilder<TSagaInput, TStepInput, TStepOutput>> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		var stepBuilder = new SagaStepBuilder<TSagaInput, TStepInput, TStepOutput>(stepName);
		var configuredBuilder = configure(stepBuilder);
		_steps.Add(configuredBuilder.Build());
		return this;
	}

	/// <summary>
	/// Adds a conditional step to the saga.
	/// </summary>
	/// <typeparam name="TStepInput"> The step input type. </typeparam>
	/// <typeparam name="TStepOutput"> The step output type. </typeparam>
	/// <param name="stepName"> The name of the step. </param>
	/// <param name="condition"> The condition function. </param>
	/// <param name="configure"> The step configuration function. </param>
	/// <returns> The saga builder instance for fluent chaining. </returns>
	public SagaBuilder<TSagaInput, TSagaOutput> AddConditionalStep<TStepInput, TStepOutput>(
		string stepName,
		Func<TSagaInput, SagaState, bool> condition,
		Func<SagaStepBuilder<TSagaInput, TStepInput, TStepOutput>, SagaStepBuilder<TSagaInput, TStepInput, TStepOutput>> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		var stepBuilder = new SagaStepBuilder<TSagaInput, TStepInput, TStepOutput>(stepName, condition);
		var configuredBuilder = configure(stepBuilder);
		_steps.Add(configuredBuilder.Build());
		return this;
	}

	/// <summary>
	/// Adds parallel steps to the saga.
	/// </summary>
	/// <param name="groupName"> The name of the parallel group. </param>
	/// <param name="configure"> The parallel group configuration function. </param>
	/// <returns> The saga builder instance for fluent chaining. </returns>
	public SagaBuilder<TSagaInput, TSagaOutput> AddParallelSteps(
		string groupName,
		Func<SagaParallelStepGroupBuilder<TSagaInput, TSagaOutput>, SagaParallelStepGroupBuilder<TSagaInput, TSagaOutput>> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		var groupBuilder = new SagaParallelStepGroupBuilder<TSagaInput, TSagaOutput>(groupName);
		var configuredBuilder = configure(groupBuilder);
		_steps.Add(configuredBuilder.Build());
		return this;
	}

	/// <summary>
	/// Builds the saga orchestration.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	/// <returns> The built saga orchestration. </returns>
	/// <exception cref="InvalidOperationException"> Thrown when the output builder is not configured. </exception>
	public FluentSagaOrchestration<TSagaInput, TSagaOutput> Build(ILogger<FluentSagaOrchestration<TSagaInput, TSagaOutput>> logger)
	{
		if (_outputBuilder == null)
		{
			throw new InvalidOperationException("Output builder is required. Use WithOutputBuilder to configure it.");
		}

		return new FluentSagaOrchestration<TSagaInput, TSagaOutput>(
			logger,
			_sagaName,
			_timeout,
			_autoCompensation,
			_maxRetryAttempts,
			_firstRetryInterval,
			_backoffCoefficient,
			_steps,
			_inputValidator ?? (static _ => Task.CompletedTask),
			_outputBuilder,
			_errorHandler);
	}
}
