// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Represents a multi-way conditional saga step that can branch to multiple paths.
/// </summary>
/// <typeparam name="TData"> The type of data for the saga. </typeparam>
public interface IMultiConditionalSagaStep<TData> : ISagaStep<TData>
	where TData : class
{
	/// <summary>
	/// Gets the collection of branch steps keyed by branch identifier.
	/// </summary>
	/// <value>the collection of branch steps keyed by branch identifier.</value>
	IReadOnlyDictionary<string, ISagaStep<TData>> Branches { get; }

	/// <summary>
	/// Gets the default step to execute if no branch matches.
	/// </summary>
	/// <value>the default step to execute if no branch matches., or <see langword="null"/> if not specified.</value>
	ISagaStep<TData>? DefaultStep { get; }

	/// <summary>
	/// Evaluates conditions to determine which branch to execute.
	/// </summary>
	/// <param name="context"> The saga execution context. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The key of the branch to execute. </returns>
	Task<string> EvaluateBranchAsync(
		ISagaContext<TData> context,
		CancellationToken cancellationToken);
}

