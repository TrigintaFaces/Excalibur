// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Diagnostics;

namespace Excalibur.Hosting.AzureFunctions;

/// <summary>
/// Fluent saga orchestration implementation for Azure Functions.
/// </summary>
/// <typeparam name="TSagaInput"> The saga input type. </typeparam>
/// <typeparam name="TSagaOutput"> The saga output type. </typeparam>
public sealed partial class FluentSagaOrchestration<TSagaInput, TSagaOutput>
{
	private readonly ILogger<FluentSagaOrchestration<TSagaInput, TSagaOutput>> _logger;
	private readonly List<ISagaStepDefinition> _steps;
	private readonly Func<TSagaInput, Task> _inputValidator;
	private readonly Func<TSagaInput, SagaState, Task<TSagaOutput>> _outputBuilder;
	private readonly Action<SagaState, Exception>? _errorHandler;
	private readonly int _maxRetryAttempts;
	private readonly TimeSpan _firstRetryInterval;
	private readonly double _backoffCoefficient;

	// Use Random.Shared for thread-safe jitter calculation (not security-sensitive)

	/// <summary>
	/// Initializes a new instance of the <see cref="FluentSagaOrchestration{TSagaInput, TSagaOutput}" /> class.
	/// </summary>
	/// <param name="logger"> The logger. </param>
	/// <param name="sagaName"> The saga name. </param>
	/// <param name="timeout"> The saga timeout. </param>
	/// <param name="autoCompensation"> Whether to enable automatic compensation. </param>
	/// <param name="maxRetryAttempts"> Maximum number of retry attempts per step. </param>
	/// <param name="firstRetryInterval"> Initial interval before first retry. </param>
	/// <param name="backoffCoefficient"> Exponential backoff coefficient for retry delays. </param>
	/// <param name="steps"> The saga steps. </param>
	/// <param name="inputValidator"> The input validator. </param>
	/// <param name="outputBuilder"> The output builder. </param>
	/// <param name="errorHandler"> The error handler. </param>
	internal FluentSagaOrchestration(
		ILogger<FluentSagaOrchestration<TSagaInput, TSagaOutput>> logger,
		string sagaName,
		TimeSpan? timeout,
		bool autoCompensation,
		int maxRetryAttempts,
		TimeSpan firstRetryInterval,
		double backoffCoefficient,
		List<ISagaStepDefinition> steps,
		Func<TSagaInput, Task> inputValidator,
		Func<TSagaInput, SagaState, Task<TSagaOutput>> outputBuilder,
		Action<SagaState, Exception>? errorHandler)
	{
		_logger = logger;
		SagaName = sagaName;
		Timeout = timeout;
		AutoCompensation = autoCompensation;
		_maxRetryAttempts = maxRetryAttempts;
		_firstRetryInterval = firstRetryInterval;
		_backoffCoefficient = backoffCoefficient;
		_steps = steps;
		_inputValidator = inputValidator;
		_outputBuilder = outputBuilder;
		_errorHandler = errorHandler;
	}

	/// <summary>
	/// Gets the saga name.
	/// </summary>
	/// <value> The saga name. </value>
	public string SagaName { get; }

	/// <summary>
	/// Gets the saga timeout.
	/// </summary>
	/// <value> The saga timeout. </value>
	public TimeSpan? Timeout { get; }

	/// <summary>
	/// Gets a value indicating whether automatic compensation is enabled.
	/// </summary>
	/// <value> A value indicating whether automatic compensation is enabled. </value>
	public bool AutoCompensation { get; }

	/// <summary>
	/// Gets the saga steps.
	/// </summary>
	/// <value> The saga steps. </value>
	public IReadOnlyList<ISagaStepDefinition> Steps => _steps.AsReadOnly();

	/// <summary>
	/// Executes the saga with the provided input.
	/// </summary>
	/// <param name="input"> The saga input. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The saga output. </returns>
	public async Task<TSagaOutput> ExecuteAsync(TSagaInput input, CancellationToken cancellationToken)
	{
		LogSagaExecutionStarting(SagaName);

		var sagaState = new SagaState();

		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			// Validate input
			await _inputValidator(input).ConfigureAwait(false);

			cancellationToken.ThrowIfCancellationRequested();

			// Execute steps
			await ExecuteStepsAsync(input, sagaState, cancellationToken).ConfigureAwait(false);

			cancellationToken.ThrowIfCancellationRequested();

			// Build output
			var output = await _outputBuilder(input, sagaState).ConfigureAwait(false);

			LogSagaExecutionCompleted(SagaName);
			return output;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			LogSagaExecutionFailed(ex, SagaName);

			try
			{
				_errorHandler?.Invoke(sagaState, ex);

				if (AutoCompensation)
				{
					await CompensateAsync(sagaState, cancellationToken).ConfigureAwait(false);
				}
			}
			catch (Exception compensationEx)
			{
				LogSagaCompensationFailed(compensationEx, SagaName);
			}

			throw;
		}
	}

	/// <summary>
	/// Simulates activity execution. In a real implementation, this would call Azure Functions activities.
	/// </summary>
	/// <param name="input"> The activity input. </param>
	/// <returns> The activity output. </returns>
	private static Task<object> SimulateActivityExecutionAsync(object input) =>

		// In a real implementation, this would use Microsoft.Azure.WebJobs.Extensions.DurableTask to call the actual Azure Functions activity
		Task.FromResult(input);

	/// <summary>
	/// Executes the saga steps.
	/// </summary>
	/// <param name="input"> The saga input. </param>
	/// <param name="state"> The saga state. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	private async Task ExecuteStepsAsync(TSagaInput input, SagaState state, CancellationToken cancellationToken)
	{
		foreach (var step in _steps)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (!step.ShouldExecute(input, state))
			{
				LogStepSkipping(step.Name);
				continue;
			}

			LogStepExecuting(step.Name);

			if (step.StepType == SagaStepType.ParallelGroup && step is SagaParallelStepGroupDefinition<TSagaInput> parallelGroup)
			{
				await ExecuteParallelStepsAsync(input, state, parallelGroup, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				await ExecuteSingleStepAsync(input, state, step, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// Executes parallel steps.
	/// </summary>
	/// <param name="input"> The saga input. </param>
	/// <param name="state"> The saga state. </param>
	/// <param name="parallelGroup"> The parallel group. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	private async Task ExecuteParallelStepsAsync(
		TSagaInput input,
		SagaState state,
		SagaParallelStepGroupDefinition<TSagaInput> parallelGroup,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var tasks = parallelGroup.Steps
			.Where(step => step.ShouldExecute(input, state))
			.Select(step => ExecuteSingleStepAsync(input, state, step, cancellationToken));

		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	/// <summary>
	/// Executes a single step with retry logic.
	/// </summary>
	/// <param name="input"> The saga input. </param>
	/// <param name="state"> The saga state. </param>
	/// <param name="step"> The step to execute. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	private async Task ExecuteSingleStepAsync(
		TSagaInput input,
		SagaState state,
		ISagaStepDefinition step,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(step.ActivityName))
		{
			// No activity to execute, just prepare input and process output
			// Lock state to prevent concurrent mutation from parallel steps
			lock (state.SyncLock)
			{
				var stepInput = step.PrepareInput(input, state);
				step.ProcessOutput(stepInput, state);
			}

			return;
		}

		// Execute with retry logic
		await ExecuteWithRetryAsync(step, async () =>
		{
			cancellationToken.ThrowIfCancellationRequested();

			object activityInput;
			lock (state.SyncLock)
			{
				// In a real Azure Functions implementation, this would call the actual activity
				activityInput = step.PrepareInput(input, state);
			}

			// Currently simulating activity execution - in production this would call the actual Azure Functions activity
			var activityOutput =
				await SimulateActivityExecutionAsync(activityInput).ConfigureAwait(false);

			lock (state.SyncLock)
			{
				step.ProcessOutput(activityOutput, state);
			}
		}, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Executes an action with exponential backoff retry logic.
	/// </summary>
	/// <param name="step"> The step being executed. </param>
	/// <param name="action"> The action to execute. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	private async Task ExecuteWithRetryAsync(ISagaStepDefinition step, Func<Task> action, CancellationToken cancellationToken)
	{
		var attempt = 0;
		Exception? lastException = null;

		while (attempt < _maxRetryAttempts)
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				if (attempt > 0)
				{
					LogStepRetryStarting(step.Name, attempt, _maxRetryAttempts);
				}

				await action().ConfigureAwait(false);

				if (attempt > 0)
				{
					LogStepRetrySucceeded(step.Name, attempt);
				}

				return;
			}
			catch (Exception ex) when (IsTransientException(ex) && attempt < _maxRetryAttempts - 1)
			{
				lastException = ex;
				attempt++;

				var delay = CalculateBackoffDelay(attempt);
				LogStepRetryFailed(ex, step.Name, attempt, delay.TotalMilliseconds);

				await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
			}
		}

		// Exhausted all retries
		LogRetryExhausted(lastException, step.Name, _maxRetryAttempts);
		throw lastException;
	}

	/// <summary>
	/// Calculates the backoff delay with jitter for a retry attempt.
	/// </summary>
	/// <param name="attempt"> The current retry attempt (1-based). </param>
	/// <returns> The delay to wait before the next retry. </returns>
	private TimeSpan CalculateBackoffDelay(int attempt)
	{
		// Exponential backoff: firstRetryInterval * (backoffCoefficient ^ (attempt - 1))
		var exponentialDelay = _firstRetryInterval.TotalMilliseconds * Math.Pow(_backoffCoefficient, attempt - 1);

		// Add jitter (0-10% of the delay) to prevent thundering herd
#pragma warning disable CA5394 // Random is not security-sensitive here
		var jitter = Random.Shared.NextDouble() * 0.1 * exponentialDelay;
#pragma warning restore CA5394

		var totalDelayMs = exponentialDelay + jitter;
		var delay = TimeSpan.FromMilliseconds(totalDelayMs);

		LogRetryDelayApplied(attempt, delay.TotalMilliseconds);

		return delay;
	}

	/// <summary>
	/// Determines if an exception is transient and should be retried.
	/// </summary>
	/// <param name="ex"> The exception to check. </param>
	/// <returns> True if the exception is transient; otherwise, false. </returns>
	private static bool IsTransientException(Exception ex) =>

		// Consider these exception types as transient:
		// - TimeoutException: Network/service timeout
		// - HttpRequestException: HTTP failures (might be transient)
		// - TaskCanceledException: Can indicate timeout
		// - InvalidOperationException with specific messages might be transient
		ex is TimeoutException or
			HttpRequestException or
			TaskCanceledException ||
		(ex is InvalidOperationException && ex.Message.Contains("temporarily", StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// Compensates the saga by executing compensation activities.
	/// </summary>
	/// <param name="state"> The saga state. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	// IDE0060: cancellationToken intentionally unused — compensation runs to completion for data consistency
#pragma warning disable IDE0060

	private async Task CompensateAsync(SagaState state, CancellationToken cancellationToken)
#pragma warning restore IDE0060
	{
		LogSagaCompensationStarting(SagaName);

		// Execute compensation activities in reverse order (LIFO)
		// Note: do not check cancellationToken during compensation — compensation
		// should run to completion to maintain data consistency, even if cancelled.
		for (var i = _steps.Count - 1; i >= 0; i--)
		{
			var step = _steps[i];

			if (!string.IsNullOrEmpty(step.CompensationActivityName))
			{
				LogCompensationExecuting(step.Name);

				try
				{
					// Currently simulating compensation activity - in production this would call the actual Azure Functions compensation activity
					_ = await SimulateActivityExecutionAsync(state).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					LogCompensationFailed(ex, step.Name);

					// Continue with other compensations even if one fails
				}
			}
		}

		LogSagaCompensationCompleted(SagaName);
	}

	// Source-generated logging methods (Sprint 507 - Moved from Dispatch to Excalibur)
	[LoggerMessage(ExcaliburHostingEventId.SagaOrchestrationStarted, LogLevel.Information, "Starting saga execution: {SagaName}")]
	private partial void LogSagaExecutionStarting(string sagaName);

	[LoggerMessage(ExcaliburHostingEventId.SagaOrchestrationCompleted, LogLevel.Information,
		"Saga execution completed successfully: {SagaName}")]
	private partial void LogSagaExecutionCompleted(string sagaName);

	[LoggerMessage(ExcaliburHostingEventId.SagaOrchestrationFailed, LogLevel.Error, "Saga execution failed: {SagaName}")]
	private partial void LogSagaExecutionFailed(Exception ex, string sagaName);

	[LoggerMessage(ExcaliburHostingEventId.SagaCompensationFailed, LogLevel.Error, "Saga compensation failed: {SagaName}")]
	private partial void LogSagaCompensationFailed(Exception compensationEx, string sagaName);

	[LoggerMessage(ExcaliburHostingEventId.SagaStepSkipping, LogLevel.Debug, "Skipping step: {StepName}")]
	private partial void LogStepSkipping(string stepName);

	[LoggerMessage(ExcaliburHostingEventId.SagaStepExecuting, LogLevel.Debug, "Executing step: {StepName}")]
	private partial void LogStepExecuting(string stepName);

	[LoggerMessage(ExcaliburHostingEventId.SagaCompensationStarted, LogLevel.Information, "Starting saga compensation: {SagaName}")]
	private partial void LogSagaCompensationStarting(string sagaName);

	[LoggerMessage(ExcaliburHostingEventId.SagaCompensationStepExecuting, LogLevel.Debug, "Executing compensation for step: {StepName}")]
	private partial void LogCompensationExecuting(string stepName);

	[LoggerMessage(ExcaliburHostingEventId.SagaCompensationStepFailed, LogLevel.Error, "Compensation failed for step: {StepName}")]
	private partial void LogCompensationFailed(Exception ex, string stepName);

	[LoggerMessage(ExcaliburHostingEventId.SagaCompensationCompleted, LogLevel.Information, "Saga compensation completed: {SagaName}")]
	private partial void LogSagaCompensationCompleted(string sagaName);

	[LoggerMessage(ExcaliburHostingEventId.SagaStepRetryStarting, LogLevel.Warning,
		"Retrying step {StepName}: attempt {Attempt} of {MaxAttempts}")]
	private partial void LogStepRetryStarting(string stepName, int attempt, int maxAttempts);

	[LoggerMessage(ExcaliburHostingEventId.SagaStepRetrySucceeded, LogLevel.Information,
		"Step {StepName} succeeded on retry attempt {Attempt}")]
	private partial void LogStepRetrySucceeded(string stepName, int attempt);

	[LoggerMessage(ExcaliburHostingEventId.SagaStepRetryFailed, LogLevel.Warning,
		"Step {StepName} failed on attempt {Attempt}, will retry after {DelayMs}ms")]
	private partial void LogStepRetryFailed(Exception ex, string stepName, int attempt, double delayMs);

	[LoggerMessage(ExcaliburHostingEventId.SagaRetryExhausted, LogLevel.Error, "Step {StepName} failed after {MaxAttempts} attempts")]
	private partial void LogRetryExhausted(Exception ex, string stepName, int maxAttempts);

	[LoggerMessage(ExcaliburHostingEventId.SagaRetryDelayApplied, LogLevel.Debug, "Retry delay for attempt {Attempt}: {DelayMs}ms")]
	private partial void LogRetryDelayApplied(int attempt, double delayMs);
}
