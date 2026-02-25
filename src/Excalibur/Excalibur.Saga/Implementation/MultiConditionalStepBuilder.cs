// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Models;

namespace Excalibur.Saga.Implementation;

/// <summary>
/// Builder for creating multi-conditional saga steps.
/// </summary>
/// <typeparam name="TData"> The type of data for the saga. </typeparam>
/// <remarks> Initializes a new instance of the <see cref="MultiConditionalStepBuilder{TData}" /> class. </remarks>
/// <param name="name"> The name of the step. </param>
public sealed class MultiConditionalStepBuilder<TData>(string name)
	where TData : class
{
	private readonly string _name = name ?? throw new ArgumentNullException(nameof(name));
	private readonly Dictionary<string, ISagaStep<TData>> _branches = [];
	private Func<ISagaContext<TData>, CancellationToken, Task<string>>? _branchEvaluator;
	private ISagaStep<TData>? _defaultStep;
	private TimeSpan _timeout = TimeSpan.FromMinutes(5);
	private RetryPolicy? _retryPolicy;

	/// <summary>
	/// Sets the branch evaluator function.
	/// </summary>
	/// <param name="evaluator"> The branch evaluation function. </param>
	/// <returns> The builder instance. </returns>
	/// <exception cref="ArgumentNullException">Thrown when the evaluator is null.</exception>
	public MultiConditionalStepBuilder<TData> EvaluateWith(
		Func<ISagaContext<TData>, CancellationToken, Task<string>> evaluator)
	{
		_branchEvaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
		return this;
	}

	/// <summary>
	/// Sets the branch evaluator using a synchronous function.
	/// </summary>
	/// <param name="evaluator"> The branch evaluation function. </param>
	/// <returns> The builder instance. </returns>
	public MultiConditionalStepBuilder<TData> EvaluateWith(
		Func<ISagaContext<TData>, string> evaluator)
	{
		ArgumentNullException.ThrowIfNull(evaluator);

		_branchEvaluator = (context, _) => Task.FromResult(evaluator(context));
		return this;
	}

	/// <summary>
	/// Adds a branch to the multi-conditional step.
	/// </summary>
	/// <param name="branchKey"> The branch identifier. </param>
	/// <param name="step"> The step to execute for this branch. </param>
	/// <returns> The builder instance. </returns>
	/// <exception cref="ArgumentException">Thrown when the branch key is null or whitespace.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the step is null.</exception>
	public MultiConditionalStepBuilder<TData> AddBranch(string branchKey, ISagaStep<TData> step)
	{
		if (string.IsNullOrWhiteSpace(branchKey))
		{
			throw new ArgumentException(
				Resources.MultiConditionalStepBuilder_BranchKeyCannotBeNullOrWhitespace,
				nameof(branchKey));
		}

		_branches[branchKey] = step ?? throw new ArgumentNullException(nameof(step));
		return this;
	}

	/// <summary>
	/// Adds multiple branches to the multi-conditional step.
	/// </summary>
	/// <param name="branches"> The branches to add. </param>
	/// <returns> The builder instance. </returns>
	public MultiConditionalStepBuilder<TData> AddBranches(IDictionary<string, ISagaStep<TData>> branches)
	{
		ArgumentNullException.ThrowIfNull(branches);

		foreach (var kvp in branches)
		{
			_ = AddBranch(kvp.Key, kvp.Value);
		}

		return this;
	}

	/// <summary>
	/// Sets the default step to execute if no branch matches.
	/// </summary>
	/// <param name="step"> The default step. </param>
	/// <returns> The builder instance. </returns>
	/// <exception cref="ArgumentNullException">Thrown when the step is null.</exception>
	public MultiConditionalStepBuilder<TData> WithDefault(ISagaStep<TData> step)
	{
		_defaultStep = step ?? throw new ArgumentNullException(nameof(step));
		return this;
	}

	/// <summary>
	/// Sets the timeout for the multi-conditional step.
	/// </summary>
	/// <param name="timeout"> The timeout duration. </param>
	/// <returns> The builder instance. </returns>
	public MultiConditionalStepBuilder<TData> WithTimeout(TimeSpan timeout)
	{
		_timeout = timeout;
		return this;
	}

	/// <summary>
	/// Sets the retry policy for the multi-conditional step.
	/// </summary>
	/// <param name="retryPolicy"> The retry policy. </param>
	/// <returns> The builder instance. </returns>
	public MultiConditionalStepBuilder<TData> WithRetryPolicy(RetryPolicy retryPolicy)
	{
		_retryPolicy = retryPolicy;
		return this;
	}

	/// <summary>
	/// Builds the multi-conditional saga step.
	/// </summary>
	/// <returns> The configured multi-conditional saga step. </returns>
	/// <exception cref="InvalidOperationException">Thrown when the branch evaluator has not been specified.</exception>
	public MultiConditionalSagaStep<TData> Build()
	{
		if (_branchEvaluator == null)
		{
			throw new InvalidOperationException(
				Resources.MultiConditionalStepBuilder_BranchEvaluatorMustBeSpecified);
		}

		if (_branches.Count == 0)
		{
			throw new InvalidOperationException(
				Resources.MultiConditionalStepBuilder_AtLeastOneBranchMustBeSpecified);
		}

		return new MultiConditionalSagaStep<TData>(_name, _branchEvaluator, _branches, _defaultStep)
		{
			Timeout = _timeout,
			RetryPolicy = _retryPolicy,
		};
	}
}
