// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Transport.GooglePubSub;

/// <summary>
/// Shared telemetry constants for the Google Pub/Sub transport.
/// All Google Pub/Sub components MUST use these constants for Meter and ActivitySource names
/// to ensure a single, consolidated telemetry surface.
/// </summary>
/// <remarks>
/// <para>
/// Follows the established transport telemetry pattern from
/// <see cref="Transport.Diagnostics.TransportTelemetryConstants"/>,
/// which produces names in the format <c>Excalibur.Dispatch.Transport.{TransportName}</c>.
/// </para>
/// <para>
/// Before this consolidation, the Google Pub/Sub transport had 7 separate Meter instances
/// and 9 separate ActivitySource instances with ad-hoc names. This class unifies them
/// under a single name for consistent telemetry filtering and subscription.
/// </para>
/// <para>
/// Components that accept <see cref="IMeterFactory"/> should use
/// <c>meterFactory?.Create(MeterName) ?? Meter</c> to prefer DI-managed lifecycle
/// while falling back to the shared static instance.
/// </para>
/// </remarks>
public static class GooglePubSubTelemetryConstants
{
	/// <summary>
	/// The shared Meter name for all Google Pub/Sub telemetry.
	/// </summary>
	/// <remarks>
	/// Matches the value produced by
	/// <c>TransportTelemetryConstants.MeterName("GooglePubSub")</c>.
	/// </remarks>
	public const string MeterName = "Excalibur.Dispatch.Transport.GooglePubSub";

	/// <summary>
	/// The shared ActivitySource name for all Google Pub/Sub telemetry.
	/// </summary>
	/// <remarks>
	/// Matches the value produced by
	/// <c>TransportTelemetryConstants.ActivitySourceName("GooglePubSub")</c>.
	/// </remarks>
	public const string ActivitySourceName = "Excalibur.Dispatch.Transport.GooglePubSub";

	/// <summary>
	/// Version string for telemetry instruments.
	/// </summary>
	public const string Version = "1.0.0";

	/// <summary>
	/// Shared process-lifetime <see cref="System.Diagnostics.ActivitySource"/> for Google Pub/Sub tracing.
	/// </summary>
	/// <remarks>
	/// Use this for components that do not have DI-managed lifecycle.
	/// Components with <see cref="IMeterFactory"/> injection should create their own
	/// <see cref="System.Diagnostics.ActivitySource"/> in their constructor.
	/// </remarks>
	public static ActivitySource SharedActivitySource { get; } = new(ActivitySourceName, Version);

	/// <summary>
	/// Shared process-lifetime <see cref="System.Diagnostics.Metrics.Meter"/> for Google Pub/Sub metrics.
	/// </summary>
	/// <remarks>
	/// Use this as a fallback for components that do not receive <see cref="IMeterFactory"/> via DI.
	/// </remarks>
	public static Meter SharedMeter { get; } = new(MeterName, Version);
}
