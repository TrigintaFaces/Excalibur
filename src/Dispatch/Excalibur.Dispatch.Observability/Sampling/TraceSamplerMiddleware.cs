// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

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
/// </remarks>
/// <param name="sampler">The trace sampler to use for sampling decisions.</param>
[AppliesTo(MessageKinds.All)]
public sealed class TraceSamplerMiddleware(ITraceSampler sampler) : IDispatchMiddleware
{
	private readonly ITraceSampler _sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Start;

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

		var currentActivity = Activity.Current;
		var activityContext = currentActivity?.Context ?? default;

		if (!_sampler.ShouldSample(activityContext, $"dispatch.{message.GetType().Name}"))
		{
			// Suppress tracing for this message by setting a context flag
			context.SetItem("dispatch.trace.sampled", false);
		}

		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}
}
