// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Excalibur.EventSourcing.Diagnostics;

/// <summary>
/// Telemetry constants and instruments for event notification and inline projection processing.
/// </summary>
/// <remarks>
/// All metrics use a shared <see cref="Meter"/> created via <see cref="IMeterFactory"/>
/// following the established pattern in this package.
/// </remarks>
internal static class EventNotificationTelemetry
{
	/// <summary>
	/// The ActivitySource name for event notification spans.
	/// </summary>
	internal const string ActivitySourceName = "Excalibur.EventSourcing.Projections";

	/// <summary>
	/// The Meter name for projection metrics.
	/// </summary>
	internal const string MeterName = "Excalibur.EventSourcing.Projections";

	/// <summary>
	/// Shared ActivitySource for event notification and inline projection spans.
	/// </summary>
	internal static readonly ActivitySource Source = new(ActivitySourceName);

	/// <summary>
	/// Counter: total events notified through the broker.
	/// </summary>
	internal const string EventsNotifiedCounterName = "excalibur.event_notification.events_notified";

	/// <summary>
	/// Histogram: duration of the entire notification pipeline (projections + handlers).
	/// </summary>
	internal const string NotificationDurationHistogramName = "excalibur.event_notification.duration";

	/// <summary>
	/// Histogram: duration of applying events to a single projection type.
	/// </summary>
	internal const string ProjectionApplyDurationHistogramName = "excalibur.projection.apply.duration";

	// ========================================
	// Event IDs for LoggerMessage (113200-113299)
	// ========================================

	/// <summary>Event notification started.</summary>
	internal const int EventNotificationStarted = 113200;

	/// <summary>Event notification completed.</summary>
	internal const int EventNotificationCompleted = 113201;

	/// <summary>Inline projection warning threshold exceeded.</summary>
	internal const int InlineProjectionSlowWarning = 113202;

	/// <summary>Inline projection failed.</summary>
	internal const int InlineProjectionFailed = 113203;

	/// <summary>Notification handler failed.</summary>
	internal const int NotificationHandlerFailed = 113204;

	/// <summary>Projection recovery completed.</summary>
	internal const int ProjectionRecoveryCompleted = 113205;
}
