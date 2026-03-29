// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Decorator middleware that restricts an inner middleware to specific message kinds.
/// Created by <c>PipelineBuilder.ForMessageKinds().Use&lt;T&gt;()</c> to scope
/// middleware to only run for configured message types.
/// </summary>
internal sealed class MessageKindFilteringMiddleware(
	IDispatchMiddleware inner,
	MessageKinds applicableKinds) : IDispatchMiddleware
{
	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => inner.Stage;

	/// <inheritdoc />
	public MessageKinds ApplicableMessageKinds => applicableKinds;

	/// <inheritdoc />
	public ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
		=> inner.InvokeAsync(message, context, nextDelegate, cancellationToken);
}
