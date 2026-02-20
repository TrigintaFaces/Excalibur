// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// AWS SQS-specific CloudEvent configuration options.
/// </summary>
public sealed class AwsSqsCloudEventOptions
{
	/// <summary>
	/// Gets or sets the maximum batch size for SQS CloudEvent operations.
	/// </summary>
	/// <remarks> AWS SQS supports up to 10 messages per batch. This option allows configuring smaller batches if needed. </remarks>
	/// <value>
	/// The maximum batch size for SQS CloudEvent operations.
	/// </value>
	public int MaxBatchSize { get; set; } = 10;

	/// <summary>
	/// Gets or sets a value indicating whether to use FIFO queue features for CloudEvents.
	/// </summary>
	/// <remarks> When enabled, CloudEvents will include message group IDs and deduplication IDs for FIFO queues. </remarks>
	/// <value>
	/// A value indicating whether to use FIFO queue features for CloudEvents.
	/// </value>
	public bool UseFifoFeatures { get; set; }

	/// <summary>
	/// Gets or sets the default message group ID for FIFO queues.
	/// </summary>
	/// <value>
	/// The default message group ID for FIFO queues.
	/// </value>
	public string? DefaultMessageGroupId { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable content-based deduplication for FIFO queues.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable content-based deduplication for FIFO queues.
	/// </value>
	public bool EnableContentBasedDeduplication { get; set; }

	/// <summary>
	/// Gets or sets the delay for scheduled CloudEvents in seconds.
	/// </summary>
	/// <remarks> AWS SQS supports up to 15 minutes (900 seconds) delay. Use 0 for immediate delivery. </remarks>
	/// <value>
	/// The delay for scheduled CloudEvents in seconds.
	/// </value>
	public int DelaySeconds { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to compress large CloudEvent payloads.
	/// </summary>
	/// <remarks> SQS has a 256KB message size limit. Compression can help fit larger CloudEvents within this limit. </remarks>
	/// <value>
	/// A value indicating whether to compress large CloudEvent payloads.
	/// </value>
	public bool EnablePayloadCompression { get; set; }

	/// <summary>
	/// Gets or sets the threshold (in bytes) for triggering payload compression.
	/// </summary>
	/// <value>
	/// The threshold (in bytes) for triggering payload compression.
	/// </value>
	public int CompressionThreshold { get; set; } = 64 * 1024; // 64KB

	/// <summary>
	/// Gets or sets a value indicating whether DoD validation extensions should be applied.
	/// </summary>
	/// <value>
	/// A value indicating whether DoD validation extensions should be applied.
	/// </value>
	public bool EnableDoDCompliance { get; set; }
}
