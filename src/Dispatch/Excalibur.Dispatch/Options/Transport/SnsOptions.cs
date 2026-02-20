// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Transport;

/// <summary>
/// Options for AWS SNS transport.
/// </summary>
public sealed class SnsOptions
{
	/// <summary>
	/// Gets or sets the topic ARN.
	/// </summary>
	/// <value> The Amazon Resource Name of the SNS topic. </value>
	public string TopicArn { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the AWS region.
	/// </summary>
	/// <value> The AWS region where the SNS topic resides. </value>
	public string Region { get; set; } = "us-east-1";

	/// <summary>
	/// Gets or sets a value indicating whether to enable message deduplication.
	/// </summary>
	/// <value> <see langword="true" /> to enable payload deduplication; otherwise, <see langword="false" />. </value>
	public bool EnableDeduplication { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use FIFO topic.
	/// </summary>
	/// <value> <see langword="true" /> to use FIFO topics; otherwise, <see langword="false" />. </value>
	public bool UseFifo { get; set; }

	/// <summary>
	/// Gets or sets the message group ID for FIFO topics.
	/// </summary>
	/// <value> The message group identifier used when publishing to FIFO topics. </value>
	public string MessageGroupId { get; set; } = "default";
}
