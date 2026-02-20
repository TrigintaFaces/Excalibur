// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Configuration options for AWS SQS integration.
/// </summary>
public sealed class AwsSqsOptions : AwsProviderOptions
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSqsOptions"/> class.
	/// </summary>
	public AwsSqsOptions()
	{
		WaitTimeSeconds = TimeSpan.FromSeconds(20);
		VisibilityTimeout = TimeSpan.FromSeconds(30);
	}

	/// <summary>
	/// Gets or sets the URL of the SQS queue.
	/// </summary>
	/// <value>
	/// The URL of the SQS queue.
	/// </value>
	public Uri? QueueUrl { get; set; }

	/// <summary>
	/// Gets or sets the message retention period in seconds.
	/// </summary>
	/// <value>
	/// The message retention period in seconds.
	/// </value>
	public int MessageRetentionPeriod { get; set; } = 345600; // 4 days

	/// <summary>
	/// Gets or sets a value indicating whether to use FIFO queues.
	/// </summary>
	/// <value>
	/// A value indicating whether to use FIFO queues.
	/// </value>
	public bool UseFifoQueue { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether content-based deduplication is enabled.
	/// </summary>
	/// <value>
	/// A value indicating whether content-based deduplication is enabled.
	/// </value>
	public bool ContentBasedDeduplication { get; set; }

	/// <summary>
	/// Gets or sets the batch configuration.
	/// </summary>
	/// <value>
	/// The batch configuration.
	/// </value>
	public BatchConfiguration? BatchConfig { get; set; }

	/// <summary>
	/// Gets or sets the long polling configuration.
	/// </summary>
	/// <value>
	/// The long polling configuration.
	/// </value>
	public LongPollingConfiguration? LongPollingConfig { get; set; }

	/// <summary>
	/// Gets or sets the KMS master key ID for encryption.
	/// </summary>
	/// <value>
	/// The KMS master key ID for encryption.
	/// </value>
	public string? KmsMasterKeyId { get; set; }

	/// <summary>
	/// Gets or sets the KMS data key reuse period in seconds.
	/// </summary>
	/// <value>
	/// The KMS data key reuse period in seconds.
	/// </value>
	public int KmsDataKeyReusePeriodSeconds { get; set; } = 300;
}
