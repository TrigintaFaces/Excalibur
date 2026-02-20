// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Provides advanced transaction scope capabilities including savepoints and nested scopes.
/// </summary>
/// <remarks>
/// <para>
/// This is an ISP sub-interface of <see cref="ITransactionScope"/>. Not all providers support
/// savepoints or nested transactions. Consumers should check for this interface via pattern
/// matching: <c>if (scope is ITransactionScopeAdvanced advanced) { ... }</c>
/// </para>
/// <para>
/// Providers that do not support these capabilities (e.g., InMemory, Redis, MongoDB) should
/// not implement this interface rather than throwing <see cref="NotSupportedException"/>.
/// </para>
/// </remarks>
public interface ITransactionScopeAdvanced
{
	/// <summary>
	/// Creates a savepoint in the transaction.
	/// </summary>
	/// <param name="savepointName"> The name of the savepoint. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task CreateSavepointAsync(string savepointName, CancellationToken cancellationToken);

	/// <summary>
	/// Rolls back to a specific savepoint.
	/// </summary>
	/// <param name="savepointName"> The name of the savepoint. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task RollbackToSavepointAsync(string savepointName, CancellationToken cancellationToken);

	/// <summary>
	/// Releases a savepoint.
	/// </summary>
	/// <param name="savepointName"> The name of the savepoint. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task ReleaseSavepointAsync(string savepointName, CancellationToken cancellationToken);

	/// <summary>
	/// Creates a nested transaction scope.
	/// </summary>
	/// <param name="isolationLevel"> The isolation level for the nested scope. </param>
	/// <returns> The nested transaction scope. </returns>
	ITransactionScope CreateNestedScope(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);
}
