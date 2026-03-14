// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Options for complete SQS channel infrastructure.
/// </summary>
public sealed class SqsChannelInfrastructureOptions
{
	/// <summary>
	/// Gets or sets the SQS queue URL.
	/// </summary>
	public Uri? QueueUrl { get; set; }

	/// <summary>
	/// Gets or sets the custom service URL for the SQS endpoint.
	/// </summary>
	public Uri? ServiceUrl { get; set; }

	/// <summary>
	/// Gets or sets the visibility timeout in seconds.
	/// </summary>
	public int VisibilityTimeout { get; set; } = 300;

	/// <summary>
	/// Gets or sets the channel adapter options for polling and receiving.
	/// </summary>
	public SqsChannelAdapterSubOptions ChannelAdapter { get; set; } = new();

	/// <summary>
	/// Gets or sets the message processing options.
	/// </summary>
	public SqsChannelProcessingSubOptions Processing { get; set; } = new();

	/// <summary>
	/// Gets or sets the batch processing options.
	/// </summary>
	public SqsChannelBatchSubOptions Batch { get; set; } = new();
}

/// <summary>
/// Channel adapter options for SQS polling and receiving.
/// </summary>
public sealed class SqsChannelAdapterSubOptions
{
	/// <summary>
	/// Gets or sets the number of concurrent pollers.
	/// </summary>
	public int ConcurrentPollers { get; set; } = 10;

	/// <summary>
	/// Gets or sets the maximum number of concurrent pollers.
	/// </summary>
	public int MaxConcurrentPollers { get; set; } = 20;

	/// <summary>
	/// Gets or sets the receive channel capacity.
	/// </summary>
	public int ReceiveChannelCapacity { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the batch interval in milliseconds.
	/// </summary>
	public int BatchIntervalMs { get; set; } = 100;
}

/// <summary>
/// Message processing options for SQS channel infrastructure.
/// </summary>
public sealed class SqsChannelProcessingSubOptions
{
	/// <summary>
	/// Gets or sets the number of message processors.
	/// </summary>
	public int ProcessorCount { get; set; } = 10;

	/// <summary>
	/// Gets or sets the maximum number of concurrent messages.
	/// </summary>
	public int MaxConcurrentMessages { get; set; } = 100;

	/// <summary>
	/// Gets or sets the delete batch interval in milliseconds.
	/// </summary>
	public int DeleteBatchIntervalMs { get; set; } = 100;
}

/// <summary>
/// Batch processing options for SQS channel infrastructure.
/// </summary>
public sealed class SqsChannelBatchSubOptions
{
	/// <summary>
	/// Gets or sets the maximum number of concurrent receive batches.
	/// </summary>
	public int MaxConcurrentReceiveBatches { get; set; } = 10;

	/// <summary>
	/// Gets or sets the maximum number of concurrent send batches.
	/// </summary>
	public int MaxConcurrentSendBatches { get; set; } = 10;

	/// <summary>
	/// Gets or sets the long polling wait time in seconds.
	/// </summary>
	public int LongPollingSeconds { get; set; } = 20;

	/// <summary>
	/// Gets or sets the batch flush interval in milliseconds.
	/// </summary>
	public int BatchFlushIntervalMs { get; set; } = 100;
}
