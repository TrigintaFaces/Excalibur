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
	private long _messagesReceived;
	private long _bytesReceived;
	private long _errorCount;
	private long _acknowledgementsSucceeded;
	private long _acknowledgementsFailed;
	private int _reconnectCount;

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
	public long MessagesReceived
	{
		get => Interlocked.Read(ref _messagesReceived);
		set => Interlocked.Exchange(ref _messagesReceived, value);
	}

	/// <summary>
	/// Gets or sets the number of bytes received.
	/// </summary>
	/// <value>
	/// The number of bytes received.
	/// </value>
	public long BytesReceived
	{
		get => Interlocked.Read(ref _bytesReceived);
		set => Interlocked.Exchange(ref _bytesReceived, value);
	}

	/// <summary>
	/// Gets or sets the number of errors encountered.
	/// </summary>
	/// <value>
	/// The number of errors encountered.
	/// </value>
	public long ErrorCount
	{
		get => Interlocked.Read(ref _errorCount);
		set => Interlocked.Exchange(ref _errorCount, value);
	}

	/// <summary>
	/// Gets or sets the number of successful acknowledgments.
	/// </summary>
	/// <value>
	/// The number of successful acknowledgments.
	/// </value>
	public long AcknowledgmentsSucceeded
	{
		get => Interlocked.Read(ref _acknowledgementsSucceeded);
		set => Interlocked.Exchange(ref _acknowledgementsSucceeded, value);
	}

	/// <summary>
	/// Gets or sets the number of failed acknowledgments.
	/// </summary>
	/// <value>
	/// The number of failed acknowledgments.
	/// </value>
	public long AcknowledgmentsFailed
	{
		get => Interlocked.Read(ref _acknowledgementsFailed);
		set => Interlocked.Exchange(ref _acknowledgementsFailed, value);
	}

	/// <summary>
	/// Gets or sets the number of times the stream has reconnected.
	/// </summary>
	/// <value>
	/// The number of times the stream has reconnected.
	/// </value>
	public int ReconnectCount
	{
		get => Interlocked.CompareExchange(ref _reconnectCount, 0, 0);
		set => Interlocked.Exchange(ref _reconnectCount, value);
	}

	/// <summary>Atomically increments <see cref="MessagesReceived"/> by 1.</summary>
	internal void IncrementMessagesReceived() => Interlocked.Increment(ref _messagesReceived);

	/// <summary>Atomically adds <paramref name="bytes"/> to <see cref="BytesReceived"/>.</summary>
	internal void AddBytesReceived(long bytes) => Interlocked.Add(ref _bytesReceived, bytes);

	/// <summary>Atomically increments <see cref="ErrorCount"/> by 1.</summary>
	internal void IncrementErrorCount() => Interlocked.Increment(ref _errorCount);

	/// <summary>Atomically increments <see cref="AcknowledgmentsSucceeded"/> by 1.</summary>
	internal void IncrementAcknowledgmentsSucceeded() => Interlocked.Increment(ref _acknowledgementsSucceeded);

	/// <summary>Atomically increments <see cref="AcknowledgmentsFailed"/> by 1.</summary>
	internal void IncrementAcknowledgmentsFailed() => Interlocked.Increment(ref _acknowledgementsFailed);

	/// <summary>Atomically increments <see cref="ReconnectCount"/> by 1.</summary>
	internal void IncrementReconnectCount() => Interlocked.Increment(ref _reconnectCount);
}
