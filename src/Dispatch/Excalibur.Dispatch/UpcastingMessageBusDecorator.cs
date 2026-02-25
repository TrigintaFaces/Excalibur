// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Decorates <see cref="IMessageBus"/> to automatically upcast versioned messages before delivery to handlers.
/// </summary>
/// <remarks>
/// <para>
/// This decorator intercepts incoming messages and upcasts them to their latest registered version
/// using the <see cref="IUpcastingPipeline"/>. This ensures handlers always receive the latest
/// message schema version, regardless of which version was originally published.
/// </para>
/// <para>
/// <b>Upcasting Behavior:</b>
/// <list type="bullet">
/// <item><description>Events (IDispatchEvent): Upcasted before delivery to handlers</description></item>
/// <item><description>Actions (IDispatchAction): Pass through unchanged (commands are ephemeral)</description></item>
/// <item><description>Documents (IDispatchDocument): Pass through unchanged</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Performance:</b>
/// <list type="bullet">
/// <item><description>Non-versioned messages: ~0ns overhead (simple type check)</description></item>
/// <item><description>Already latest version: ~5ns (version comparison only)</description></item>
/// <item><description>Requires upcasting: ~15ns per hop (cached paths)</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Thread Safety:</b> This decorator is stateless and thread-safe as a singleton.
/// The underlying <see cref="IUpcastingPipeline"/> handles its own thread synchronization.
/// </para>
/// </remarks>
/// <seealso cref="IUpcastingPipeline"/>
/// <seealso cref="IVersionedMessage"/>
public sealed class UpcastingMessageBusDecorator : IMessageBus
{
	private readonly IMessageBus _inner;
	private readonly IUpcastingPipeline _pipeline;

	/// <summary>
	/// Initializes a new instance of the <see cref="UpcastingMessageBusDecorator"/> class.
	/// </summary>
	/// <param name="inner">The inner message bus to decorate.</param>
	/// <param name="pipeline">The upcasting pipeline for version transformation.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> or <paramref name="pipeline"/> is null.</exception>
	public UpcastingMessageBusDecorator(IMessageBus inner, IUpcastingPipeline pipeline)
	{
		_inner = inner ?? throw new ArgumentNullException(nameof(inner));
		_pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
	}

	/// <summary>
	/// Publishes an action message to the message bus without upcasting.
	/// </summary>
	/// <param name="action">The action to publish.</param>
	/// <param name="context">The message context containing metadata and routing information.</param>
	/// <param name="cancellationToken">The cancellation token to observe.</param>
	/// <returns>A task representing the asynchronous publish operation.</returns>
	/// <remarks>
	/// Actions (commands) are ephemeral and pass through unchanged. The producer is expected
	/// to send the current version, and handlers should be updated to accept the latest schema.
	/// </remarks>
	public Task PublishAsync(IDispatchAction action, IMessageContext context, CancellationToken cancellationToken)
	{
		// Actions are ephemeral - pass through without upcasting
		return _inner.PublishAsync(action, context, cancellationToken);
	}

	/// <summary>
	/// Publishes an event message to the message bus, upcasting if necessary.
	/// </summary>
	/// <param name="evt">The event to publish.</param>
	/// <param name="context">The message context containing metadata and routing information.</param>
	/// <param name="cancellationToken">The cancellation token to observe.</param>
	/// <returns>A task representing the asynchronous publish operation.</returns>
	/// <remarks>
	/// <para>
	/// Integration events may arrive from external systems in older versions.
	/// This method upcasts versioned events to the latest registered version before
	/// delivering to handlers.
	/// </para>
	/// <para>
	/// If the event does not implement <see cref="IVersionedMessage"/>, or if it is already
	/// at the latest version, it passes through with minimal overhead.
	/// </para>
	/// </remarks>
	public Task PublishAsync(IDispatchEvent evt, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(evt);
		ArgumentNullException.ThrowIfNull(context);

		// Check if event is versioned and needs upcasting
		if (evt is IVersionedMessage versioned)
		{
			var latestVersion = _pipeline.GetLatestVersion(versioned.MessageType);

			// Only upcast if not at latest version (and latest version is known)
			if (latestVersion > 0 && versioned.Version < latestVersion)
			{
				var upcasted = _pipeline.Upcast(evt);

				// Track original and upcasted types in context for observability
				context.Items["Dispatch:OriginalMessageType"] = evt.GetType();
				context.Items["Dispatch:UpcastedMessageType"] = upcasted.GetType();
				context.Items["Dispatch:OriginalVersion"] = versioned.Version;
				context.Items["Dispatch:UpcastedVersion"] = latestVersion;

				return _inner.PublishAsync((IDispatchEvent)upcasted, context, cancellationToken);
			}
		}

		// Non-versioned or already at latest version - pass through
		return _inner.PublishAsync(evt, context, cancellationToken);
	}

	/// <summary>
	/// Publishes a document message to the message bus without upcasting.
	/// </summary>
	/// <param name="doc">The document to publish.</param>
	/// <param name="context">The message context containing metadata and routing information.</param>
	/// <param name="cancellationToken">The cancellation token to observe.</param>
	/// <returns>A task representing the asynchronous publish operation.</returns>
	/// <remarks>
	/// Documents pass through unchanged. Document versioning, if needed, should be handled
	/// at the application layer through explicit transformation.
	/// </remarks>
	public Task PublishAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken)
	{
		// Documents pass through without upcasting
		return _inner.PublishAsync(doc, context, cancellationToken);
	}
}
