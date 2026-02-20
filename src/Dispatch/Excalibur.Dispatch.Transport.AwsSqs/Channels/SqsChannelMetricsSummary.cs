// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Summary of SQS channel metrics.
/// </summary>
public sealed class SqsChannelMetricsSummary
{
	public Dictionary<string, QueueMetricsSnapshot> QueueMetrics { get; init; } = [];
}
