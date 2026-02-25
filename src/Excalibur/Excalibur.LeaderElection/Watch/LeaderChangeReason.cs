// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.LeaderElection.Watch;

/// <summary>
/// Specifies the reason for a leader change event.
/// </summary>
public enum LeaderChangeReason
{
	/// <summary>
	/// A new leader was elected through the normal election process.
	/// </summary>
	Elected = 0,

	/// <summary>
	/// The previous leader's lease expired without renewal.
	/// </summary>
	Expired = 1,

	/// <summary>
	/// The previous leader voluntarily resigned leadership.
	/// </summary>
	Resigned = 2,

	/// <summary>
	/// The leader was forced out due to a health check failure.
	/// </summary>
	HealthCheckFailed = 3,
}
