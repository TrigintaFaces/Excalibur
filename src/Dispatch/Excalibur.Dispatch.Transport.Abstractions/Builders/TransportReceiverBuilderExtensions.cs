// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport.Decorators;

namespace Excalibur.Dispatch.Transport.Builders;

/// <summary>
/// Extension methods for <see cref="TransportReceiverBuilder"/> to add common decorators.
/// </summary>
public static class TransportReceiverBuilderExtensions
{
	/// <summary>
	/// Adds OpenTelemetry telemetry (metrics + tracing) to the receiver pipeline.
	/// </summary>
	/// <param name="builder">The receiver builder.</param>
	/// <param name="transportName">The transport name for tagging (e.g., "Kafka").</param>
	/// <param name="meter">The meter for recording metrics.</param>
	/// <param name="activitySource">The activity source for distributed tracing.</param>
	/// <returns>The builder for chaining.</returns>
	public static TransportReceiverBuilder UseTelemetry(
		this TransportReceiverBuilder builder,
		string transportName,
		Meter meter,
		ActivitySource activitySource) =>
		builder.Use(inner => new TelemetryTransportReceiver(inner, meter, activitySource, transportName));

	/// <summary>
	/// Adds dead letter queue routing to the receiver pipeline.
	/// Messages rejected with <c>requeue: false</c> are routed to the dead letter handler.
	/// </summary>
	/// <param name="builder">The receiver builder.</param>
	/// <param name="transportName">The transport provider name for metric tagging (e.g., "Kafka").</param>
	/// <param name="deadLetterHandler">A delegate that routes a message to the dead letter queue.</param>
	/// <param name="meter">Optional meter for recording dead-letter metrics.</param>
	/// <returns>The builder for chaining.</returns>
	public static TransportReceiverBuilder UseDeadLetterQueue(
		this TransportReceiverBuilder builder,
		string transportName,
		Func<TransportReceivedMessage, string?, CancellationToken, Task> deadLetterHandler,
		Meter? meter = null) =>
		builder.Use(inner => new DeadLetterTransportReceiver(inner, deadLetterHandler, transportName, meter));

	/// <summary>
	/// Adds CloudEvents detection and unwrapping to the receiver pipeline.
	/// Detects CloudEvents-encoded messages and optionally transforms them.
	/// </summary>
	/// <param name="builder">The receiver builder.</param>
	/// <param name="mapper">The CloudEvent mapper for detecting CloudEvents.</param>
	/// <param name="unwrapper">Optional function to transform messages after CloudEvent detection.</param>
	/// <returns>The builder for chaining.</returns>
	public static TransportReceiverBuilder UseCloudEvents(
		this TransportReceiverBuilder builder,
		ICloudEventMapper<TransportReceivedMessage> mapper,
		Func<TransportReceivedMessage, TransportReceivedMessage>? unwrapper = null) =>
		builder.Use(inner => new CloudEventsTransportReceiver(inner, mapper, unwrapper));
}
