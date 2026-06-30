// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading;
using System.Threading.Tasks;

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Compat.MassTransit;

/// <summary>
/// Adapts a migrated MassTransit-style <see cref="IConsumer{TMessage}"/> onto the canonical Excalibur
/// <see cref="IEventHandler{TEvent}"/>, invoking <see cref="IConsumer{TMessage}.Consume"/> with a
/// minimal <see cref="ConsumeContext{TMessage}"/> built from the dispatched event.
/// </summary>
/// <typeparam name="TConsumer">The migrated consumer type.</typeparam>
/// <typeparam name="TMessage">
/// The consumed message type, which must be a dispatch event. Annotating the migrated message with
/// <see cref="IDispatchEvent"/> is the documented manual migration step that makes the consumer routable
/// through the Excalibur pipeline.
/// </typeparam>
/// <remarks>
/// This is the deterministic half of the MassTransit consumer migration (FR-16): the
/// <c>Consume(context)</c> → <c>HandleAsync(event, ct)</c> bridge. No reflection is used (generic,
/// DI-resolved), so the adapter is AOT-safe.
/// </remarks>
internal sealed class ConsumerEventHandlerAdapter<TConsumer, TMessage> : IEventHandler<TMessage>
	where TConsumer : IConsumer<TMessage>
	where TMessage : class, IDispatchEvent
{
	private readonly TConsumer _consumer;

	/// <summary>
	/// Initializes a new instance of the <see cref="ConsumerEventHandlerAdapter{TConsumer, TMessage}"/>
	/// class.
	/// </summary>
	/// <param name="consumer">The migrated consumer, resolved from dependency injection.</param>
	public ConsumerEventHandlerAdapter(TConsumer consumer) => _consumer = consumer;

	/// <inheritdoc />
	public Task HandleAsync(TMessage eventMessage, CancellationToken cancellationToken)
	{
		var context = new DefaultConsumeContext<TMessage>(eventMessage, cancellationToken);
		return _consumer.Consume(context);
	}
}
