// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery.Pipeline;

/// <summary>
/// Interface for middleware executors to avoid virtual calls.
/// </summary>
public interface IMiddlewareExecutor
{
	/// <summary>
	/// Executes the middleware pipeline.
	/// </summary>
	/// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
	ValueTask<IMessageResult> ExecuteAsync(
		ref MiddlewareContext context,
		IDispatchMessage message,
		IMessageContext messageContext,
		DispatchRequestDelegate finalDelegate,
		CancellationToken cancellationToken);

	/// <summary>
	/// Executes the middleware pipeline with a typed result.
	/// </summary>
	/// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
	ValueTask<IMessageResult<TResponse>> ExecuteAsync<TMessage, TResponse>(
		ref MiddlewareContext context,
		TMessage message,
		IMessageContext messageContext,
		CancellationToken cancellationToken)
		where TMessage : IDispatchAction<TResponse>;
}
