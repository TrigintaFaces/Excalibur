// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Outbox;

using System.Diagnostics.CodeAnalysis;

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
internal sealed class DeferredOutboxWriter(IMessageContextAccessor contextAccessor) : IOutboxWriter, IScheduledOutboxWriter
{
	/// <inheritdoc />
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public ValueTask WriteAsync(
		IDispatchMessage message,
		string? destination,
		CancellationToken cancellationToken) =>
		Buffer(message, destination, scheduledAt: null);

	/// <inheritdoc />
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public ValueTask WriteScheduledAsync(
		IDispatchMessage message,
		string? destination,
		DateTimeOffset scheduledAt,
		CancellationToken cancellationToken) =>
		Buffer(message, destination, scheduledAt);

	private ValueTask Buffer(IDispatchMessage message, string? destination, DateTimeOffset? scheduledAt)
	{
		ArgumentNullException.ThrowIfNull(message);

		var context = contextAccessor.MessageContext
			?? throw new InvalidOperationException(
				"DeferredOutboxWriter requires an active message context. " +
				"Ensure this is called within a dispatch pipeline.");

		var outboxContext = context.GetItem<OutboxContext>("OutboxContext")
			?? throw new InvalidOperationException(
				"This handler wrote to the outbox, but no IOutboxStore is registered for this host, so the " +
				"write has nowhere to be staged. Register an outbox store for your provider -- " +
				"AddSqlServerOutbox(), AddPostgresOutbox() or the equivalent for the store you use -- or " +
				"register your own IOutboxStore implementation. Outbox staging runs on the default pipeline " +
				"automatically once a store is present, so no further pipeline configuration is needed. This " +
				"names the registration rather than the middleware deliberately: the middleware is a " +
				"composition detail you did not choose and cannot find in your own code, whereas the " +
				"IOutboxStore registration is yours to add.");

		outboxContext.AddOutboundMessage(message, destination, scheduledAt);

		return ValueTask.CompletedTask;
	}
}
