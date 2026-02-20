// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Threading;

/// <summary>
/// Middleware that detects and executes messages marked for background processing.
/// </summary>
/// <remarks>
/// This middleware intercepts messages implementing <see cref="IExecuteInBackground" /> and executes them on a background thread,
/// immediately returning a 202 Accepted response. Messages expecting typed results cannot be executed in the background and will fail with
/// a 400 Bad Request error.
/// </remarks>
/// <param name="logger"> Logger for diagnostic information. </param>
public sealed partial class BackgroundExecutionMiddleware(ILogger<BackgroundExecutionMiddleware> logger) : IDispatchMiddleware
{
	private readonly ILogger<BackgroundExecutionMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <summary>
	/// Gets the pipeline stage where this middleware should execute.
	/// </summary>
	/// <remarks> Executes near the end of the pipeline to ensure validation and authorization have already been performed. </remarks>
	/// <value> The current <see cref="Stage" /> value. </value>
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.End - 1;

	/// <summary>
	/// Processes messages marked for background execution.
	/// </summary>
	/// <param name="message"> The message being processed. </param>
	/// <param name="context"> The message context. </param>
	/// <param name="nextDelegate"> The next middleware in the pipeline. </param>
	/// <param name="cancellationToken"> Token to cancel the operation. </param>
	/// <returns>
	/// A completed task with a 202 Accepted result for background messages, or delegates to the next middleware for regular messages.
	/// </returns>
	/// <exception cref="ArgumentNullException"> Thrown when required parameters are null. </exception>
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification = "Background execution checks only known dispatch message types registered at startup.")]
	public ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		// Pass through messages not marked for background execution
		if (message is not IExecuteInBackground backgroundMessage)
		{
			return nextDelegate(message, context, cancellationToken);
		}

		// Validate that background messages don't expect typed results
		if (ExpectsTypedResult(message))
		{
			LogCannotExecuteInBackground(_logger, message.GetType().Name);

			var invalidBackgroundProblem = new MessageProblemDetails
			{
				Type = ProblemDetailsTypes.BackgroundExecution,
				Title = "Invalid Background Execution",
				Status = 400,
				Detail =
					$"Message of type '{message.GetType().Name}' cannot be executed in background because it expects a strongly-typed result.",
				Instance = Guid.NewGuid().ToString(),
			};
			return new ValueTask<IMessageResult>(
				MessageResult.Failed(invalidBackgroundProblem));
		}

		// Execute the message processing in the background
		BackgroundTaskRunner.RunDetachedInBackground(
			async ct =>
			{
				try
				{
					_ = await nextDelegate(message, context, ct).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					LogBackgroundExecutionFailed(_logger, ex, message.GetType().Name);

					if (backgroundMessage.PropagateExceptions)
					{
						// Here "propagate" explicitly means critical exceptions should terminate the host or trigger external handling
						LogCriticalBackgroundException(_logger, ex, message.GetType().Name);

						// Rethrow to global background error handler (e.g., an unhandled exception handler)
						throw;
					}

					// Otherwise, explicitly log without propagating
					LogBackgroundExceptionNotPropagated(_logger, ex, message.GetType().Name);
				}
			},
			cancellationToken,
			ex => Task.CompletedTask,
			_logger);

		var problemDetails = new MessageProblemDetails
		{
			Type = ProblemDetailsTypes.BackgroundExecution,
			Title = "Background Execution",
			Status = 202,
			Detail = "The request has been accepted for background execution.",
			Instance = Guid.NewGuid().ToString(),
		};
		return new ValueTask<IMessageResult>(MessageResult.Success());
	}

	/// <summary>
	/// Determines if a message expects a typed result.
	/// </summary>
	/// <param name="message"> The message to check. </param>
	/// <returns> True if the message implements IDispatchAction{T}; otherwise, false. </returns>
	[RequiresUnreferencedCode("Uses reflection to check message interfaces")]
	private static bool ExpectsTypedResult(IDispatchMessage message)
	{
		// Optimized version that avoids LINQ allocation
		var interfaces = message.GetType().GetInterfaces();
		for (var i = 0; i < interfaces.Length; i++)
		{
			var @interface = interfaces[i];
			if (@interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(IDispatchAction<>))
			{
				return true;
			}
		}

		return false;
	}

	// Source-generated logging methods
	[LoggerMessage(CoreEventId.BackgroundExecutionInvalid, LogLevel.Error,
		"Cannot execute message {MessageType} in background because it expects a strongly-typed result.")]
	private static partial void LogCannotExecuteInBackground(
		ILogger logger,
		string messageType);

	[LoggerMessage(CoreEventId.BackgroundExecutionFailed, LogLevel.Error,
		"Background execution failed for message type {MessageType}.")]
	private static partial void LogBackgroundExecutionFailed(
		ILogger logger,
		Exception ex,
		string messageType);

	[LoggerMessage(CoreEventId.BackgroundExecutionCritical, LogLevel.Critical,
		"Critical exception occurred during background execution of {MessageType}. Initiating shutdown.")]
	private static partial void LogCriticalBackgroundException(
		ILogger logger,
		Exception ex,
		string messageType);

	[LoggerMessage(CoreEventId.BackgroundExceptionNotPropagated, LogLevel.Warning,
		"Exception during background execution of {MessageType} logged but not propagated.")]
	private static partial void LogBackgroundExceptionNotPropagated(
		ILogger logger,
		Exception ex,
		string messageType);
}
