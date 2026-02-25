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
/// Implements a multi-way conditional saga step that can branch to multiple paths.
/// </summary>
/// <typeparam name="TData"> The type of data for the saga. </typeparam>
public sealed partial class MultiConditionalSagaStep<TData> : IMultiConditionalSagaStep<TData>
	where TData : class
{
	private readonly string _name;
	private readonly Func<ISagaContext<TData>, CancellationToken, Task<string>> _branchEvaluator;
	private readonly Dictionary<string, ISagaStep<TData>> _branches;
	private readonly ISagaStep<TData>? _defaultStep;
	private readonly ILogger<MultiConditionalSagaStep<TData>> _logger;
	private ISagaStep<TData>? _executedStep;
	private string? _executedBranch;

	/// <summary>
	/// Initializes a new instance of the <see cref="MultiConditionalSagaStep{TData}" /> class.
	/// </summary>
	/// <param name="name"> The name of the step. </param>
	/// <param name="branchEvaluator"> The function to evaluate which branch to take. </param>
	/// <param name="branches"> The collection of branch steps. </param>
	/// <param name="defaultStep"> The default step if no branch matches. </param>
	/// <param name="logger"> The logger. </param>
	public MultiConditionalSagaStep(
		string name,
		Func<ISagaContext<TData>, CancellationToken, Task<string>> branchEvaluator,
		IDictionary<string, ISagaStep<TData>> branches,
		ISagaStep<TData>? defaultStep = null,
		ILogger<MultiConditionalSagaStep<TData>>? logger = null)
	{
		_name = name ?? throw new ArgumentNullException(nameof(name));
		_branchEvaluator = branchEvaluator ?? throw new ArgumentNullException(nameof(branchEvaluator));
		_branches = new(branches ?? throw new ArgumentNullException(nameof(branches)), StringComparer.Ordinal);
		_defaultStep = defaultStep;
		_logger = logger ?? NullLogger<MultiConditionalSagaStep<TData>>.Instance;
	}

	/// <inheritdoc />
	public string Name => _name;

	/// <inheritdoc />
	public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <inheritdoc />
	public RetryPolicy? RetryPolicy { get; set; }

	/// <inheritdoc />
	public bool CanCompensate
	{
		get
		{
			foreach (var branch in _branches.Values)
			{
				if (!branch.CanCompensate)
				{
					return false;
				}
			}

			return DefaultStep?.CanCompensate ?? true;
		}
	}

	/// <inheritdoc />
	public IReadOnlyDictionary<string, ISagaStep<TData>> Branches => _branches;

	/// <inheritdoc />
	public ISagaStep<TData>? DefaultStep => _defaultStep;

	/// <summary>
	/// Creates a builder for constructing multi-conditional saga steps.
	/// </summary>
	/// <param name="name"> The name of the step. </param>
	/// <returns> A new builder instance. </returns>
	// R0.8: Deprecated analyzer - migrate to CA1000 when addressing generic static member pattern
#pragma warning disable MA0018
	public static MultiConditionalStepBuilder<TData> CreateBuilder(string name) => new(name);

#pragma warning restore MA0018

	/// <inheritdoc />
	public async Task<string> EvaluateBranchAsync(
		ISagaContext<TData> context,
		CancellationToken cancellationToken)
	{
		try
		{
			LogEvaluatingBranch(Name);
			var branch = await _branchEvaluator(context, cancellationToken).ConfigureAwait(false);
			LogBranchEvaluated(Name, branch);
			return branch;
		}
		catch (Exception ex)
		{
			LogBranchEvaluationError(ex, Name);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task<StepResult> ExecuteAsync(
		ISagaContext<TData> context,
		CancellationToken cancellationToken)
	{
		LogExecutingStep(Name, _branches.Count);

		try
		{
			_executedBranch = await EvaluateBranchAsync(context, cancellationToken).ConfigureAwait(false);

			if (_branches.TryGetValue(_executedBranch, out var branchStep))
			{
				_executedStep = branchStep;
				LogExecutingBranch(_executedBranch, branchStep.Name);
			}
			else if (DefaultStep != null)
			{
				_executedStep = DefaultStep;
				LogExecutingDefaultStep(_executedBranch, DefaultStep.Name);
				_executedBranch = "default";
			}
			else
			{
				LogNoBranchFound(_executedBranch);
				return StepResult.Success(new Dictionary<string, object>(StringComparer.Ordinal)
				{
					["EvaluatedBranch"] = _executedBranch,
					["BranchFound"] = false,
					["Skipped"] = true,
				});
			}

			var result = await _executedStep.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);

			// Add branch information to the result
			if (result?.OutputData != null)
			{
				result.OutputData["ExecutedBranch"] = _executedBranch;
				result.OutputData["MultiConditionalStepName"] = Name;
			}

			return result ?? StepResult.Failure($"Multi-conditional step '{Name}' returned null result");
		}
		catch (Exception ex)
		{
			LogStepFailed(ex, Name);
			return StepResult.Failure($"Multi-conditional step '{Name}' failed: {ex.Message}");
		}
	}

	/// <inheritdoc />
	public async Task<StepResult> CompensateAsync(
		ISagaContext<TData> context,
		CancellationToken cancellationToken)
	{
		LogCompensatingStep(Name);

		if (_executedStep == null)
		{
			LogSkippingCompensation();
			return StepResult.Success();
		}

		if (!_executedStep.CanCompensate)
		{
			LogStepCannotCompensate(_executedStep.Name, _executedBranch);
			return StepResult.Success();
		}

		try
		{
			LogCompensatingExecutedStep(_executedStep.Name, _executedBranch);
			return await _executedStep.CompensateAsync(context, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogCompensationFailed(ex, Name);
			return StepResult.Failure($"Compensation failed for step '{Name}': {ex.Message}");
		}
	}

	[LoggerMessage(SagaEventId.MultiCondEvaluatingBranch, LogLevel.Debug, "Evaluating branch for multi-conditional step '{StepName}'")]
	private partial void LogEvaluatingBranch(string stepName);

	[LoggerMessage(SagaEventId.MultiCondBranchEvaluated, LogLevel.Debug,
		"Multi-conditional step '{StepName}' evaluated to branch: {Branch}")]
	private partial void LogBranchEvaluated(string stepName, string branch);

	[LoggerMessage(SagaEventId.MultiCondBranchEvaluationError, LogLevel.Error,
		"Error evaluating branch for multi-conditional step '{StepName}'")]
	private partial void LogBranchEvaluationError(Exception ex, string stepName);

	[LoggerMessage(SagaEventId.MultiCondExecutingStep, LogLevel.Information,
		"Executing multi-conditional saga step '{StepName}' with {BranchCount} branches")]
	private partial void LogExecutingStep(string stepName, int branchCount);

	[LoggerMessage(SagaEventId.MultiCondExecutingBranch, LogLevel.Debug, "Executing branch '{Branch}' with step '{StepName}'")]
	private partial void LogExecutingBranch(string branch, string stepName);

	[LoggerMessage(SagaEventId.MultiCondExecutingDefaultStep, LogLevel.Debug,
		"Branch '{Branch}' not found, executing default step '{StepName}'")]
	private partial void LogExecutingDefaultStep(string branch, string stepName);

	[LoggerMessage(SagaEventId.MultiCondNoBranchFound, LogLevel.Warning, "No branch found for '{Branch}' and no default step configured")]
	private partial void LogNoBranchFound(string branch);

	[LoggerMessage(SagaEventId.MultiCondStepFailed, LogLevel.Error, "Multi-conditional saga step '{StepName}' failed")]
	private partial void LogStepFailed(Exception ex, string stepName);

	[LoggerMessage(SagaEventId.MultiCondCompensatingStep, LogLevel.Information, "Compensating multi-conditional saga step '{StepName}'")]
	private partial void LogCompensatingStep(string stepName);

	[LoggerMessage(SagaEventId.MultiCondSkippingCompensation, LogLevel.Debug, "No step was executed, skipping compensation")]
	private partial void LogSkippingCompensation();

	[LoggerMessage(SagaEventId.MultiCondStepCannotCompensate, LogLevel.Warning,
		"Executed step '{StepName}' in branch '{Branch}' cannot be compensated")]
	private partial void LogStepCannotCompensate(string stepName, string? branch);

	[LoggerMessage(SagaEventId.MultiCondCompensatingExecutedStep, LogLevel.Debug,
		"Compensating executed step '{StepName}' from branch '{Branch}'")]
	private partial void LogCompensatingExecutedStep(string stepName, string? branch);

	[LoggerMessage(SagaEventId.MultiCondCompensationFailed, LogLevel.Error,
		"Failed to compensate multi-conditional saga step '{StepName}'")]
	private partial void LogCompensationFailed(Exception ex, string stepName);
}
