// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Text;
using System.Threading.Tasks.Dataflow;

using Excalibur.Dispatch.Abstractions;

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Diagnostics;
using Excalibur.Saga.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using StepResult = Excalibur.Saga.Abstractions.StepResult;

namespace Excalibur.Saga.Implementation;

/// <summary>
/// Implements a parallel saga step that executes multiple child steps concurrently.
/// </summary>
/// <typeparam name="TData"> The type of data for the saga. </typeparam>
/// <remarks> Initializes a new instance of the <see cref="ParallelSagaStep{TData}" /> class. </remarks>
/// <param name="name"> The name of the step. </param>
/// <param name="parallelSteps"> The steps to execute in parallel. </param>
/// <param name="logger"> The logger. </param>
public partial class ParallelSagaStep<TData>(
	string name,
	IEnumerable<ISagaStep<TData>>? parallelSteps,
	ILogger<ParallelSagaStep<TData>>? logger = null) : IParallelSagaStep<TData>
	where TData : class
{
	/// <summary>
	/// Cached composite format for error messages.
	/// </summary>
	private static readonly CompositeFormat ParallelismStrategyNotSupportedFormat =
		CompositeFormat.Parse(ErrorConstants.ParallelismStrategyNotSupported);

	private readonly ILogger _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<ParallelSagaStep<TData>>();

	private readonly List<ISagaStep<TData>> _parallelSteps =
		parallelSteps?.ToList() ?? throw new ArgumentNullException(nameof(parallelSteps));

	/// <inheritdoc />
	public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

	/// <inheritdoc />
	public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <inheritdoc />
	public RetryPolicy? RetryPolicy { get; set; }

	/// <inheritdoc />
	public bool CanCompensate => _parallelSteps.TrueForAll(static s => s.CanCompensate);

	/// <inheritdoc />
	public IReadOnlyList<ISagaStep<TData>> ParallelSteps => _parallelSteps.AsReadOnly();

	/// <inheritdoc />
	public ParallelismStrategy Strategy { get; set; } = ParallelismStrategy.Limited;

	/// <inheritdoc />
	public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

	/// <inheritdoc />
	public bool RequireAllSuccess { get; set; } = true;

	/// <inheritdoc />
	public bool ContinueOnFailure { get; set; }

	/// <inheritdoc />
	public async Task<StepResult> ExecuteAsync(
		ISagaContext<TData> context,
		CancellationToken cancellationToken)
	{
		LogParallelStepExecutionStarted(Name, _parallelSteps.Count);

		_ = new List<StepResult>();

		var results = Strategy switch
		{
			ParallelismStrategy.Unlimited => await ExecuteUnlimitedAsync(context, cancellationToken).ConfigureAwait(false),
			ParallelismStrategy.Limited => await ExecuteLimitedAsync(context, cancellationToken).ConfigureAwait(false),
			ParallelismStrategy.Batched => await ExecuteBatchedAsync(context, cancellationToken).ConfigureAwait(false),
			ParallelismStrategy.Adaptive => await ExecuteAdaptiveAsync(context, cancellationToken).ConfigureAwait(false),
			_ => throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, ParallelismStrategyNotSupportedFormat,
				Strategy)),
		};
		return AggregateResults(results);
	}

	/// <inheritdoc />
	public async Task<StepResult> CompensateAsync(
		ISagaContext<TData> context,
		CancellationToken cancellationToken)
	{
		LogParallelStepCompensationStarted(Name, _parallelSteps.Count);

		// Compensate in reverse order, but still in parallel
		var stepsToCompensate = _parallelSteps.Where(s => s.CanCompensate).Reverse().ToList();
		var compensationTasks = stepsToCompensate.Select(step =>
			CompensateStepAsync(step, context, cancellationToken));

		var results = await Task.WhenAll(compensationTasks).ConfigureAwait(false);
		return AggregateResults(results.ToList());
	}

	/// <inheritdoc />
	public StepResult AggregateResults(IReadOnlyList<StepResult> results)
	{
		ArgumentNullException.ThrowIfNull(results);
		if (!results.Any())
		{
			return StepResult.Success();
		}

		var allSucceeded = results.All(static r => r.IsSuccess);
		var anyFailed = results.Any(static r => !r.IsSuccess);

		if (RequireAllSuccess && !allSucceeded)
		{
			var failedSteps = results.Where(static r => !r.IsSuccess).Select(static r => r.ErrorMessage);
			return StepResult.Failure($"Parallel execution failed: {string.Join("; ", failedSteps)}");
		}

		if (!RequireAllSuccess && anyFailed && !ContinueOnFailure)
		{
			var firstFailure = results.First(static r => !r.IsSuccess);
			return StepResult.Failure($"Parallel execution failed: {firstFailure.ErrorMessage}");
		}

		// Aggregate data from all successful results
		var aggregatedData = new Dictionary<string, object>(StringComparer.Ordinal);
		var index = 0;
		foreach (var result in results)
		{
			if (result is { IsSuccess: true, OutputData: not null })
			{
				foreach (var kvp in result.OutputData)
				{
					aggregatedData[$"step_{index.ToString(CultureInfo.InvariantCulture)}_{kvp.Key}"] = kvp.Value;
				}
			}

			index++;
		}

		return StepResult.Success(aggregatedData);
	}

	private async Task<List<StepResult>> ExecuteUnlimitedAsync(
		ISagaContext<TData> context,
		CancellationToken cancellationToken)
	{
		var tasks = _parallelSteps.Select(step => ExecuteStepAsync(step, context, cancellationToken));
		var results = await Task.WhenAll(tasks).ConfigureAwait(false);
		return [.. results];
	}

	private async Task<List<StepResult>> ExecuteLimitedAsync(
		ISagaContext<TData> context,
		CancellationToken cancellationToken)
	{
		using var semaphore = new SemaphoreSlim(MaxDegreeOfParallelism, MaxDegreeOfParallelism);
		var results = new List<StepResult>();
		var tasks = new List<Task<StepResult>>();

		foreach (var step in _parallelSteps)
		{
			await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

			var task = Task.Run(
				async () =>
				{
					try
					{
						return await ExecuteStepAsync(step, context, cancellationToken).ConfigureAwait(false);
					}
					finally
					{
						_ = semaphore.Release();
					}
				}, cancellationToken);

			tasks.Add(task);
		}

		results.AddRange(await Task.WhenAll(tasks).ConfigureAwait(false));
		return results;
	}

	private async Task<List<StepResult>> ExecuteBatchedAsync(
		ISagaContext<TData> context,
		CancellationToken cancellationToken)
	{
		var results = new List<StepResult>();
		var batchSize = MaxDegreeOfParallelism;

		for (var i = 0; i < _parallelSteps.Count; i += batchSize)
		{
			var batch = _parallelSteps.Skip(i).Take(batchSize);
			var batchTasks = batch.Select(step => ExecuteStepAsync(step, context, cancellationToken));
			var batchResults = await Task.WhenAll(batchTasks).ConfigureAwait(false);
			results.AddRange(batchResults);

			// Check if we should continue based on results
			if (!ContinueOnFailure && batchResults.Any(r => !r.IsSuccess))
			{
				break;
			}
		}

		return results;
	}

	private async Task<List<StepResult>> ExecuteAdaptiveAsync(
		ISagaContext<TData> context,
		CancellationToken cancellationToken)
	{
		// Use TPL Dataflow for adaptive parallelism
		var actionBlock = new ActionBlock<ISagaStep<TData>>(
			async step => await ExecuteStepAsync(step, context, cancellationToken).ConfigureAwait(false),
			new ExecutionDataflowBlockOptions
			{
				MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
				CancellationToken = cancellationToken,
			});

		foreach (var step in _parallelSteps)
		{
			_ = await actionBlock.SendAsync(step, cancellationToken).ConfigureAwait(false);
		}

		actionBlock.Complete();
		await actionBlock.Completion.ConfigureAwait(false);

		// For adaptive strategy, we'd collect results differently This is a simplified implementation
		return await ExecuteLimitedAsync(context, cancellationToken).ConfigureAwait(false);
	}

	private async Task<StepResult> ExecuteStepAsync(
		ISagaStep<TData> step,
		ISagaContext<TData> context,
		CancellationToken cancellationToken)
	{
		try
		{
			LogStepExecutionStarted(step.Name);

			using var stepCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			stepCts.CancelAfter(step.Timeout);

			var result = await step.ExecuteAsync(context, stepCts.Token).ConfigureAwait(false);
			if (result?.OutputData != null)
			{
				result.OutputData["StepName"] = step.Name;
			}

			LogStepExecutionCompleted(step.Name, result?.IsSuccess ?? false);

			return result ?? StepResult.Failure($"Parallel step '{step.Name}' returned null result");
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			LogStepCancelled(step.Name);

			return StepResult.Failure($"Step '{step.Name}' was cancelled");
		}
		catch (Exception ex)
		{
			LogStepExecutionFailed(step.Name, ex);

			return StepResult.Failure($"Step '{step.Name}' failed: {ex.Message}");
		}
	}

	private async Task<StepResult> CompensateStepAsync(
		ISagaStep<TData> step,
		ISagaContext<TData> context,
		CancellationToken cancellationToken)
	{
		try
		{
			LogStepCompensationStarted(step.Name);

			using var stepCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			stepCts.CancelAfter(step.Timeout);

			var result = await step.CompensateAsync(context, stepCts.Token).ConfigureAwait(false);
			if (result?.OutputData != null)
			{
				result.OutputData["StepName"] = step.Name;
			}

			LogStepCompensationCompleted(step.Name, result?.IsSuccess ?? false);

			return result ?? StepResult.Failure($"Parallel step '{step.Name}' returned null result");
		}
		catch (Exception ex)
		{
			LogStepCompensationFailed(step.Name, ex);

			return StepResult.Failure($"Step '{step.Name}' compensation failed: {ex.Message}");
		}
	}

	// Source-generated logging methods
	[LoggerMessage(SagaEventId.ParallelStepExecutionStarted, LogLevel.Information,
		"Executing parallel saga step '{StepName}' with {StepCount} parallel steps")]
	private partial void LogParallelStepExecutionStarted(string stepName, int stepCount);

	[LoggerMessage(SagaEventId.ParallelStepCompensationStarted, LogLevel.Information,
		"Compensating parallel saga step '{StepName}' with {StepCount} parallel steps")]
	private partial void LogParallelStepCompensationStarted(string stepName, int stepCount);

	[LoggerMessage(SagaEventId.StartingParallelStepExecution, LogLevel.Debug,
		"Executing parallel step '{StepName}'")]
	private partial void LogStepExecutionStarted(string stepName);

	[LoggerMessage(SagaEventId.ParallelStepCompleted, LogLevel.Debug,
		"Parallel step '{StepName}' completed with result: {IsSuccess}")]
	private partial void LogStepExecutionCompleted(string stepName, bool isSuccess);

	[LoggerMessage(SagaEventId.ParallelStepFailed, LogLevel.Warning,
		"Parallel step '{StepName}' was cancelled")]
	private partial void LogStepCancelled(string stepName);

	[LoggerMessage(SagaEventId.ParallelExecutionFailed, LogLevel.Error,
		"Parallel step '{StepName}' failed with exception")]
	private partial void LogStepExecutionFailed(string stepName, Exception ex);

	[LoggerMessage(SagaEventId.CompensatingParallelStep, LogLevel.Debug,
		"Compensating parallel step '{StepName}'")]
	private partial void LogStepCompensationStarted(string stepName);

	[LoggerMessage(SagaEventId.ParallelCompensationCompleted, LogLevel.Debug,
		"Parallel step '{StepName}' compensation completed with result: {IsSuccess}")]
	private partial void LogStepCompensationCompleted(string stepName, bool isSuccess);

	[LoggerMessage(SagaEventId.ParallelCompensationFailed, LogLevel.Error,
		"Parallel step '{StepName}' compensation failed with exception")]
	private partial void LogStepCompensationFailed(string stepName, Exception ex);
}
