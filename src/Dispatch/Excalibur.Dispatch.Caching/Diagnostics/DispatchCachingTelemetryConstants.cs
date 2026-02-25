// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Caching.Diagnostics;

/// <summary>
/// Shared telemetry constants for the Excalibur.Dispatch.Caching package.
/// All Dispatch caching components MUST use these constants for Meter and ActivitySource names
/// to ensure a single, consolidated telemetry surface.
/// </summary>
public static class DispatchCachingTelemetryConstants
{
	/// <summary>
	/// The shared Meter name for all Dispatch caching telemetry.
	/// </summary>
	public const string MeterName = "Excalibur.Dispatch.Caching";

	/// <summary>
	/// The shared ActivitySource name for all Dispatch caching telemetry.
	/// </summary>
	public const string ActivitySourceName = "Excalibur.Dispatch.Caching";

	/// <summary>
	/// Version string for telemetry instruments.
	/// </summary>
	public const string Version = "1.0.0";
}
