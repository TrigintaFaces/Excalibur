// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
				"This handler wrote to the outbox, but the dispatch pipeline it ran on has no outbox " +
				"staging stage, so the write has nowhere to be held until the transaction commits. Two " +
				"things are needed and they are separate: register an IOutboxStore for your provider -- " +
				"AddSqlServerOutbox(), AddPostgresOutbox(), or your own IOutboxStore implementation -- " +
				"and add the staging " +
				"middleware to the pipeline this message dispatches on, with " +
				"pipeline.Use<OutboxStagingMiddleware>(). Both are yours to add on purpose: this " +
				"framework seats no middleware you did not ask for, so that a host which does not use " +
				"an outbox pays nothing for one.");

		outboxContext.AddOutboundMessage(message, destination, scheduledAt);

		return ValueTask.CompletedTask;
	}
}
