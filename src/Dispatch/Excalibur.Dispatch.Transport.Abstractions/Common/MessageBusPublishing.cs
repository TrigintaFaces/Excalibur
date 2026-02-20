// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Provides common IMessageBus publishing method implementations.
/// </summary>
public static class MessageBusPublishing
{
	/// <summary>
	/// Default implementation for PublishAsync(Excalibur.Dispatch.Abstractions.IDispatchAction).
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public static async Task PublishActionAsync(
		Dispatch.Abstractions.IDispatchAction action,
		Dispatch.Abstractions.IMessageContext context,
		Func<string, byte[], Dispatch.Abstractions.IMessageContext, CancellationToken, Task> publishFunc,
		Dispatch.Abstractions.Serialization.IPayloadSerializer serializer,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(publishFunc);

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var body = serializer.SerializeObject(action, action.GetType());
		var routingKey = context.Items.TryGetValue("RoutingKey", out var key) && key is string rk ? rk : action.GetType().Name;
		await publishFunc(routingKey, body, context, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Default implementation for PublishAsync(Excalibur.Dispatch.Abstractions.IDispatchEvent).
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public static async Task PublishEventAsync(
		Dispatch.Abstractions.IDispatchEvent evt,
		Dispatch.Abstractions.IMessageContext context,
		Func<string, byte[], Dispatch.Abstractions.IMessageContext, CancellationToken, Task> publishFunc,
		Dispatch.Abstractions.Serialization.IPayloadSerializer serializer,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(evt);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(publishFunc);

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var body = serializer.SerializeObject(evt, evt.GetType());
		var routingKey = context.Items.TryGetValue("RoutingKey", out var key) && key is string rk ? rk : evt.GetType().Name;
		await publishFunc(routingKey, body, context, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Default implementation for PublishAsync(Excalibur.Dispatch.Abstractions.IDispatchDocument).
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public static async Task PublishDocumentAsync(
		Dispatch.Abstractions.IDispatchDocument doc,
		Dispatch.Abstractions.IMessageContext context,
		Func<string, byte[], Dispatch.Abstractions.IMessageContext, CancellationToken, Task> publishFunc,
		Dispatch.Abstractions.Serialization.IPayloadSerializer serializer,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(doc);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(publishFunc);

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var body = serializer.SerializeObject(doc, doc.GetType());
		var routingKey = context.Items.TryGetValue("RoutingKey", out var key) && key is string rk ? rk : doc.GetType().Name;
		await publishFunc(routingKey, body, context, cancellationToken).ConfigureAwait(false);
	}
}
