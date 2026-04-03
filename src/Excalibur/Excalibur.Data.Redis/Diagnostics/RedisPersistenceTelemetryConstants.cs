// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Redis.Diagnostics;

/// <summary>
/// Shared telemetry constants for Redis persistence instrumentation.
/// </summary>
internal static class RedisPersistenceTelemetryConstants
{
	/// <summary>
	/// The meter name for Redis persistence metrics.
	/// </summary>
	public const string MeterName = "Excalibur.Data.Redis.Persistence";

	/// <summary>
	/// The version string for telemetry instruments.
	/// </summary>
	public const string Version = "1.0.0";
}
