// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Represents a pooled <see cref="Utf8JsonWriter" /> that automatically returns to the pool when disposed.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Value-Type Disposal Warning:</strong> This is a <c>readonly struct</c> implementing
/// <see cref="IDisposable"/>. Value-type semantics apply:
/// </para>
/// <list type="bullet">
/// <item><description>Copying this struct creates a shallow copy sharing the same underlying writer reference.</description></item>
/// <item><description>Disposing any copy returns the writer to the pool, invalidating all copies.</description></item>
/// <item><description>After disposal, accessing <see cref="Writer"/> on any copy causes undefined behavior.</description></item>
/// </list>
/// <para>
/// <strong>Best Practice:</strong> Use with <c>using</c> statement and avoid copying:
/// <code>
/// using var writer = pool.RentWriter(stream);
/// writer.WriteStartObject();
/// // ... write JSON content
/// writer.WriteEndObject();
/// </code>
/// </para>
/// </remarks>
public readonly struct PooledUtf8JsonWriter : IDisposable, IEquatable<PooledUtf8JsonWriter>
{
	private readonly IUtf8JsonWriterPool _pool;

	/// <summary>
	/// Initializes a new instance of the <see cref="PooledUtf8JsonWriter" /> struct.
	/// </summary>
	/// <param name="pool"> The pool to return the writer to when disposed. </param>
	/// <param name="writer"> The pooled writer. </param>
	internal PooledUtf8JsonWriter(IUtf8JsonWriterPool pool, Utf8JsonWriter writer)
	{
		_pool = pool ?? throw new ArgumentNullException(nameof(pool));
		Writer = writer ?? throw new ArgumentNullException(nameof(writer));
	}

	/// <summary>
	/// Gets the underlying <see cref="Utf8JsonWriter" />.
	/// </summary>
	/// <value>The current <see cref="Writer"/> value.</value>
	public Utf8JsonWriter Writer { get; }

	/// <summary>
	/// Gets the number of bytes written so far.
	/// </summary>
	/// <value>The current <see cref="BytesWritten"/> value.</value>
	public long BytesWritten => Writer.BytesCommitted + Writer.BytesPending;

	/// <summary>
	/// Gets the number of bytes committed so far.
	/// </summary>
	/// <value>The current <see cref="BytesCommitted"/> value.</value>
	public long BytesCommitted => Writer.BytesCommitted;

	/// <summary>
	/// Gets the current depth of the JSON being written.
	/// </summary>
	/// <value>The current <see cref="CurrentDepth"/> value.</value>
	public int CurrentDepth => Writer.CurrentDepth;

	/// <summary>
	/// Determines whether two pooled UTF8 JSON writers are equal.
	/// </summary>
	/// <param name="left"> The first pooled UTF8 JSON writer to compare. </param>
	/// <param name="right"> The second pooled UTF8 JSON writer to compare. </param>
	/// <returns> true if the pooled UTF8 JSON writers are equal; otherwise, false. </returns>
	public static bool operator ==(PooledUtf8JsonWriter left, PooledUtf8JsonWriter right) => left.Equals(right);

	/// <summary>
	/// Determines whether two pooled UTF8 JSON writers are not equal.
	/// </summary>
	/// <param name="left"> The first pooled UTF8 JSON writer to compare. </param>
	/// <param name="right"> The second pooled UTF8 JSON writer to compare. </param>
	/// <returns> true if the pooled UTF8 JSON writers are not equal; otherwise, false. </returns>
	public static bool operator !=(PooledUtf8JsonWriter left, PooledUtf8JsonWriter right) => !left.Equals(right);

	/// <summary>
	/// Writes a property name.
	/// </summary>
	public void WritePropertyName(string propertyName) => Writer.WritePropertyName(propertyName);

	/// <summary>
	/// Writes a property name.
	/// </summary>
	public void WritePropertyName(ReadOnlySpan<char> propertyName) => Writer.WritePropertyName(propertyName);

	/// <summary>
	/// Writes a property name.
	/// </summary>
	public void WritePropertyName(ReadOnlySpan<byte> utf8PropertyName) => Writer.WritePropertyName(utf8PropertyName);

	/// <summary>
	/// Writes the start of a JSON object.
	/// </summary>
	public void WriteStartObject() => Writer.WriteStartObject();

	/// <summary>
	/// Writes the end of a JSON object.
	/// </summary>
	public void WriteEndObject() => Writer.WriteEndObject();

	/// <summary>
	/// Writes the start of a JSON array.
	/// </summary>
	public void WriteStartArray() => Writer.WriteStartArray();

	/// <summary>
	/// Writes the end of a JSON array.
	/// </summary>
	public void WriteEndArray() => Writer.WriteEndArray();

	/// <summary>
	/// Writes a string value.
	/// </summary>
	public void WriteStringValue(string? value) => Writer.WriteStringValue(value);

	/// <summary>
	/// Writes a string value.
	/// </summary>
	public void WriteStringValue(ReadOnlySpan<char> value) => Writer.WriteStringValue(value);

	/// <summary>
	/// Writes a string value.
	/// </summary>
	public void WriteStringValue(ReadOnlySpan<byte> utf8Value) => Writer.WriteStringValue(utf8Value);

	/// <summary>
	/// Writes a number value.
	/// </summary>
	public void WriteNumberValue(int value) => Writer.WriteNumberValue(value);

	/// <summary>
	/// Writes a number value.
	/// </summary>
	public void WriteNumberValue(long value) => Writer.WriteNumberValue(value);

	/// <summary>
	/// Writes a number value.
	/// </summary>
	public void WriteNumberValue(double value) => Writer.WriteNumberValue(value);

	/// <summary>
	/// Writes a boolean value.
	/// </summary>
	public void WriteBooleanValue(bool value) => Writer.WriteBooleanValue(value);

	/// <summary>
	/// Writes a null value.
	/// </summary>
	public void WriteNullValue() => Writer.WriteNullValue();

	/// <summary>
	/// Flushes the writer.
	/// </summary>
	public void Flush() => Writer.Flush();

	/// <summary>
	/// Returns the writer to the pool.
	/// </summary>
	public void Dispose() => _pool?.ReturnToPool(Writer);

	/// <summary>
	/// Determines whether the specified pooled UTF8 JSON writer is equal to the current pooled UTF8 JSON writer.
	/// </summary>
	/// <param name="other"> The pooled UTF8 JSON writer to compare with the current pooled UTF8 JSON writer. </param>
	/// <returns> true if the specified pooled UTF8 JSON writer is equal to the current pooled UTF8 JSON writer; otherwise, false. </returns>
	public bool Equals(PooledUtf8JsonWriter other) => ReferenceEquals(_pool, other._pool) && ReferenceEquals(Writer, other.Writer);

	/// <summary>
	/// Determines whether the specified object is equal to the current pooled UTF8 JSON writer.
	/// </summary>
	/// <param name="obj"> The object to compare with the current pooled UTF8 JSON writer. </param>
	/// <returns> true if the specified object is equal to the current pooled UTF8 JSON writer; otherwise, false. </returns>
	public override bool Equals(object? obj) => obj is PooledUtf8JsonWriter other && Equals(other);

	/// <summary>
	/// Returns the hash code for this pooled UTF8 JSON writer.
	/// </summary>
	/// <returns> A hash code for the current pooled UTF8 JSON writer. </returns>
	public override int GetHashCode() => HashCode.Combine(_pool?.GetHashCode() ?? 0, Writer?.GetHashCode() ?? 0);
}
