// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Decorator middleware that reports a caller-supplied pipeline stage in place of the inner middleware's
/// own. Created by <c>PipelineBuilder.UseAt&lt;T&gt;(stage)</c> so that a registration-time stage override
/// reaches the pipeline's stage ordering.
/// </summary>
/// <remarks>
/// The override is applied by decoration rather than by assigning the inner middleware's stage, which keeps
/// it scoped to this one registration. Middleware is commonly resolved as a DI singleton shared by every
/// pipeline, so writing the stage onto the instance would leak one pipeline's ordering choice into all of
/// them.
/// </remarks>
internal sealed class StageOverrideMiddleware(
	IDispatchMiddleware inner,
	DispatchMiddlewareStage stage) : IDispatchMiddleware, IMiddlewareDecorator
{
	/// <inheritdoc />
	public IDispatchMiddleware Inner => inner;

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => stage;

	/// <inheritdoc />
	public MessageKinds ApplicableMessageKinds => inner.ApplicableMessageKinds;

	/// <inheritdoc />
	public ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
		=> inner.InvokeAsync(message, context, nextDelegate, cancellationToken);
}
