// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

namespace Excalibur.Outbox.Diagnostics;

/// <summary>
/// ActivitySource for distributed tracing of background processing services.
/// </summary>
/// <remarks>
/// <para>
/// This class provides a shared <see cref="ActivitySource"/> for tracing background
/// processor operations (outbox, inbox). Activities follow OpenTelemetry semantic
/// conventions and are registered under the <c>Excalibur.BackgroundServices</c> source.
/// </para>
/// <para>
/// To enable collection, register the source with your OpenTelemetry provider:
/// <code>
/// builder.Services.AddOpenTelemetry()
///     .WithTracing(t => t.AddSource(BackgroundServiceActivitySource.SourceName));
/// </code>
/// </para>
/// </remarks>
public static class BackgroundServiceActivitySource
{
	/// <summary>
	/// The activity source name for background service tracing.
	/// </summary>
	public const string SourceName = "Excalibur.Dispatch.BackgroundServices";

	/// <summary>
	/// The activity source version.
	/// </summary>
	public const string SourceVersion = "1.0.0";

	private static readonly ActivitySource Source = new(SourceName, SourceVersion);

	/// <summary>
	/// Starts an activity for a processing cycle.
	/// </summary>
	/// <param name="serviceType">The service type (e.g., "outbox", "inbox").</param>
	/// <param name="operation">The operation type (e.g., "pending", "scheduled", "retry").</param>
	/// <returns>The started activity, or null if no listeners are registered.</returns>
	public static Activity? StartProcessingCycle(string serviceType, string operation)
	{
		var activity = Source.StartActivity(
			$"{serviceType}.{operation}",
			ActivityKind.Internal);

		_ = (activity?.SetTag("service.type", serviceType));
		_ = (activity?.SetTag("operation", operation));

		return activity;
	}

	/// <summary>
	/// Starts an activity for a drain operation during shutdown.
	/// </summary>
	/// <param name="serviceType">The service type (e.g., "outbox", "inbox").</param>
	/// <returns>The started activity, or null if no listeners are registered.</returns>
	public static Activity? StartDrain(string serviceType)
	{
		var activity = Source.StartActivity(
			$"{serviceType}.drain",
			ActivityKind.Internal);

		_ = (activity?.SetTag("service.type", serviceType));

		return activity;
	}
}
