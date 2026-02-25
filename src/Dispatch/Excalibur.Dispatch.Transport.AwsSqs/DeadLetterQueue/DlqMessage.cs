// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Represents a message in the dead letter queue.
/// </summary>
public sealed class DlqMessage
{
	/// <summary>
	/// Gets or sets the message ID.
	/// </summary>
	/// <value>
	/// The message ID.
	/// </value>
	public required string MessageId { get; set; }

	/// <summary>
	/// Gets or sets the message body.
	/// </summary>
	/// <value>
	/// The message body.
	/// </value>
	public required string Body { get; set; }

	/// <summary>
	/// Gets or sets the receipt handle for the message.
	/// </summary>
	/// <value>
	/// The receipt handle for the message.
	/// </value>
	public string? ReceiptHandle { get; set; }

	/// <summary>
	/// Gets or sets the source queue URL.
	/// </summary>
	/// <value>
	/// The source queue URL.
	/// </value>
	public Uri? SourceQueueUrl { get; set; }

	/// <summary>
	/// Gets or sets the number of processing attempts.
	/// </summary>
	/// <value>
	/// The number of processing attempts.
	/// </value>
	public int AttemptCount { get; set; }

	/// <summary>
	/// Gets or sets when the message was first sent.
	/// </summary>
	/// <value>
	/// When the message was first sent.
	/// </value>
	public DateTime FirstSentTimestamp { get; set; }

	/// <summary>
	/// Gets or sets when the message was moved to DLQ.
	/// </summary>
	/// <value>
	/// When the message was moved to DLQ.
	/// </value>
	public DateTime? MovedToDlqTimestamp { get; set; }

	/// <summary>
	/// Gets the message attributes.
	/// </summary>
	/// <value>
	/// The message attributes.
	/// </value>
	public Dictionary<string, string> Attributes { get; } = [];

	/// <summary>
	/// Gets the message metadata.
	/// </summary>
	/// <value>
	/// The message metadata.
	/// </value>
	public Dictionary<string, object> Metadata { get; } = [];

	/// <summary>
	/// Gets or sets the last error message.
	/// </summary>
	/// <value>
	/// The last error message.
	/// </value>
	public string? LastError { get; set; }

	/// <summary>
	/// Gets or sets the reason for DLQ placement.
	/// </summary>
	/// <value>
	/// The reason for DLQ placement.
	/// </value>
	public string? DlqReason { get; set; }
}
