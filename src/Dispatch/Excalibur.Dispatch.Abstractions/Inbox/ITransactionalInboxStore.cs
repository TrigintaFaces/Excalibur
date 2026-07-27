// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

namespace Excalibur.Dispatch;

/// <summary>
/// An optional capability for <see cref="IInboxStore"/> implementations backed by a store that supports an
/// enlisting transaction, enabling <b>exactly-once</b> processing: the handler runs and the message is marked
/// processed inside a <b>single</b> transaction that commits or rolls back atomically.
/// </summary>
/// <remarks>
/// <para>
/// This is a segregated capability interface (composition, NOT inheritance of <see cref="IInboxStore"/>) so
/// <see cref="IInboxStore"/> stays within the Interface Segregation threshold. Only stores whose backing
/// engine can enlist the handler's own writes and the processed-mark in one transaction (for example
/// SqlServer and Postgres) implement it. Stores that cannot fall back to the atomic claim protocol
/// (<see cref="IClaimableInboxStore"/>), which is <b>at-least-once</b> (handler and mark are separate
/// operations, so a crash between them redelivers) — a documented, structurally-guarded degradation, never a
/// silent one. Presence is reported through <see cref="IInboxStoreCapabilities.SupportsTransactional"/> so a
/// <c>ValidateOnStart</c> guard can fail fast rather than throw <see cref="System.NotSupportedException"/> at
/// runtime.
/// </para>
/// <para>
/// The exactly-once guarantee holds only on the success path: if the handler throws, the whole
/// transaction rolls back — nothing is marked processed and the message is redelivered for retry, so a
/// handler failure never silently drops a message and never marks a message whose handler did not commit.
/// </para>
/// </remarks>
public interface ITransactionalInboxStore
{
	/// <summary>
	/// Atomically, within a single enlisting transaction, checks the message has not already been processed,
	/// runs <paramref name="handler"/>, and marks the message processed — committing the handler's writes and
	/// the processed-mark together, or rolling both back on failure.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message.</param>
	/// <param name="handlerType">The fully qualified type name of the handler processing the message.</param>
	/// <param name="handler">
	/// The processing delegate, invoked exactly once inside the transaction if and only if the message was not
	/// already processed. It receives the store's active <see cref="IDbTransaction"/>: any data writes it performs
	/// on <see cref="IDbTransaction.Connection"/> enlisted in that transaction (for example, Dapper commands
	/// passing <c>transaction: tx</c>) commit atomically with the processed-mark. Writes made on a <b>different</b>
	/// connection are <b>not</b> enlisted and are therefore not atomic with the mark.
	/// </param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// <see langword="true"/> if the caller processed the message (handler ran and the processed-mark committed);
	/// <see langword="false"/> if the message was already processed (a duplicate — <paramref name="handler"/> is
	/// not invoked).
	/// </returns>
	/// <remarks>
	/// The atomic mark+handler commit uses a <b>single local database transaction</b> (one connection) — never an
	/// ambient/distributed transaction (no <c>TransactionScope</c>/MSDTC), so it is portable across hosts and does
	/// not require a distributed transaction coordinator. The seam takes the BCL <see cref="IDbTransaction"/> so
	/// the messaging-layer abstraction stays free of any data-access-layer dependency.
	/// </remarks>
	/// <exception cref="System.ArgumentException">Thrown when <paramref name="messageId"/> or <paramref name="handlerType"/> is null or empty.</exception>
	ValueTask<bool> TryProcessTransactionallyAsync(
		string messageId,
		string handlerType,
		Func<IDbTransaction, CancellationToken, ValueTask> handler,
		CancellationToken cancellationToken);
}
