// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport.Decorators;

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
}
