// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Diagnostics;
using Excalibur.Saga.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using StepResult = Excalibur.Saga.Abstractions.StepResult;

namespace Excalibur.Saga.Implementation;

/// <summary>
/// Implements a conditional saga step that determines execution path based on conditions.
/// </summary>
/// <typeparam name="TData"> The type of data for the saga. </typeparam>
/// <remarks> Initializes a new instance of the <see cref="ConditionalSagaStep{TData}" /> class. </remarks>
/// <param name="name"> The name of the step. </param>
/// <param name="condition"> The condition evaluation function. </param>
/// <param name="thenStep"> The step to execute when condition is true. </param>
/// <param name="elseStep"> The step to execute when condition is false. </param>
/// <param name="logger"> The logger. </param>
public partial class ConditionalSagaStep<TData>(
	string name,
	Func<ISagaContext<TData>, CancellationToken, Task<bool>> condition,
	ISagaStep<TData>? thenStep = null,
	ISagaStep<TData>? elseStep = null,
	ILogger<ConditionalSagaStep<TData>>? logger = null) : IConditionalSagaStep<TData>
	where TData : class
{
	private readonly ILogger _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<ConditionalSagaStep<TData>>();

	private readonly Func<ISagaContext<TData>, CancellationToken, Task<bool>> _condition =
		condition ?? throw new ArgumentNullException(nameof(condition));

	private ISagaStep<TData>? _executedStep;

	/// <inheritdoc />
	public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

	/// <inheritdoc />
	public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <inheritdoc />
	public RetryPolicy? RetryPolicy { get; set; }

	/// <inheritdoc />
	public bool CanCompensate =>
		(ThenStep?.CanCompensate ?? true) && (ElseStep?.CanCompensate ?? true);

	/// <inheritdoc />
	public ISagaStep<TData>? ThenStep { get; } = thenStep;

	/// <inheritdoc />
	public ISagaStep<TData>? ElseStep { get; } = elseStep;

	/// <inheritdoc />
	public BranchingStrategy Strategy { get; set; } = BranchingStrategy.Simple;

	/// <summary>
	/// Creates a builder for constructing conditional saga steps.
	/// </summary>
	/// <param name="name"> The name of the step. </param>
	/// <returns> A new builder instance. </returns>
	// R0.8: Deprecated analyzer - migrate to CA1000 when addressing generic static member pattern
#pragma warning disable MA0018
	public static ConditionalStepBuilder<TData> CreateBuilder(string name) => new(name);

#pragma warning restore MA0018

	/// <inheritdoc />
	public async Task<bool> EvaluateConditionAsync(
		ISagaContext<TData> context,
		CancellationToken cancellationToken)
	{
		try
		{
			LogConditionEvaluationStarted(Name);

			var result = await _condition(context, cancellationToken).ConfigureAwait(false);
			LogConditionEvaluationCompleted(Name, result);

			return result;
		}
		catch (Exception ex)
		{
			LogConditionEvaluationError(Name, ex);

			throw;
		}
	}

	/// <inheritdoc />
	public async Task<StepResult> ExecuteAsync(
		ISagaContext<TData> context,
		CancellationToken cancellationToken)
	{
		LogConditionalStepExecutionStarted(Name);

		try
		{
			var conditionResult = await EvaluateConditionAsync(context, cancellationToken).ConfigureAwait(false);

			_executedStep = conditionResult ? ThenStep : ElseStep;

			if (_executedStep == null)
			{
				LogNoStepToExecute(conditionResult);

				return StepResult.Success(new Dictionary<string, object>(StringComparer.Ordinal)
				{
					["ConditionResult"] = conditionResult,
					["Branch"] = conditionResult ? "Then" : "Else",
					["Skipped"] = true,
				});
			}

			LogBranchExecution(conditionResult ? "Then" : "Else", _executedStep.Name);

			var result = await _executedStep.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);

			// Add branch information to the result
			if (result?.OutputData != null)
			{
				result.OutputData["ConditionalBranch"] = conditionResult ? "Then" : "Else";
				result.OutputData["ConditionalStepName"] = Name;
			}

			return result ?? StepResult.Failure($"Conditional step '{Name}' returned null result");
		}
		catch (Exception ex)
		{
			LogConditionalStepExecutionFailed(Name, ex);

			return StepResult.Failure($"Conditional step '{Name}' failed: {ex.Message}");
		}
	}

	/// <inheritdoc />
	public async Task<StepResult> CompensateAsync(
		ISagaContext<TData> context,
		CancellationToken cancellationToken)
	{
		LogConditionalStepCompensationStarted(Name);

		if (_executedStep == null)
		{
			LogNoStepExecutedSkippingCompensation();

			return StepResult.Success();
		}

		if (!_executedStep.CanCompensate)
		{
			LogExecutedStepCannotBeCompensated(_executedStep.Name);

			return StepResult.Success();
		}

		try
		{
			LogCompensatingExecutedStep(_executedStep.Name);

			return await _executedStep.CompensateAsync(context, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogConditionalStepCompensationFailed(Name, ex);

			return StepResult.Failure($"Compensation failed for step '{Name}': {ex.Message}");
		}
	}

	// Source-generated logging methods
	[LoggerMessage(SagaEventId.ConditionEvaluationStarted, LogLevel.Debug,
		"Evaluating condition for step '{StepName}'")]
	private partial void LogConditionEvaluationStarted(string stepName);

	[LoggerMessage(SagaEventId.ConditionEvaluationCompleted, LogLevel.Debug,
		"Condition for step '{StepName}' evaluated to: {Result}")]
	private partial void LogConditionEvaluationCompleted(string stepName, bool result);

	[LoggerMessage(SagaEventId.ConditionEvaluationError, LogLevel.Error,
		"Error evaluating condition for step '{StepName}'")]
	private partial void LogConditionEvaluationError(string stepName, Exception ex);

	[LoggerMessage(SagaEventId.ConditionalStepExecutionStarted, LogLevel.Information,
		"Executing conditional saga step '{StepName}'")]
	private partial void LogConditionalStepExecutionStarted(string stepName);

	[LoggerMessage(SagaEventId.NoStepToExecute, LogLevel.Debug,
		"No step to execute for condition result: {ConditionResult}")]
	private partial void LogNoStepToExecute(bool conditionResult);

	[LoggerMessage(SagaEventId.BranchExecution, LogLevel.Debug,
		"Executing {Branch} branch with step '{StepName}'")]
	private partial void LogBranchExecution(string branch, string stepName);

	[LoggerMessage(SagaEventId.ConditionalStepExecutionFailed, LogLevel.Error,
		"Conditional saga step '{StepName}' failed")]
	private partial void LogConditionalStepExecutionFailed(string stepName, Exception ex);

	[LoggerMessage(SagaEventId.ConditionalStepCompensationStarted, LogLevel.Information,
		"Compensating conditional saga step '{StepName}'")]
	private partial void LogConditionalStepCompensationStarted(string stepName);

	[LoggerMessage(SagaEventId.NoStepExecutedSkippingCompensation, LogLevel.Debug,
		"No step was executed, skipping compensation")]
	private partial void LogNoStepExecutedSkippingCompensation();

	[LoggerMessage(SagaEventId.ExecutedStepCannotBeCompensated, LogLevel.Warning,
		"Executed step '{StepName}' cannot be compensated")]
	private partial void LogExecutedStepCannotBeCompensated(string stepName);

	[LoggerMessage(SagaEventId.CompensatingExecutedStep, LogLevel.Debug,
		"Compensating executed step '{StepName}'")]
	private partial void LogCompensatingExecutedStep(string stepName);

	[LoggerMessage(SagaEventId.ConditionalStepCompensationFailed, LogLevel.Error,
		"Failed to compensate conditional saga step '{StepName}'")]
	private partial void LogConditionalStepCompensationFailed(string stepName, Exception ex);
}
