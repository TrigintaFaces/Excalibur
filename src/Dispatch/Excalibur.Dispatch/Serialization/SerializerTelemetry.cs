// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Telemetry data for serializer operations.
/// </summary>
public sealed class SerializerTelemetry
{
	private long _serializationCount;
	private long _deserializationCount;
	private long _bytesWritten;
	private long _bytesRead;

	/// <summary>
	/// Gets the total number of serialization operations performed.
	/// </summary>
	/// <value>
	/// The total number of serialization operations performed.
	/// </value>
	public long SerializationCount => Interlocked.Read(ref _serializationCount);

	/// <summary>
	/// Gets the total number of deserialization operations performed.
	/// </summary>
	/// <value>
	/// The total number of deserialization operations performed.
	/// </value>
	public long DeserializationCount => Interlocked.Read(ref _deserializationCount);

	/// <summary>
	/// Gets the total number of bytes written during serialization.
	/// </summary>
	/// <value>
	/// The total number of bytes written during serialization.
	/// </value>
	public long BytesWritten => Interlocked.Read(ref _bytesWritten);

	/// <summary>
	/// Gets the total number of bytes read during deserialization.
	/// </summary>
	/// <value>
	/// The total number of bytes read during deserialization.
	/// </value>
	public long BytesRead => Interlocked.Read(ref _bytesRead);

	/// <summary>
	/// Resets all telemetry counters to zero.
	/// </summary>
	public void Reset()
	{
		_ = Interlocked.Exchange(ref _serializationCount, 0);
		_ = Interlocked.Exchange(ref _deserializationCount, 0);
		_ = Interlocked.Exchange(ref _bytesWritten, 0);
		_ = Interlocked.Exchange(ref _bytesRead, 0);
	}

	internal void IncrementSerializationCount() => Interlocked.Increment(ref _serializationCount);

	internal void IncrementDeserializationCount() => Interlocked.Increment(ref _deserializationCount);

	internal void AddBytesWritten(long bytes) => Interlocked.Add(ref _bytesWritten, bytes);

	internal void AddBytesRead(long bytes) => Interlocked.Add(ref _bytesRead, bytes);
}
