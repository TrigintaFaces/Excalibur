// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Observability.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Observability.Sampling;

/// <summary>
/// Middleware that applies trace sampling decisions to the Dispatch pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Uses the configured <see cref="ITraceSampler"/> to determine whether the current
/// message processing should be traced. When a message is not sampled, the middleware
/// suppresses activity creation for downstream middleware, reducing telemetry overhead.
/// </para>
/// <para>
/// The sampling decision is treated as cross-cutting instrumentation and fails open: if the
/// sampler throws, the failure is logged and dispatch continues unsuppressed (the default
/// "sampled" behavior), matching the Microsoft skip-pattern for optional infrastructure.
/// </para>
/// </remarks>
/// <param name="sampler">The trace sampler to use for sampling decisions.</param>
/// <param name="logger">Logger for diagnostic output.</param>
[AppliesTo(MessageKinds.All)]
internal sealed partial class TraceSamplerMiddleware(ITraceSampler sampler, ILogger<TraceSamplerMiddleware> logger)
	: IDispatchMiddleware
{
	private readonly ITraceSampler _sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
	private readonly ILogger<TraceSamplerMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Start;

	/// <inheritdoc />
	[SuppressMessage("Design", "CA1031:Do not catch general exception types",
		Justification = "Fail-open instrumentation: a sampler failure must not prevent dispatch (Microsoft skip-pattern). Cancellation is rethrown.")]
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		// Instrumentation (the sampling decision) is isolated from the handler call below and
		// fails open: a sampler failure must never prevent dispatch. Cancellation is propagated.
		try
		{
			var currentActivity = Activity.Current;
			var activityContext = currentActivity?.Context ?? default;

			if (!_sampler.ShouldSample(activityContext, $"dispatch.{message.GetType().Name}"))
			{
				// Suppress tracing for this message by setting a context flag
				context.SetItem("dispatch.trace.sampled", false);
			}
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			// Fail open: skip the sampling decision (leave tracing at its default), continue dispatch.
			LogSamplingInstrumentationFailed(ex);
		}

		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}

	[LoggerMessage(ObservabilityEventId.TraceSamplingInstrumentationFailed, LogLevel.Warning,
		"Trace sampling instrumentation failed and was skipped; dispatch continues unsuppressed")]
	private partial void LogSamplingInstrumentationFailed(Exception ex);
}
