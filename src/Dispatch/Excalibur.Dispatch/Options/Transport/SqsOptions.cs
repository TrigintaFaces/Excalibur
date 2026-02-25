// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Transport;

/// <summary>
/// Options for AWS SQS transport.
/// </summary>
public sealed class SqsOptions
{
	/// <summary>
	/// Gets or sets the queue URL.
	/// </summary>
	/// <value> The SQS queue endpoint used for operations. </value>
	public Uri? QueueUrl { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of messages to receive at once.
	/// </summary>
	/// <value> The batch size requested during each receive call. </value>
	public int MaxNumberOfMessages { get; set; } = 10;

	/// <summary>
	/// Gets or sets the visibility timeout in seconds.
	/// </summary>
	/// <value> The duration messages remain invisible after being received. </value>
	public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the wait time for long polling in seconds.
	/// </summary>
	/// <value> The long-polling wait time applied to receive requests. </value>
	public int WaitTimeSeconds { get; set; } = 20;

	/// <summary>
	/// Gets or sets the maximum concurrency for message processing.
	/// </summary>
	/// <value> The number of parallel message handlers allowed. </value>
	public int MaxConcurrency { get; set; } = 10;

	/// <summary>
	/// Gets or sets the AWS region.
	/// </summary>
	/// <value> The AWS region hosting the SQS queue. </value>
	public string Region { get; set; } = "us-east-1";
}
