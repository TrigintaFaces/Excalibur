// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

using Excalibur.Saga.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Saga.Orchestration;

/// <summary>
/// Middleware that coordinates saga event handling after the main message handlers execute.
/// </summary>
public sealed partial class SagaHandlingMiddleware(SagaCoordinator coordinator, ILogger<SagaHandlingMiddleware> logger)
	: IDispatchMiddleware
{
	/// <summary>
	/// Gets the middleware execution stage. Saga handling occurs at the end of the pipeline after main handlers complete. This ensures that
	/// business logic executes first, followed by saga coordination for workflow orchestration.
	/// </summary>
	/// <value>The current <see cref="Stage"/> value.</value>
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.End;

	/// <summary>
	/// Invokes the middleware to handle saga event processing after main message handlers complete. This method executes the next
	/// middleware/handler first, then processes any saga events for workflow orchestration. Provides comprehensive error handling and
	/// logging for saga coordination failures.
	/// </summary>
	/// <param name="message"> Dispatch message that may be a saga event requiring workflow processing. </param>
	/// <param name="context"> Message context containing correlation, routing, and processing metadata. </param>
	/// <param name="nextDelegate"> Next delegate in the middleware pipeline chain. </param>
	/// <param name="cancellationToken"> Cancellation token for operation timeout and graceful shutdown. </param>
	/// <returns> Message result from the pipeline execution, preserving original handler results. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when context or nextDelegate is null. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when saga processing fails due to configuration or state issues. </exception>
	[UnconditionalSuppressMessage(
			"Trimming",
			"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
			Justification = "Saga handling depends on registered saga types and guards reflection usage.")]
	[UnconditionalSuppressMessage(
			"Aot",
			"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
			Justification = "Saga handling uses reflection for registered saga types.")]
	public async ValueTask<IMessageResult> InvokeAsync(
	IDispatchMessage message,
	IMessageContext context,
	DispatchRequestDelegate nextDelegate,
	CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

		if (message is not ISagaEvent sagaEvent)
		{
			return result;
		}

		try
		{
			await coordinator.ProcessEventAsync(context, sagaEvent, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogSagaHandlingFailed(sagaEvent.GetType().Name, ex);

			throw;
		}

		return result;
	}

	// Source-generated logging methods
	[LoggerMessage(SagaEventId.SagaHandlingFailed, LogLevel.Error,
		"Saga handling failed for event {EventType}")]
	private partial void LogSagaHandlingFailed(string eventType, Exception ex);
}
