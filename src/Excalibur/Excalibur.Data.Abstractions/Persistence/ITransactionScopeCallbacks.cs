// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Provides lifecycle callback registration for transaction scopes.
/// </summary>
/// <remarks>
/// <para>
/// This is an ISP sub-interface of <see cref="ITransactionScope"/>. Consumers that need
/// to register callbacks on commit, rollback, or completion can check for this interface
/// via pattern matching: <c>if (scope is ITransactionScopeCallbacks callbacks) { ... }</c>
/// </para>
/// </remarks>
public interface ITransactionScopeCallbacks
{
	/// <summary>
	/// Registers a callback to be executed after the transaction commits.
	/// </summary>
	/// <param name="callback"> The callback to execute. </param>
	void OnCommit(Func<Task> callback);

	/// <summary>
	/// Registers a callback to be executed after the transaction rolls back.
	/// </summary>
	/// <param name="callback"> The callback to execute. </param>
	void OnRollback(Func<Task> callback);

	/// <summary>
	/// Registers a callback to be executed when the transaction completes (commit or rollback).
	/// </summary>
	/// <param name="callback"> The callback to execute. </param>
	void OnComplete(Func<TransactionStatus, Task> callback);
}
