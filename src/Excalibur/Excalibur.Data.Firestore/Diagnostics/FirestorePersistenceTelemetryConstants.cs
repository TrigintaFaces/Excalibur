// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Firestore.Diagnostics;

/// <summary>
/// Shared telemetry constants for Firestore persistence instrumentation.
/// </summary>
public static class FirestorePersistenceTelemetryConstants
{
	/// <summary>
	/// The meter name for Firestore persistence metrics.
	/// </summary>
	public const string MeterName = "Excalibur.Data.Firestore.Persistence";

	/// <summary>
	/// The version string for telemetry instruments.
	/// </summary>
	public const string Version = "1.0.0";
}
