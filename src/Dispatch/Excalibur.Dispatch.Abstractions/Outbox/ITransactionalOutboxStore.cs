// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Extends <see cref="IOutboxStore"/> with transactional capabilities for exactly-once delivery.
/// </summary>
/// <remarks>
/// <para>
/// This interface enables the <see cref="Options.Delivery.OutboxDeliveryGuarantee.TransactionalWhenApplicable"/>
/// delivery guarantee level by providing a mechanism to atomically dispatch a message and mark it as sent
/// within a single database transaction.
/// </para>
/// <para>
/// Implementations should return <see langword="true"/> for <see cref="SupportsTransactions"/>
/// only when they can guarantee atomic dispatch-and-mark operations. In-memory stores and stores
/// that use different databases for persistence and transport should return <see langword="false"/>.
/// </para>
/// </remarks>
public interface ITransactionalOutboxStore : IOutboxStore
{
	/// <summary>
	/// Gets a value indicating whether the store supports transactional dispatch operations.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if the store can atomically dispatch and mark messages within a
	/// single transaction; otherwise, <see langword="false"/>.
	/// </value>
	/// <remarks>
	/// <para>
	/// When this property returns <see langword="false"/>, the outbox processor will fall back
	/// to <see cref="Options.Delivery.OutboxDeliveryGuarantee.MinimizedWindow"/> behavior.
	/// </para>
	/// </remarks>
	bool SupportsTransactions { get; }

	/// <summary>
	/// Marks multiple messages as sent within a single transaction.
	/// </summary>
	/// <param name="messageIds">The unique identifiers of the messages to mark as sent.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous transactional mark-sent operation.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="messageIds"/> is null.</exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <see cref="SupportsTransactions"/> is <see langword="false"/>
	/// or when any message does not exist.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This method ensures that all messages are marked as sent atomically. If any message
	/// fails to be marked, the entire operation is rolled back.
	/// </para>
	/// </remarks>
	Task MarkSentTransactionalAsync(IReadOnlyList<string> messageIds, CancellationToken cancellationToken);
}
