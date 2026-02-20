// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Invokes registered <see cref="IDispatchMiddleware" /> components for a dispatch operation.
/// </summary>
public interface IDispatchMiddlewareInvoker
{
	/// <summary>
	/// Executes each middleware component and returns the resulting <see cref="IMessageResult" />.
	/// </summary>
	/// <typeparam name="T"> Expected type of the result. </typeparam>
	/// <param name="message"> Message being dispatched. </param>
	/// <param name="context"> Context associated with the message. </param>
	/// <param name="nextDelegate"> Delegate to invoke after middleware. </param>
	/// <param name="cancellationToken"> Token used to cancel the operation. </param>
	/// <returns> The result produced by the middleware chain. </returns>
	/// <remarks>
	/// This method returns <see cref="ValueTask{T}"/> to avoid Task allocation on synchronous completion paths.
	/// When the middleware chain completes synchronously (e.g., cache hit, no middleware), no Task is allocated.
	/// </remarks>
	ValueTask<T> InvokeAsync<T>(
		IDispatchMessage message,
		IMessageContext context,
		Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<T>> nextDelegate,
		CancellationToken cancellationToken)
		where T : IMessageResult;
}
