// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.Transport.Decorators;
using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Builders;

/// <summary>
/// Extension methods for <see cref="TransportSenderBuilder"/> to add common decorators.
/// </summary>
public static class TransportSenderBuilderExtensions
{
	/// <summary>
	/// Adds OpenTelemetry telemetry (metrics + tracing) to the sender pipeline.
	/// </summary>
	/// <param name="builder">The sender builder.</param>
	/// <param name="transportName">The transport name for tagging (e.g., "Kafka").</param>
	/// <param name="meter">The meter for recording metrics.</param>
	/// <param name="activitySource">The activity source for distributed tracing.</param>
	/// <returns>The builder for chaining.</returns>
	public static TransportSenderBuilder UseTelemetry(
		this TransportSenderBuilder builder,
		string transportName,
		Meter meter,
		ActivitySource activitySource) =>
		builder.Use(inner => new TelemetryTransportSender(inner, meter, activitySource, transportName));

	/// <summary>
	/// Adds message ordering to the sender pipeline.
	/// Sets <see cref="TransportTelemetryConstants.PropertyKeys.OrderingKey"/> on each message.
	/// </summary>
	/// <param name="builder">The sender builder.</param>
	/// <param name="keySelector">A function that extracts the ordering key from a message.</param>
	/// <returns>The builder for chaining.</returns>
	public static TransportSenderBuilder UseOrdering(
		this TransportSenderBuilder builder,
		Func<TransportMessage, string?> keySelector) =>
		builder.Use(inner => new OrderingTransportSender(inner, keySelector));

	/// <summary>
	/// Adds message deduplication to the sender pipeline.
	/// Sets <see cref="TransportTelemetryConstants.PropertyKeys.DeduplicationId"/> on each message.
	/// </summary>
	/// <param name="builder">The sender builder.</param>
	/// <param name="idSelector">A function that generates the deduplication ID from a message.</param>
	/// <returns>The builder for chaining.</returns>
	public static TransportSenderBuilder UseDeduplication(
		this TransportSenderBuilder builder,
		Func<TransportMessage, string?> idSelector) =>
		builder.Use(inner => new DeduplicationTransportSender(inner, idSelector));

	/// <summary>
	/// Adds message scheduling to the sender pipeline.
	/// Sets <see cref="TransportTelemetryConstants.PropertyKeys.ScheduledTime"/> on each message.
	/// </summary>
	/// <param name="builder">The sender builder.</param>
	/// <param name="timeSelector">A function that determines the scheduled delivery time from a message.</param>
	/// <returns>The builder for chaining.</returns>
	public static TransportSenderBuilder UseScheduling(
		this TransportSenderBuilder builder,
		Func<TransportMessage, DateTimeOffset?> timeSelector) =>
		builder.Use(inner => new SchedulingTransportSender(inner, timeSelector));

	/// <summary>
	/// Adds CloudEvents encoding to the sender pipeline.
	/// Converts outgoing messages to CloudEvents format before sending.
	/// </summary>
	/// <param name="builder">The sender builder.</param>
	/// <param name="mapper">The CloudEvent mapper for converting messages.</param>
	/// <param name="cloudEventFactory">A factory that creates a <see cref="CloudEvent"/> from a <see cref="TransportMessage"/>.</param>
	/// <returns>The builder for chaining.</returns>
	public static TransportSenderBuilder UseCloudEvents(
		this TransportSenderBuilder builder,
		ICloudEventMapper<TransportMessage> mapper,
		Func<TransportMessage, CloudEvent> cloudEventFactory) =>
		builder.Use(inner => new CloudEventsTransportSender(inner, mapper, cloudEventFactory));
}
