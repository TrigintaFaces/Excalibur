// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Consumer/receiver options for the AWS SQS provider.
/// </summary>
/// <remarks>
/// Follows the <c>AmazonSQSConfig</c> pattern of separating consumer behavior from connection settings.
/// </remarks>
public sealed class AwsSqsConsumerOptions
{
	/// <summary>
	/// Gets or sets the visibility timeout for consumed messages.
	/// </summary>
	/// <value> The visibility timeout for consumed messages. </value>
	public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the long polling wait time.
	/// </summary>
	/// <value> The long polling wait time. </value>
	public TimeSpan WaitTimeSeconds { get; set; } = TimeSpan.FromSeconds(20);

	/// <summary>
	/// Gets or sets the maximum number of messages to receive in a batch.
	/// </summary>
	/// <value> The maximum number of messages to receive in a batch. </value>
	[Range(1, 10)]
	public int MaxNumberOfMessages { get; set; } = 10;
}
