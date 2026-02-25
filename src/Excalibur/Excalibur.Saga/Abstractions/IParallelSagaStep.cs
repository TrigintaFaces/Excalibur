// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Represents a parallel saga step that executes multiple child steps concurrently.
/// </summary>
/// <typeparam name="TData"> The type of data for the saga. </typeparam>
public interface IParallelSagaStep<TData> : ISagaStep<TData>
	where TData : class
{
	/// <summary>
	/// Gets the collection of child steps to execute in parallel.
	/// </summary>
	/// <value>the collection of child steps to execute in parallel.</value>
	IReadOnlyList<ISagaStep<TData>> ParallelSteps { get; }

	/// <summary>
	/// Gets the parallelism strategy for this step.
	/// </summary>
	/// <value>the parallelism strategy for this step.</value>
	ParallelismStrategy Strategy { get; }

	/// <summary>
	/// Gets the maximum degree of parallelism.
	/// </summary>
	/// <value>the maximum degree of parallelism.</value>
	int MaxDegreeOfParallelism { get; }

	/// <summary>
	/// Gets a value indicating whether all parallel steps must succeed for the step to succeed.
	/// </summary>
	/// <value><see langword="true"/> if whether all parallel steps must succeed for the step to succeed.; otherwise, <see langword="false"/>.</value>
	bool RequireAllSuccess { get; }

	/// <summary>
	/// Gets a value indicating whether to continue executing remaining steps if one fails.
	/// </summary>
	/// <value><see langword="true"/> if whether to continue executing remaining steps if one fails.; otherwise, <see langword="false"/>.</value>
	bool ContinueOnFailure { get; }

	/// <summary>
	/// Aggregates the results from parallel step executions.
	/// </summary>
	/// <param name="results"> The individual step results. </param>
	/// <returns> The aggregated result. </returns>
	StepResult AggregateResults(IReadOnlyList<StepResult> results);
}

