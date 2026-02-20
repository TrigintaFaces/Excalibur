// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Manages transactional operations for Azure Service Bus.
/// </summary>
/// <remarks>
/// <para>
/// Provides transaction support for Azure Service Bus send operations using
/// <c>ServiceBusTransactedBatch</c> semantics. Messages sent within a transaction
/// are either all committed or all rolled back atomically.
/// </para>
/// <para>
/// This follows the Microsoft pattern from <c>Azure.Messaging.ServiceBus</c>,
/// specifically the <c>ServiceBusSender.CreateTransactionScope</c> pattern
/// with explicit begin/commit/rollback lifecycle.
/// </para>
/// <para>
/// Usage pattern:
/// <list type="number">
///   <item><description>Call <see cref="BeginTransactionAsync"/> to start a new transaction.</description></item>
///   <item><description>Perform send operations using the returned transaction context.</description></item>
///   <item><description>Call <see cref="CommitAsync"/> to commit all operations, or <see cref="RollbackAsync"/> to discard them.</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// await transaction.BeginTransactionAsync(cancellationToken);
/// try
/// {
///     // Send messages within the transaction scope
///     await transaction.CommitAsync(cancellationToken);
/// }
/// catch
/// {
///     await transaction.RollbackAsync(cancellationToken);
///     throw;
/// }
/// </code>
/// </example>
public interface IAzureServiceBusTransaction : IAsyncDisposable
{
	/// <summary>
	/// Begins a new Service Bus transaction.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> representing the asynchronous begin operation.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when a transaction is already in progress.
	/// </exception>
	Task BeginTransactionAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Commits all operations performed within the current transaction.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> representing the asynchronous commit operation.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when no transaction is in progress.
	/// </exception>
	Task CommitAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Rolls back all operations performed within the current transaction.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> representing the asynchronous rollback operation.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when no transaction is in progress.
	/// </exception>
	Task RollbackAsync(CancellationToken cancellationToken);
}
