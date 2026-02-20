// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Execution;

/// <summary>
/// Defines a provider-neutral contract for executing non-query commands against a data store.
/// Implementations must be async, cancellation-aware, and free of provider-specific types.
/// </summary>
public interface IDataExecutor
{
	/// <summary>
	/// Executes a non-query command asynchronously and returns the number of affected rows.
	/// </summary>
	/// <param name="commandText">The command text to execute (e.g., SQL, CQL, or provider-specific DSL as agreed by the implementation).</param>
	/// <param name="parameters">Optional named parameters for the command.</param>
	/// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
	/// <returns>The number of affected rows.</returns>
	Task<int> ExecuteAsync(
		string commandText,
		IReadOnlyDictionary<string, object?>? parameters,
		CancellationToken cancellationToken);
}
