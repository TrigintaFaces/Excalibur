// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Provides telemetry operations for Google Cloud Pub/Sub.
/// </summary>
public static class PubSubTelemetry
{
	/// <summary>
	/// Creates a meter for custom metrics.
	/// </summary>
	/// <param name="name"> Meter name. </param>
	/// <param name="version"> Meter version. </param>
	/// <returns> A configured meter. </returns>
	public static Meter CreatePubSubMeter(string name, string version = "1.0.0") =>
		new($"Excalibur.Dispatch.Transport.GooglePubSub.PubSub.{name}", version);

	/// <summary>
	/// Creates an activity source for custom tracing.
	/// </summary>
	/// <param name="name"> Source name. </param>
	/// <param name="version"> Source version. </param>
	/// <returns> A configured activity source. </returns>
	public static ActivitySource CreatePubSubActivitySource(string name, string version = "1.0.0") =>
		new($"Excalibur.Dispatch.Transport.GooglePubSub.PubSub.{name}", version);

	/// <summary>
	/// Formats a subscription name for telemetry.
	/// </summary>
	/// <param name="subscription"> The subscription name. </param>
	/// <returns> A formatted subscription name. </returns>
	public static string FormatSubscriptionName(string subscription)
	{
		// Extract just the subscription name from the full resource path
		// Format: projects/{project}/subscriptions/{subscription}
		var parts = subscription.Split('/');
		return parts.Length >= 4 ? parts[3] : subscription;
	}

	/// <summary>
	/// Formats a topic name for telemetry.
	/// </summary>
	/// <param name="topic"> The topic name. </param>
	/// <returns> A formatted topic name. </returns>
	public static string FormatTopicName(string topic)
	{
		// Extract just the topic name from the full resource path
		// Format: projects/{project}/topics/{topic}
		var parts = topic.Split('/');
		return parts.Length >= 4 ? parts[3] : topic;
	}
}
