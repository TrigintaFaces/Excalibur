// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Provides a base implementation for dispatch middleware with allocation-free patterns.
/// </summary>
/// <remarks>
/// This abstract class provides a foundation for creating middleware components with minimal allocations. It implements common patterns and
/// provides virtual methods for customization. Derived classes should override the ProcessAsync method to implement their specific logic.
/// </remarks>
public abstract class DispatchMiddlewareBase : IDispatchMiddleware
{
	/// <inheritdoc />
	public virtual DispatchMiddlewareStage? Stage { get; }

	/// <inheritdoc />
	public virtual MessageKinds ApplicableMessageKinds => MessageKinds.All;

	/// <inheritdoc />
	public virtual IReadOnlyCollection<string>? RequiredFeatures => null;

	/// <inheritdoc />
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		// Check if this middleware should process the message
		if (!ShouldProcess(message, context))
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Pre-processing logic
		var preprocessResult = await OnBeforeProcessAsync(message, context, cancellationToken).ConfigureAwait(false);
		if (preprocessResult != null)
		{
			return preprocessResult;
		}

		try
		{
			// Process the message
			var result = await ProcessAsync(message, context, nextDelegate, cancellationToken).ConfigureAwait(false);

			// Post-processing logic
			return await OnAfterProcessAsync(message, context, result, cancellationToken).ConfigureAwait(false) ?? result;
		}
		catch (Exception ex)
		{
			return await OnErrorAsync(message, context, ex, cancellationToken).ConfigureAwait(false)
				   ?? throw new InvalidOperationException($"Middleware {GetType().Name} encountered an error", ex);
		}
	}

	/// <summary>
	/// Determines whether this middleware should process the given message.
	/// </summary>
	/// <param name="message"> The message to evaluate. </param>
	/// <param name="context"> The message context. </param>
	/// <returns> True if the message should be processed; otherwise, false. </returns>
	protected virtual bool ShouldProcess(IDispatchMessage message, IMessageContext context)
	{
		// Check message kind applicability based on message type
		var messageKind = message switch
		{
			IDispatchAction => MessageKinds.Action,
			IDispatchEvent => MessageKinds.Event,
			IDispatchDocument => MessageKinds.Document,
			_ => MessageKinds.None,
		};

		if (messageKind != MessageKinds.None)
		{
			return (messageKind & ApplicableMessageKinds) != 0;
		}

		// Default to processing all messages if not an envelope
		return true;
	}

	/// <summary>
	/// Called before processing the message.
	/// </summary>
	/// <param name="message"> The message being processed. </param>
	/// <param name="context"> The message context. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A result to short-circuit processing, or null to continue. </returns>
	protected virtual ValueTask<IMessageResult?> OnBeforeProcessAsync(
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken) =>
		ValueTask.FromResult<IMessageResult?>(null);

	/// <summary>
	/// Processes the message through the middleware logic.
	/// </summary>
	/// <param name="message"> The message being processed. </param>
	/// <param name="context"> The message context. </param>
	/// <param name="nextDelegate"> The next middleware delegate. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The processing result. </returns>
	protected virtual async ValueTask<IMessageResult> ProcessAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(nextDelegate);

		// Default implementation just calls the next middleware
		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Called after processing the message.
	/// </summary>
	/// <param name="message"> The message that was processed. </param>
	/// <param name="context"> The message context. </param>
	/// <param name="result"> The processing result. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A modified result, or null to use the original result. </returns>
	protected virtual ValueTask<IMessageResult?> OnAfterProcessAsync(
		IDispatchMessage message,
		IMessageContext context,
		IMessageResult result,
		CancellationToken cancellationToken) =>
		ValueTask.FromResult<IMessageResult?>(null);

	/// <summary>
	/// Called when an error occurs during processing.
	/// </summary>
	/// <param name="message"> The message being processed. </param>
	/// <param name="context"> The message context. </param>
	/// <param name="exception"> The exception that occurred. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A result to handle the error, or null to rethrow. </returns>
	protected virtual ValueTask<IMessageResult?> OnErrorAsync(
		IDispatchMessage message,
		IMessageContext context,
		Exception exception,
		CancellationToken cancellationToken) =>
		ValueTask.FromResult<IMessageResult?>(null);
}
