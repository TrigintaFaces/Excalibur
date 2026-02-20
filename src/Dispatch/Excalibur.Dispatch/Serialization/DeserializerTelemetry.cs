// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Telemetry data for deserializer operations.
/// </summary>
public sealed class DeserializerTelemetry
{
	private long _deserializationCount;
	private long _bytesRead;
	private long _sequenceDeserializations;
	private long _pipeDeserializations;
	private long _streamDeserializations;

	/// <summary>
	/// Gets the total number of deserialization operations performed.
	/// </summary>
	/// <value>
	/// The total number of deserialization operations performed.
	/// </value>
	public long DeserializationCount => Interlocked.Read(ref _deserializationCount);

	/// <summary>
	/// Gets the total number of bytes read during deserialization.
	/// </summary>
	/// <value>
	/// The total number of bytes read during deserialization.
	/// </value>
	public long BytesRead => Interlocked.Read(ref _bytesRead);

	/// <summary>
	/// Gets the number of sequence-based deserializations.
	/// </summary>
	/// <value>
	/// The number of sequence-based deserializations.
	/// </value>
	public long SequenceDeserializations => Interlocked.Read(ref _sequenceDeserializations);

	/// <summary>
	/// Gets the number of pipe-based deserializations.
	/// </summary>
	/// <value>
	/// The number of pipe-based deserializations.
	/// </value>
	public long PipeDeserializations => Interlocked.Read(ref _pipeDeserializations);

	/// <summary>
	/// Gets the number of stream-based deserializations.
	/// </summary>
	/// <value>
	/// The number of stream-based deserializations.
	/// </value>
	public long StreamDeserializations => Interlocked.Read(ref _streamDeserializations);

	/// <summary>
	/// Resets all telemetry counters to zero.
	/// </summary>
	public void Reset()
	{
		_ = Interlocked.Exchange(ref _deserializationCount, 0);
		_ = Interlocked.Exchange(ref _bytesRead, 0);
		_ = Interlocked.Exchange(ref _sequenceDeserializations, 0);
		_ = Interlocked.Exchange(ref _pipeDeserializations, 0);
		_ = Interlocked.Exchange(ref _streamDeserializations, 0);
	}

	internal void IncrementDeserializationCount() => Interlocked.Increment(ref _deserializationCount);

	internal void AddBytesRead(long bytes) => Interlocked.Add(ref _bytesRead, bytes);

	internal void IncrementSequenceDeserializations() => Interlocked.Increment(ref _sequenceDeserializations);

	internal void IncrementPipeDeserializations() => Interlocked.Increment(ref _pipeDeserializations);

	internal void IncrementStreamDeserializations() => Interlocked.Increment(ref _streamDeserializations);
}
