// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Caching.Diagnostics;

/// <summary>
/// Shared telemetry constants for the Excalibur.Caching package.
/// All caching components MUST use these constants for Meter and ActivitySource names
/// to ensure a single, consolidated telemetry surface.
/// </summary>
public static class CachingTelemetryConstants
{
	/// <summary>
	/// The shared Meter name for all Excalibur.Caching telemetry.
	/// </summary>
	public const string MeterName = "Excalibur.Caching";

	/// <summary>
	/// The shared ActivitySource name for all Excalibur.Caching telemetry.
	/// </summary>
	public const string ActivitySourceName = "Excalibur.Caching";

	/// <summary>
	/// Version string for telemetry instruments.
	/// </summary>
	public const string Version = "1.0.0";
}
