// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Threading;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Threading;

/// <summary>
/// Middleware that detects and executes messages marked for background processing.
/// </summary>
/// <remarks>
/// <para>
/// This middleware intercepts messages implementing <see cref="IExecuteInBackground" /> and hands them to a
/// background worker, returning immediately with a successful result whose
/// <see cref="IMessageResult.Disposition" /> is
/// <see cref="MessageDisposition.AcceptedForBackgroundExecution" />. That disposition is the contract: the
/// dispatch succeeded and the handler has <b>not</b> run, so a caller recording completion must not treat
/// it as done. Messages expecting typed results cannot be executed in the background and fail with a
/// 400 Bad Request error.
/// </para>
/// <para>
/// A dispatch whose token is already cancelled is <b>not</b> accepted: the handler never runs and the result is
/// <see cref="MessageResult.Cancelled()" />. Once accepted, the work no longer follows the caller's token —
/// that token is often request-scoped and fires when the response carrying the acceptance completes. It
/// runs until it finishes, and is cancelled only when the host's shutdown budget elapses with it still running.
/// </para>
/// <para>
/// A failing background message is always logged. What happens next is the host-level policy
/// <see cref="BackgroundExecutionOptions.ExceptionBehavior" />.
/// </para>
/// </remarks>
public sealed partial class BackgroundExecutionMiddleware : IDispatchMiddleware
{
	private readonly BackgroundExecutionExceptionBehavior _exceptionBehavior;
	private readonly IHostApplicationLifetime? _hostApplicationLifetime;
	private readonly ILogger<BackgroundExecutionMiddleware> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="BackgroundExecutionMiddleware" /> class.
	/// </summary>
	/// <param name="options"> The background execution options. </param>
	/// <param name="logger"> Logger for diagnostic information. </param>
	/// <param name="hostApplicationLifetime">
	/// The host's lifetime. Required when <see cref="BackgroundExecutionOptions.ExceptionBehavior" /> is
	/// <see cref="BackgroundExecutionExceptionBehavior.StopHost" />.
	/// </param>
	/// <exception cref="InvalidOperationException">
	/// <see cref="BackgroundExecutionExceptionBehavior.StopHost" /> is configured and no
	/// <paramref name="hostApplicationLifetime" /> is supplied.
	/// </exception>
	public BackgroundExecutionMiddleware(
		IOptions<BackgroundExecutionOptions> options,
		ILogger<BackgroundExecutionMiddleware> logger,
		IHostApplicationLifetime? hostApplicationLifetime = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_exceptionBehavior = options.Value.ExceptionBehavior;
		_hostApplicationLifetime = hostApplicationLifetime;
		_logger = logger;

		// Startup validation refuses this configuration under a host. This guards the paths that bypass it —
		// direct construction, or a container built without ValidateOnStart — so StopHost can never quietly
		// degrade to log-only at the first failure.
		if (_exceptionBehavior == BackgroundExecutionExceptionBehavior.StopHost && hostApplicationLifetime is null)
		{
			throw new InvalidOperationException(
				$"{nameof(BackgroundExecutionOptions)}.{nameof(BackgroundExecutionOptions.ExceptionBehavior)} is " +
				$"{nameof(BackgroundExecutionExceptionBehavior.StopHost)}, but no {nameof(IHostApplicationLifetime)} is available.");
		}
	}

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
	/// For a background message, a completed task carrying a successful result whose
	/// <see cref="IMessageResult.Disposition" /> is
	/// <see cref="MessageDisposition.AcceptedForBackgroundExecution" /> -- the work is accepted and pending,
	/// not complete. For any other message, delegates to the next middleware.
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
		if (message is not IExecuteInBackground)
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

		// Not accepted: a caller that has already cancelled is told so synchronously, and the handler never runs.
		// Accepting it would report pending work that the caller had withdrawn before it was handed over.
		if (cancellationToken.IsCancellationRequested)
		{
			LogBackgroundExecutionNotAccepted(_logger, message.GetType().Name);
			return new ValueTask<IMessageResult>(MessageResult.Cancelled());
		}

		// Accepted: from here the work follows the drain's shutdown token, NOT the caller's.
		BackgroundTaskRunner.RunDetachedUntilShutdown(
			async shutdownToken =>
			{
				try
				{
					_ = await nextDelegate(message, context, shutdownToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
				{
					// The host's shutdown budget elapsed with this work still running. That is the drain's report to
					// make, not a handler failure, so it neither counts as one nor triggers the failure policy.
					LogBackgroundExecutionCancelledAtShutdown(_logger, message.GetType().Name);
				}
				catch (Exception ex)
				{
					LogBackgroundExecutionFailed(_logger, ex, message.GetType().Name);

					if (_exceptionBehavior == BackgroundExecutionExceptionBehavior.StopHost)
					{
						LogStoppingHost(_logger, ex, message.GetType().Name);
						_hostApplicationLifetime!.StopApplication();
					}
				}
			},
			onError: null,
			_logger);

		// The caller is told the work is PENDING through the disposition, not through problem details.
		// A 202-shaped MessageProblemDetails was built here and discarded; carrying it instead would put
		// an RFC 7807 error object (IMessageProblemDetails: "details of errors", Title defaulting to
		// "Error") onto a successful result, while IMessageResult.ProblemDetails is documented as
		// non-null only "when the operation fails" -- so a caller testing it for failure would read a
		// backgrounded message as failed. The disposition is the field that exists to answer this.
		return new ValueTask<IMessageResult>(
			new BasicMessageResult(
				succeeded: true,
				disposition: MessageDisposition.AcceptedForBackgroundExecution));
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
		"Background execution of {MessageType} failed and the configured exception behavior is StopHost. Stopping the host.")]
	private static partial void LogStoppingHost(
		ILogger logger,
		Exception ex,
		string messageType);

	[LoggerMessage(CoreEventId.BackgroundTaskCancelled, LogLevel.Warning,
		"Background execution of {MessageType} was cancelled because the host's shutdown budget elapsed before it finished.")]
	private static partial void LogBackgroundExecutionCancelledAtShutdown(
		ILogger logger,
		string messageType);

	[LoggerMessage(CoreEventId.BackgroundExecutionNotAccepted, LogLevel.Information,
		"Message {MessageType} was not accepted for background execution because the dispatch was already cancelled.")]
	private static partial void LogBackgroundExecutionNotAccepted(
		ILogger logger,
		string messageType);
}
