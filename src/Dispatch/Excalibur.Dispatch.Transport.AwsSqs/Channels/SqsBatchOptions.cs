// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Options for SQS batch processor.
/// </summary>
public sealed class SqsBatchOptions
{
	public Uri? QueueUrl { get; set; }

	public int MaxConcurrentReceiveBatches { get; set; } = 10;

	public int MaxConcurrentSendBatches { get; set; } = 10;

	public int LongPollingSeconds { get; set; } = 20;

	public int VisibilityTimeout { get; set; } = 300;

	public int BatchFlushIntervalMs { get; set; } = 100;
}
