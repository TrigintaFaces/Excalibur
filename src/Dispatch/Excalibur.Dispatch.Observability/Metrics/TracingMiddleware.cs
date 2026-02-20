// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Extensions;

namespace Excalibur.Dispatch.Observability.Metrics;

/// <summary>
/// Middleware that provides distributed tracing for message processing using OpenTelemetry.
/// </summary>
/// <remarks>
/// <para>
/// This middleware creates spans for each message processed through the dispatch pipeline.
/// It uses the <see cref="DispatchActivitySource"/> to create activities that can be
/// exported to OpenTelemetry-compatible backends.
/// </para>
/// <para>
/// To enable tracing, add the Dispatch activity source to your OpenTelemetry configuration:
/// <code>
/// services.AddOpenTelemetry()
///     .WithTracing(tracing => tracing.AddSource("Excalibur.Dispatch"));
/// </code>
/// </para>
/// </remarks>
/// <param name="sanitizer">The telemetry sanitizer for PII protection.</param>
[AppliesTo(MessageKinds.All)]
public sealed class TracingMiddleware(ITelemetrySanitizer sanitizer) : IDispatchMiddleware
{
	private readonly ITelemetrySanitizer _sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

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

		var messageType = message.GetType();
		using var activity = DispatchActivitySource.Instance.StartActivity(
			$"dispatch.{messageType.Name}",
			ActivityKind.Internal);

		if (activity is null)
		{
			// No listener registered, skip tracing overhead
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Set standard attributes
		_ = activity.SetTag("message.type", messageType.Name);
		_ = activity.SetTag("message.id", context.MessageId ?? string.Empty);
		_ = activity.SetTag("dispatch.correlation.id", context.CorrelationId ?? string.Empty);
		_ = activity.SetTag("dispatch.operation", "handle");

		// Add handler type if available
		if (context.GetItem<Type>("HandlerType") is { } handlerType)
		{
			_ = activity.SetTag("handler.type", handlerType.Name);
		}

		// Add message kind
		var messageKind = message switch
		{
			IDispatchAction => "Action",
			IDispatchEvent => "Event",
			IDispatchDocument => "Document",
			_ => "Message"
		};
		_ = activity.SetTag("message.kind", messageKind);

		try
		{
			var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

			if (result.IsSuccess)
			{
				_ = activity.SetStatus(ActivityStatusCode.Ok);
				_ = activity.SetTag("dispatch.status", "success");
			}
			else
			{
				_ = activity.SetStatus(ActivityStatusCode.Error, result.ProblemDetails?.Detail ?? "Message processing failed");
				_ = activity.SetTag("dispatch.status", "failed");

				if (result.ProblemDetails is { } pd)
				{
					_ = activity.SetTag("error.type", pd.Type ?? "unknown");
					_ = activity.SetTag("error.code", pd.ErrorCode);
				}
			}

			return result;
		}
		catch (Exception ex)
		{
			activity.SetSanitizedErrorStatus(ex, _sanitizer);
			_ = activity.SetTag("dispatch.status", "exception");
			throw;
		}
	}
}
