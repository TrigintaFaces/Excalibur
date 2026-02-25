// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Represents a conditional saga step that determines execution path based on conditions.
/// </summary>
/// <typeparam name="TData"> The type of data for the saga. </typeparam>
public interface IConditionalSagaStep<TData> : ISagaStep<TData>
	where TData : class
{
	/// <summary>
	/// Gets the step to execute when the condition is true.
	/// </summary>
	/// <value>the step to execute when the condition is true., or <see langword="null"/> if not specified.</value>
	ISagaStep<TData>? ThenStep { get; }

	/// <summary>
	/// Gets the step to execute when the condition is false.
	/// </summary>
	/// <value>the step to execute when the condition is false., or <see langword="null"/> if not specified.</value>
	ISagaStep<TData>? ElseStep { get; }

	/// <summary>
	/// Gets the branching strategy for this conditional step.
	/// </summary>
	/// <value>the branching strategy for this conditional step.</value>
	BranchingStrategy Strategy { get; }

	/// <summary>
	/// Evaluates the condition to determine which branch to execute.
	/// </summary>
	/// <param name="context"> The saga execution context. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the condition is met, false otherwise. </returns>
	Task<bool> EvaluateConditionAsync(
		ISagaContext<TData> context,
		CancellationToken cancellationToken);
}

