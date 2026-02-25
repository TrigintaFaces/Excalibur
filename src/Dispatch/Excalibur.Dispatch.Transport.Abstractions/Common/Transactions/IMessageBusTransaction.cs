// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Represents a message bus transaction.
/// </summary>
public interface IMessageBusTransaction : IAsyncDisposable, IDisposable
{
	/// <summary>
	/// Gets the transaction ID.
	/// </summary>
	/// <value>
	/// The transaction ID
	/// </value>
	string TransactionId { get; }

	/// <summary>
	/// Gets a value indicating whether the transaction is active.
	/// </summary>
	/// <value>
	/// A value indicating whether the transaction is active
	/// </value>
	bool IsActive { get; }

	/// <summary>
	/// Commits the transaction.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task CommitAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Rolls back the transaction.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task RollbackAsync(CancellationToken cancellationToken);
}
