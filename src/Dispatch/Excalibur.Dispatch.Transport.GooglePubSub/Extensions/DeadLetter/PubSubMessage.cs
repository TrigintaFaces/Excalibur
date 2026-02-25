// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Represents a Pub/Sub message.
/// </summary>
public sealed class PubSubMessage
{
	/// <summary>
	/// Gets or sets the message ID.
	/// </summary>
	/// <value>
	/// The message ID.
	/// </value>
	public string MessageId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the message data.
	/// </summary>
	/// <value>
	/// The message data.
	/// </value>
	public byte[] Data { get; set; } = [];

	/// <summary>
	/// Gets or sets the message attributes.
	/// </summary>
	/// <value>
	/// The message attributes.
	/// </value>
	public Dictionary<string, string> Attributes { get; set; } = [];

	/// <summary>
	/// Gets or sets the publish time.
	/// </summary>
	/// <value>
	/// The publish time.
	/// </value>
	public DateTimeOffset PublishTime { get; set; }

	/// <summary>
	/// Gets or sets the acknowledgment ID.
	/// </summary>
	/// <value>
	/// The acknowledgment ID.
	/// </value>
	public string AckId { get; set; } = string.Empty;
}
