// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Options for complete SQS channel infrastructure.
/// </summary>
public sealed class SqsChannelInfrastructureOptions
{
	/// <summary>
	/// Common options.
	/// </summary>
	public Uri? QueueUrl { get; set; }

	public Uri? ServiceUrl { get; set; }

	public int VisibilityTimeout { get; set; } = 300;

	/// <summary>
	/// Channel adapter options.
	/// </summary>
	public int ConcurrentPollers { get; set; } = 10;

	public int MaxConcurrentPollers { get; set; } = 20;

	public int ReceiveChannelCapacity { get; set; } = 1000;

	public int BatchIntervalMs { get; set; } = 100;

	/// <summary>
	/// Message processor options.
	/// </summary>
	public int ProcessorCount { get; set; } = 10;

	public int MaxConcurrentMessages { get; set; } = 100;

	public int DeleteBatchIntervalMs { get; set; } = 100;

	/// <summary>
	/// Batch processor options.
	/// </summary>
	public int MaxConcurrentReceiveBatches { get; set; } = 10;

	public int MaxConcurrentSendBatches { get; set; } = 10;

	public int LongPollingSeconds { get; set; } = 20;

	public int BatchFlushIntervalMs { get; set; } = 100;
}
