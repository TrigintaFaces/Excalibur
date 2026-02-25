// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Specifies the starting position for event processing.
/// </summary>
public enum EventHubStartingPosition
{
	/// <summary>
	/// Start processing from the earliest available event.
	/// </summary>
	Earliest = 0,

	/// <summary>
	/// Start processing from the latest event.
	/// </summary>
	Latest = 1,

	/// <summary>
	/// Start processing from a specific date/time.
	/// </summary>
	FromTimestamp = 2,
}
