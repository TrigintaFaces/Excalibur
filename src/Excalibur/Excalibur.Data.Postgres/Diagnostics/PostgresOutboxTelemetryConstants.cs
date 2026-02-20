// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Postgres.Diagnostics;

/// <summary>
/// Shared telemetry constants for Postgres outbox store instrumentation.
/// </summary>
public static class PostgresOutboxTelemetryConstants
{
	/// <summary>
	/// The meter name for Postgres outbox metrics.
	/// </summary>
	public const string MeterName = "Excalibur.Data.Postgres.Outbox";

	/// <summary>
	/// The version string for telemetry instruments.
	/// </summary>
	public const string Version = "1.0";
}
