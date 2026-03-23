// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Outbox;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Outbox;

/// <summary>
/// Writes directly to <see cref="IOutboxStore"/> within the ambient transaction.
/// </summary>
/// <remarks>
/// This <see cref="IOutboxWriter"/> implementation is used in
/// <see cref="OutboxConsistencyMode.Transactional"/> mode. Messages are written
/// to the outbox store within the same transaction as the business state change,
/// guaranteeing atomicity. Delegates to <see cref="IOutboxStore.EnqueueAsync"/>
/// to reuse existing serialization and header construction logic.
/// </remarks>
internal sealed class TransactionalOutboxWriter(
	IOutboxStore outboxStore,
	IMessageContextAccessor contextAccessor) : IOutboxWriter
{
	/// <inheritdoc />
	public async ValueTask WriteAsync(
		IDispatchMessage message,
		string? destination,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var context = contextAccessor.MessageContext
			?? throw new InvalidOperationException(
				"TransactionalOutboxWriter requires an active message context.");

		// Verify ambient transaction exists (set by TransactionMiddleware at key "Transaction")
		_ = context.GetItem<object>("Transaction")
			?? throw new InvalidOperationException(
				"TransactionalOutboxWriter requires an ambient transaction. " +
				"Ensure TransactionMiddleware is in the pipeline and configured.");

		// Propagate destination and scheduled delivery time through context items
		// so EnqueueAsync (and its store implementations) can include them in
		// the persisted OutboundMessage.
		// Propagate destination through context items so EnqueueAsync (and its store
		// implementations) can include it in the persisted OutboundMessage.
		if (destination is not null)
		{
			context.SetItem("OutboxDestination", destination);
		}

		// Delegate to EnqueueAsync which handles serialization, header construction,
		// and correlation/causation/tenant context propagation from IMessageContext.
		await outboxStore.EnqueueAsync(message, context, cancellationToken)
			.ConfigureAwait(false);
	}
}
