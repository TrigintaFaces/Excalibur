// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Represents a transaction that can be committed or rolled back.
/// </summary>
public interface ITransaction : IDisposable
{
	/// <summary>
	/// Gets the transaction identifier.
	/// </summary>
	/// <value>
	/// The transaction identifier.
	/// </value>
	string Id { get; }

	/// <summary>
	/// Commits the transaction.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task representing the commit operation. </returns>
	Task CommitAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Rolls back the transaction.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task representing the rollback operation. </returns>
	Task RollbackAsync(CancellationToken cancellationToken);
}
