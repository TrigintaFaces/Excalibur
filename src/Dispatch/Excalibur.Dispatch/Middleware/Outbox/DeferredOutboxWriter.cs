// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Outbox;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Middleware.Outbox;

/// <summary>
/// Buffers messages in <see cref="OutboxContext"/> for post-processing flush
/// by <see cref="OutboxStagingMiddleware"/>.
/// </summary>
/// <remarks>
/// This is the default <see cref="IOutboxWriter"/> implementation used in
/// <see cref="OutboxConsistencyMode.EventuallyConsistent"/> mode. Messages are
/// buffered during handler execution and staged after the handler completes.
/// </remarks>
internal sealed class DeferredOutboxWriter(IMessageContextAccessor contextAccessor) : IOutboxWriter
{
	/// <inheritdoc />
	public ValueTask WriteAsync(
		IDispatchMessage message,
		string? destination,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var context = contextAccessor.MessageContext
			?? throw new InvalidOperationException(
				"DeferredOutboxWriter requires an active message context. " +
				"Ensure this is called within a dispatch pipeline.");

		var outboxContext = context.GetItem<OutboxContext>("OutboxContext")
			?? throw new InvalidOperationException(
				"OutboxContext not found. Ensure OutboxStagingMiddleware is in the pipeline.");

		var scheduledAt = OutboxScheduledDeliveryScope.Current;
		outboxContext.AddOutboundMessage(message, destination, scheduledAt);

		return ValueTask.CompletedTask;
	}
}
