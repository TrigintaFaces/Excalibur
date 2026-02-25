// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery.Pipeline;

/// <summary>
/// Default middleware executor that uses a struct-based state machine.
/// </summary>
public sealed class DefaultMiddlewareExecutor : IMiddlewareExecutor
{
	/// <inheritdoc />
	public ValueTask<IMessageResult> ExecuteAsync(
		ref MiddlewareContext context,
		IDispatchMessage message,
		IMessageContext messageContext,
		DispatchRequestDelegate finalDelegate,
		CancellationToken cancellationToken)
	{
		var stateMachine = new MiddlewareStateMachine(
			context,
			message,
			messageContext,
			finalDelegate,
			cancellationToken);

		return stateMachine.RunAsync();
	}

	/// <inheritdoc />
	[SuppressMessage("Style", "RCS1163:Unused parameter",
		Justification =
			"Local function TypedFinalDelegate parameter 'ct' required by DispatchRequestDelegate signature in state machine pattern")]
	public ValueTask<IMessageResult<TResponse>> ExecuteAsync<TMessage, TResponse>(
		ref MiddlewareContext context,
		TMessage message,
		IMessageContext messageContext,
		CancellationToken cancellationToken)
		where TMessage : IDispatchAction<TResponse>
	{
		// Create final delegate that extracts the typed response
		ValueTask<IMessageResult> TypedFinalDelegate(
			IDispatchMessage msg,
			IMessageContext ctx,
			CancellationToken ct)
		{
			_ = msg;
			_ = ctx;
			// This would invoke the final handler and extract the result For now, return a placeholder
			var result = MessageResult.Success(default(TResponse));
			return new ValueTask<IMessageResult>(result);
		}

		var stateMachine = new MiddlewareStateMachine(
			context,
			message,
			messageContext,
			TypedFinalDelegate,
			cancellationToken);

		// Use async helper method instead of ContinueWith to avoid closure allocations
		return CastResultAsync<TResponse>(stateMachine.RunAsync());
	}

	/// <summary>
	/// Helper method to cast IMessageResult to IMessageResult&lt;TResponse&gt; using async/await
	/// instead of ContinueWith, avoiding closure allocations.
	/// </summary>
	private static async ValueTask<IMessageResult<TResponse>> CastResultAsync<TResponse>(ValueTask<IMessageResult> task)
	{
		var result = await task.ConfigureAwait(false);
		return (IMessageResult<TResponse>)result;
	}

	/// <summary>
	/// State machine for executing middleware without allocations.
	/// </summary>
	private struct MiddlewareStateMachine(
		MiddlewareContext context,
		IDispatchMessage message,
		IMessageContext messageContext,
		DispatchRequestDelegate finalDelegate,
		CancellationToken cancellationToken)
	{
		private MiddlewareContext _context = context;

		public ValueTask<IMessageResult> RunAsync() => ExecuteNextAsync();

		private ValueTask<IMessageResult> ExecuteNextAsync()
		{
			var middleware = _context.MoveNext();

			if (middleware == null)
			{
				// No more middleware, execute final delegate
				return finalDelegate(message, messageContext, cancellationToken);
			}

			// Execute current middleware with a delegate that captures 'this' This is where we still have some allocation, but it's minimized
			return middleware.InvokeAsync(
				message,
				messageContext,
				NextDelegateAsync,
				cancellationToken);
		}

		private ValueTask<IMessageResult> NextDelegateAsync(
			IDispatchMessage message,
			IMessageContext context,
			CancellationToken cancellationToken) =>

			// Continue with next middleware
			ExecuteNextAsync();
	}
}
