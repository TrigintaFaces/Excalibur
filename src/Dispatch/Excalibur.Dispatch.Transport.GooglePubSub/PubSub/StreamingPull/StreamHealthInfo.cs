// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Health information for a single stream.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="StreamHealthInfo" /> class. </remarks>
/// <param name="streamId"> The stream identifier. </param>
public sealed class StreamHealthInfo(string streamId)
{
	/// <summary>
	/// Gets the stream identifier.
	/// </summary>
	/// <value>
	/// The stream identifier.
	/// </value>
	public string StreamId { get; } = streamId ?? throw new ArgumentNullException(nameof(streamId));

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether the stream is connected.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether the stream is connected.
	/// </value>
	public bool IsConnected { get; set; }

	/// <summary>
	/// Gets or sets the time the stream connected.
	/// </summary>
	/// <value>
	/// The time the stream connected.
	/// </value>
	public DateTimeOffset ConnectedTime { get; set; }

	/// <summary>
	/// Gets or sets the time the stream disconnected.
	/// </summary>
	/// <value>
	/// The time the stream disconnected.
	/// </value>
	public DateTimeOffset DisconnectedTime { get; set; }

	/// <summary>
	/// Gets or sets the last time a message was received.
	/// </summary>
	/// <value>
	/// The last time a message was received.
	/// </value>
	public DateTimeOffset LastMessageTime { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the last time an error occurred.
	/// </summary>
	/// <value>
	/// The last time an error occurred.
	/// </value>
	public DateTimeOffset LastErrorTime { get; set; }

	/// <summary>
	/// Gets or sets the last error that occurred.
	/// </summary>
	/// <value>
	/// The last error that occurred.
	/// </value>
	public Exception? LastError { get; set; }

	/// <summary>
	/// Gets or sets the number of messages received.
	/// </summary>
	/// <value>
	/// The number of messages received.
	/// </value>
	public long MessagesReceived { get; set; }

	/// <summary>
	/// Gets or sets the number of bytes received.
	/// </summary>
	/// <value>
	/// The number of bytes received.
	/// </value>
	public long BytesReceived { get; set; }

	/// <summary>
	/// Gets or sets the number of errors encountered.
	/// </summary>
	/// <value>
	/// The number of errors encountered.
	/// </value>
	public long ErrorCount { get; set; }

	/// <summary>
	/// Gets or sets the number of successful acknowledgments.
	/// </summary>
	/// <value>
	/// The number of successful acknowledgments.
	/// </value>
	public long AcknowledgmentsSucceeded { get; set; }

	/// <summary>
	/// Gets or sets the number of failed acknowledgments.
	/// </summary>
	/// <value>
	/// The number of failed acknowledgments.
	/// </value>
	public long AcknowledgmentsFailed { get; set; }

	/// <summary>
	/// Gets or sets the number of times the stream has reconnected.
	/// </summary>
	/// <value>
	/// The number of times the stream has reconnected.
	/// </value>
	public int ReconnectCount { get; set; }
}
