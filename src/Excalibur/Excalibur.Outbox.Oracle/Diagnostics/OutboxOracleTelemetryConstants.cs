// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Oracle.Diagnostics;

/// <summary>
/// Shared telemetry constants for Oracle outbox store instrumentation.
/// </summary>
internal static class OutboxOracleTelemetryConstants
{
	/// <summary>
	/// The meter name for Oracle outbox metrics.
	/// </summary>
	public const string MeterName = "Excalibur.Outbox.Oracle";

	/// <summary>
	/// The version string for telemetry instruments.
	/// </summary>
	public const string Version = "1.0";
}
