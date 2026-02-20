// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

// Interface moved to benchmarks.Performance.Polling

/// <summary>
/// Result of a long polling operation.
/// </summary>
public sealed class LongPollingResult
{
	public int MessageCount { get; set; }

	public TimeSpan ElapsedTime { get; set; }

	public bool IsEmpty => MessageCount == 0;

	public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
