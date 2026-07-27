// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// An optional capability for <see cref="IInboxStore"/> implementations backed by a document store whose
/// native transaction cannot be expressed as a BCL <see cref="System.Data.IDbTransaction"/>, enabling
/// <b>exactly-once</b> processing: the handler runs and the message is marked processed inside a single
/// provider-native transaction that commits or rolls back atomically.
/// </summary>
/// <remarks>
/// <para>
/// This is the document-store analogue of <see cref="ITransactionalInboxStore"/>. Where the relational seam
/// hands the handler a BCL <see cref="System.Data.IDbTransaction"/>, this seam hands it an opaque
/// <see cref="IInboxTransactionScope"/>; a handler that needs to enlist its own writes casts the scope to the
/// provider-native handle through a provider-specific extension (for example <c>scope.AsMongoSession()</c>).
/// </para>
/// <para>
/// Presence is reported through <see cref="IInboxStoreCapabilities.SupportsTransactional"/> so a
/// <c>ValidateOnStart</c> guard can fail fast rather than throw at runtime. A store that cannot honour the
/// atomic contract (for example a Cosmos DB store configured without a shared partition key, whose
/// <c>TransactionalBatch</c> is single-partition) MUST report <see langword="false"/> and fall back to the
/// at-least-once idempotent claim protocol (<see cref="IClaimableInboxStore"/>) — a documented,
/// structurally-guarded degradation, never a false atomic advertisement.
/// </para>
/// <para>
/// The exactly-once guarantee holds only on the success path: if the handler throws, the whole native
/// transaction rolls back — nothing is marked processed and the message is redelivered for retry.
/// </para>
/// </remarks>
public interface IScopedTransactionalInboxStore
{
	/// <summary>
	/// Atomically, within a single provider-native transaction, checks the message has not already been
	/// processed, runs <paramref name="handler"/>, and marks the message processed — committing the handler's
	/// enlisted writes and the processed-mark together, or rolling both back on failure.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message.</param>
	/// <param name="handlerType">The fully qualified type name of the handler processing the message.</param>
	/// <param name="handler">
	/// The processing delegate, invoked exactly once inside the native transaction if and only if the message
	/// was not already processed. It receives an opaque <see cref="IInboxTransactionScope"/>; writes it performs
	/// through the provider-native handle obtained from that scope commit atomically with the processed-mark.
	/// Writes made outside the scope are not enlisted and are therefore not atomic with the mark.
	/// </param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// <see langword="true"/> if the caller processed the message (handler ran and the processed-mark
	/// committed); <see langword="false"/> if the message was already processed (a duplicate —
	/// <paramref name="handler"/> is not invoked).
	/// </returns>
	/// <exception cref="System.ArgumentException">Thrown when <paramref name="messageId"/> or <paramref name="handlerType"/> is null or empty.</exception>
	ValueTask<bool> TryProcessTransactionallyAsync(
		string messageId,
		string handlerType,
		Func<IInboxTransactionScope, CancellationToken, ValueTask> handler,
		CancellationToken cancellationToken);
}
