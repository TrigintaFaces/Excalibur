// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Common polling statistics for transport providers.
/// </summary>
public sealed class TransportPollingStatistics
{
	public int TotalPolls { get; set; }

	public int TotalMessages { get; set; }

	public int TotalErrors { get; set; }

	public TimeSpan TotalDuration { get; set; }
}
