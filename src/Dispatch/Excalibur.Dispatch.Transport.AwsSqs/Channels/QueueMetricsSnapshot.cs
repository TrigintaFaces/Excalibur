// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Snapshot of queue metrics.
/// </summary>
public sealed class QueueMetricsSnapshot
{
	public long MessagesReceived { get; init; }

	public long MessagesSent { get; init; }

	public long Errors { get; init; }

	public double AverageReceiveTime { get; init; }

	public double AverageSendTime { get; init; }
}
