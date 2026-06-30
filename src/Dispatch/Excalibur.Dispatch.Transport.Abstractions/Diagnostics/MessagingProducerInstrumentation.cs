// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

namespace Excalibur.Dispatch.Transport.Diagnostics;

/// <summary>
/// Shared, fail-open producer-side OpenTelemetry instrumentation for transport publish paths.
/// </summary>
/// <remarks>
/// <para>
/// The message-bus publish paths (RabbitMQ, Kafka, Azure Service Bus, AWS SQS/SNS, Google Pub/Sub)
/// use this helper to start a single <see cref="ActivityKind.Producer"/> span per publish, tagged with
/// the OpenTelemetry <c>messaging.*</c> semantic conventions
/// (<see cref="TransportTelemetryConstants.MessagingConventions"/>). Centralizing the attribute
/// vocabulary here keeps it identical across every transport (no per-transport copy-paste of literal
/// attribute keys).
/// </para>
/// <para>
/// All spans are emitted from the single <see cref="ActivitySource"/> named <see cref="ActivitySourceName"/>;
/// consumers register that source name with their tracer provider to receive producer spans.
/// </para>
/// <para>
/// Instrumentation is strictly fail-open: a failure to start or tag a span never propagates onto the
/// publish hot path, so telemetry can never break message publishing.
/// </para>
/// </remarks>
internal static class MessagingProducerInstrumentation
{
	/// <summary>
	/// The <see cref="ActivitySource"/> name used for transport producer spans. Register this name with
	/// the tracer provider (e.g. <c>AddSource</c>) to receive producer-side publish spans.
	/// </summary>
	public const string ActivitySourceName = "Excalibur.Dispatch.Transport.Producer";

	/// <summary>
	/// Process-lifetime singleton activity source for producer spans. Do not dispose.
	/// </summary>
	private static readonly ActivitySource Source = new(ActivitySourceName);

	/// <summary>
	/// Starts a producer span for a publish operation, tagged with the OpenTelemetry messaging
	/// semantic conventions. Returns <see langword="null"/> when no listener is interested or when
	/// instrumentation fails (fail-open).
	/// </summary>
	/// <param name="messagingSystem">
	/// The messaging system identifier (e.g. <see cref="TransportTelemetryConstants.MessagingConventions.Systems.RabbitMq"/>).
	/// </param>
	/// <param name="destination">The destination name (queue, topic, exchange, or ARN).</param>
	/// <param name="messageId">The message identifier, when available; otherwise <see langword="null"/>.</param>
	/// <returns>The started producer <see cref="Activity"/>, or <see langword="null"/>.</returns>
	public static Activity? StartPublishActivity(string messagingSystem, string? destination, string? messageId = null)
	{
		try
		{
			var activity = Source.StartActivity("publish", ActivityKind.Producer);
			if (activity is null)
			{
				return null;
			}

			_ = activity.SetTag(TransportTelemetryConstants.MessagingConventions.System, messagingSystem);
			_ = activity.SetTag(
				TransportTelemetryConstants.MessagingConventions.Operation,
				TransportTelemetryConstants.MessagingConventions.OperationPublish);

			if (!string.IsNullOrEmpty(destination))
			{
				_ = activity.SetTag(TransportTelemetryConstants.MessagingConventions.DestinationName, destination);
			}

			if (!string.IsNullOrEmpty(messageId))
			{
				_ = activity.SetTag(TransportTelemetryConstants.MessagingConventions.MessageId, messageId);
			}

			return activity;
		}
		catch (Exception)
		{
			// Fail-open: instrumentation must never break the publish hot path.
			return null;
		}
	}
}
