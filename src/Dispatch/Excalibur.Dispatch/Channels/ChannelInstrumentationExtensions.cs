// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;

using Excalibur.Dispatch.Diagnostics;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Extension methods for channel instrumentation and activity tracking.
/// </summary>
public static class ChannelInstrumentationExtensions
{
	private static readonly ActivitySource ActivitySource = new(DispatchTelemetryConstants.ActivitySources.ChannelTransport);

	/// <summary>
	/// Begins a new activity for tracing channel operations.
	/// </summary>
	/// <param name="operationName"> The name of the operation being performed. </param>
	/// <returns> An Activity instance that should be disposed when the operation completes. </returns>
	public static Activity? BeginActivity(string operationName) => ActivitySource.StartActivity(operationName);
}
