// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport.Decorators;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Builders;

/// <summary>
/// Extension methods for <see cref="TransportSubscriberBuilder"/> to add common decorators.
/// </summary>
public static class TransportSubscriberBuilderExtensions
{
	/// <summary>
	/// Adds OpenTelemetry telemetry (metrics + tracing) to the subscriber pipeline.
	/// Instruments both subscription lifecycle and per-message handler invocations.
	/// </summary>
	/// <param name="builder">The subscriber builder.</param>
	/// <param name="transportName">The transport name for tagging (e.g., "Kafka").</param>
	/// <param name="meter">The meter for recording metrics.</param>
	/// <param name="activitySource">The activity source for distributed tracing.</param>
	/// <returns>The builder for chaining.</returns>
	public static TransportSubscriberBuilder UseTelemetry(
		this TransportSubscriberBuilder builder,
		string transportName,
		Meter meter,
		ActivitySource activitySource) =>
		builder.Use(inner => new TelemetryTransportSubscriber(inner, meter, activitySource, transportName));

	/// <summary>
	/// Adds dead letter queue routing to the subscriber pipeline.
	/// Messages for which the handler returns <see cref="MessageAction.Reject"/> are routed to the dead letter handler.
	/// </summary>
	/// <param name="builder">The subscriber builder.</param>
	/// <param name="transportName">The transport provider name for metric tagging (e.g., "Kafka").</param>
	/// <param name="deadLetterHandler">A delegate that routes a message to the dead letter queue.</param>
	/// <param name="meter">Optional meter for recording dead-letter metrics.</param>
	/// <returns>The builder for chaining.</returns>
	public static TransportSubscriberBuilder UseDeadLetterQueue(
		this TransportSubscriberBuilder builder,
		string transportName,
		Func<TransportReceivedMessage, string?, CancellationToken, Task> deadLetterHandler,
		Meter? meter = null) =>
		builder.Use(inner => new DeadLetterTransportSubscriber(inner, deadLetterHandler, transportName, meter));

	/// <summary>
	/// Adds self-healing reconnect/backoff to the subscriber pipeline (bd-no0lue/kxexrz): when the inner
	/// subscription faults with a non-cancellation error the loop backs off and re-subscribes, so a transient
	/// receive/stream fault no longer silently kills the subscriber. Cooperative cancellation propagates and is
	/// never retried.
	/// </summary>
	/// <remarks>
	/// The backoff schedule is supplied as a <see cref="Func{T, TResult}"/> (1-based attempt number → delay)
	/// so this package takes no resilience-library dependency (mirrors <c>UseDeadLetterQueue</c>'s delegate
	/// seam). The DI/transport layer owns the concrete, <b>clamped</b> schedule — e.g. the in-house
	/// <c>ExponentialBackoffCalculator</c> whose <c>MaxDelay</c> caps the wait (bd-7npc0q) — so unbounded
	/// exponential growth can never produce an absurd reconnect delay.
	/// </remarks>
	/// <param name="builder">The subscriber builder.</param>
	/// <param name="backoffDelay">
	/// The backoff schedule: given the 1-based reconnect attempt number, returns the (clamped) delay to wait
	/// before the next re-subscribe. Required — the caller supplies a bounded schedule.
	/// </param>
	/// <param name="loggerFactory">The logger factory used to create the reconnect decorator's logger.</param>
	/// <returns>The builder for chaining.</returns>
	public static TransportSubscriberBuilder UseReconnect(
		this TransportSubscriberBuilder builder,
		Func<int, TimeSpan> backoffDelay,
		ILoggerFactory loggerFactory)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(backoffDelay);
		ArgumentNullException.ThrowIfNull(loggerFactory);

		return builder.Use(inner => new ReconnectingTransportSubscriber(
			inner,
			backoffDelay,
			loggerFactory.CreateLogger<ReconnectingTransportSubscriber>()));
	}
}
