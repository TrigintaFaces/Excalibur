// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch;

/// <summary>
/// Provides persistent storage for outbound messages in the Transactional Outbox pattern.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Traditional (polling) outbox</strong> for relational and document databases
/// (SQL Server, Postgres, MongoDB, etc.). Messages are staged within a database
/// transaction alongside business data, then published by a background polling service
/// (<c>OutboxBackgroundService</c>).
/// </para>
/// <para>
/// For cloud-native databases that use change-feed triggers instead of polling
/// (Cosmos DB, DynamoDB, Firestore), see
/// <c>Excalibur.Data.CloudNative.ICloudNativeOutboxStore</c>.
/// The two interfaces serve fundamentally different outbox patterns and are
/// intentionally separate:
/// </para>
/// <list type="bullet">
/// <item><c>IOutboxStore</c> -- polling-based, SQL transactions, background service</item>
/// <item><c>ICloudNativeOutboxStore</c> -- change-feed triggers, partition keys, serverless</item>
/// </list>
/// <para>
/// This interface contains 5 core methods following the Microsoft IDistributedCache pattern.
/// For batch operations, implement <see cref="IOutboxStoreBatch"/>.
/// For admin/query operations, implement <see cref="IOutboxStoreAdmin"/>.
/// </para>
/// <para>
/// <strong>Optional capabilities are discovered through <see cref="IServiceProvider.GetService(Type)"/>,
/// never by casting the store.</strong> A store answers for the capability interfaces it implements and
/// returns <see langword="null"/> for the rest. A decorator answers for what it adds and defers everything
/// else to the store it wraps, so decoration cannot silently drop a capability the underlying store has.
/// Casting (<c>store is IOutboxStoreAdmin</c>) sees only the outermost type and is therefore lossy through
/// any decorator; it must not be used.
/// </para>
/// <para>
/// Interface uses ValueTask for synchronous completion optimization.
/// In-memory implementations complete synchronously without allocation overhead.
/// </para>
/// </remarks>
public interface IOutboxStore : IServiceProvider
{
	/// <summary>
	/// Resolves an optional outbox capability, or <see langword="null"/> when it is unavailable.
	/// </summary>
	/// <param name="serviceType"> The capability interface to resolve, for example <see cref="IOutboxStoreAdmin"/>. </param>
	/// <returns>
	/// An instance assignable to <paramref name="serviceType"/> when this store provides the capability;
	/// otherwise <see langword="null"/>.
	/// </returns>
	/// <remarks>
	/// The default implementation answers for any capability this instance itself implements. Leaf stores
	/// need not override it. Decorators must override it to defer unknown capabilities to the store they
	/// wrap; deriving from <c>OutboxStoreDecorator</c> does so automatically.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="serviceType"/> is null. </exception>
	object? IServiceProvider.GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		return serviceType.IsInstanceOfType(this) ? this : null;
	}

	/// <summary>
	/// Stages a message in the outbox for later delivery.
	/// </summary>
	/// <param name="message"> The outbound message to stage. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous stage operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when message is null. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when a message with the same ID already exists. </exception>
	ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken);

	/// <summary>
	/// Enqueues a message in the outbox for later delivery with context.
	/// </summary>
	/// <param name="message"> The message to enqueue. </param>
	/// <param name="context"> The message context. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous enqueue operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when message or context is null. </exception>
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves unsent messages from the outbox for publishing.
	/// </summary>
	/// <param name="batchSize"> Maximum number of messages to retrieve. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> Collection of unsent messages ready for delivery. </returns>
	/// <remarks>
	/// This claim is unfenced. A store that can enforce leadership fencing implements
	/// <see cref="IFencedOutboxStore"/>, whose overloads take the leadership token; a store that
	/// cannot is never handed one.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when batchSize is less than 1. </exception>
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(
		int batchSize,
		CancellationToken cancellationToken);

	/// <summary>
	/// Marks a message as successfully sent.
	/// </summary>
	/// <param name="messageId"> The unique identifier of the message to mark as sent. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous mark-sent operation. </returns>
	/// <remarks>
	/// This mutation is unfenced. A store that can enforce leadership fencing implements
	/// <see cref="IFencedOutboxStore"/>.
	/// </remarks>
	/// <exception cref="ArgumentException"> Thrown when messageId is null or empty. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when the message does not exist or is already marked as sent. </exception>
	ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken);

	/// <summary>
	/// Marks a message as failed during delivery.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A failure is a delay, not an ending. The message stays owed delivery, and three conditions hold
	/// together to keep that true without turning it into a retry loop. Each is stated because a store can
	/// satisfy the other two and still be wrong.
	/// </para>
	/// <para>
	/// <b>The message is withheld, then returned.</b> It does not become claimable again immediately -- an
	/// immediate re-claim is a zero-backoff loop that saturates the transport against a destination that is
	/// failing anyway -- and it does become claimable once the store's failure-backoff floor has elapsed.
	/// The floor is measured from the recorded failure, not from a claim lease: a message that failed
	/// without ever being claimed has no lease, and a floor derived from one would yield nothing for it.
	/// Withholding it permanently satisfies the first half and silently drops the message, so both halves
	/// are required.
	/// </para>
	/// <para>
	/// <b>Only the claim's owner may report against it.</b> A report from a dispatcher that no longer holds
	/// the claim is a no-op rather than an error: it is stale, not invalid. Honouring it would release a
	/// claim its successor is still delivering under, and both would then send the same message. A message
	/// that was never claimed has no owner and is reported freely. The guard must not be satisfied by
	/// refusing everybody -- the owner's own report still has to land, or the store cannot record failures
	/// at all.
	/// </para>
	/// <para>
	/// <b>The recorded attempt count never decreases.</b> Implementations record the greater of the stored
	/// count and <paramref name="retryCount"/>. The ceiling that eventually gives up on a message is driven
	/// by that count, so a late report carrying a lower number would push the ceiling further away each time
	/// one arrived, and the message would be retried without end.
	/// </para>
	/// <para>
	/// Marking a message that does not exist is a silent no-op. A message that has already been delivered is
	/// never reopened by a late failure report.
	/// </para>
	/// </remarks>
	/// <param name="messageId"> The unique identifier of the message that failed. </param>
	/// <param name="errorMessage"> The error description or exception message. </param>
	/// <param name="retryCount"> The current retry attempt count. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous mark-failed operation. </returns>
	/// <exception cref="ArgumentException"> Thrown when messageId is null or empty. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when errorMessage is null. </exception>
	ValueTask MarkFailedAsync(
		string messageId,
		string errorMessage,
		int retryCount,
		CancellationToken cancellationToken);

}
