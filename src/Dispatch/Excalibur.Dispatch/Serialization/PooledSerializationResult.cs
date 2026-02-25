// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Represents the result of a zero-allocation serialization operation.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Value-Type Disposal Warning:</strong> This is a <c>readonly struct</c> implementing
/// <see cref="IDisposable"/>. Value-type semantics apply:
/// </para>
/// <list type="bullet">
/// <item><description>Copying this struct creates a shallow copy sharing the same underlying buffer.</description></item>
/// <item><description>Disposing any copy returns the buffer to the pool, invalidating all copies.</description></item>
/// <item><description>After disposal, accessing <see cref="WrittenMemory"/> or <see cref="WrittenSpan"/> returns invalid data.</description></item>
/// </list>
/// <para>
/// <strong>Best Practice:</strong> Use with <c>using</c> statement and avoid copying:
/// <code>
/// using var result = serializer.SerializeToPooledBuffer(message);
/// await stream.WriteAsync(result.WrittenMemory);
/// </code>
/// </para>
/// </remarks>
public readonly struct PooledSerializationResult : IDisposable, IEquatable<PooledSerializationResult>
{
	private readonly PooledBuffer _pooledBuffer;

	internal PooledSerializationResult(PooledBuffer pooledBuffer, int length)
	{
		_pooledBuffer = pooledBuffer;
		Length = length;
	}

	/// <summary>
	/// Gets the written memory containing the serialized data.
	/// </summary>
	/// <value>
	/// The written memory containing the serialized data.
	/// </value>
	public ReadOnlyMemory<byte> WrittenMemory => _pooledBuffer.Buffer.AsMemory(0, Length);

	/// <summary>
	/// Gets the written span containing the serialized data.
	/// </summary>
	/// <value>
	/// The written span containing the serialized data.
	/// </value>
	public ReadOnlySpan<byte> WrittenSpan => _pooledBuffer.Buffer.AsSpan(0, Length);

	/// <summary>
	/// Gets the number of bytes written.
	/// </summary>
	/// <value>The current <see cref="Length"/> value.</value>
	public int Length { get; }

	/// <summary>
	/// Determines whether two pooled serialization results are equal.
	/// </summary>
	/// <param name="left"> The first pooled serialization result to compare. </param>
	/// <param name="right"> The second pooled serialization result to compare. </param>
	/// <returns> true if the pooled serialization results are equal; otherwise, false. </returns>
	public static bool operator ==(PooledSerializationResult left, PooledSerializationResult right) => left.Equals(right);

	/// <summary>
	/// Determines whether two pooled serialization results are not equal.
	/// </summary>
	/// <param name="left"> The first pooled serialization result to compare. </param>
	/// <param name="right"> The second pooled serialization result to compare. </param>
	/// <returns> true if the pooled serialization results are not equal; otherwise, false. </returns>
	public static bool operator !=(PooledSerializationResult left, PooledSerializationResult right) => !left.Equals(right);

	/// <summary>
	/// Copies the serialized data to the specified buffer writer.
	/// </summary>
	public void CopyTo(IBufferWriter<byte> writer)
	{
		ArgumentNullException.ThrowIfNull(writer);
		writer.Write(WrittenSpan);
	}

	/// <summary>
	/// Returns the pooled buffer to the pool.
	/// </summary>
	public void Dispose() => _pooledBuffer.Dispose();

	/// <summary>
	/// Determines whether the specified pooled serialization result is equal to the current pooled serialization result.
	/// </summary>
	/// <param name="other"> The pooled serialization result to compare with the current pooled serialization result. </param>
	/// <returns>
	/// true if the specified pooled serialization result is equal to the current pooled serialization result; otherwise, false.
	/// </returns>
	public bool Equals(PooledSerializationResult other) => ReferenceEquals(_pooledBuffer, other._pooledBuffer) && Length == other.Length;

	/// <summary>
	/// Determines whether the specified object is equal to the current pooled serialization result.
	/// </summary>
	/// <param name="obj"> The object to compare with the current pooled serialization result. </param>
	/// <returns> true if the specified object is equal to the current pooled serialization result; otherwise, false. </returns>
	public override bool Equals(object? obj) => obj is PooledSerializationResult other && Equals(other);

	/// <summary>
	/// Returns the hash code for this pooled serialization result.
	/// </summary>
	/// <returns> A hash code for the current pooled serialization result. </returns>
	public override int GetHashCode() => HashCode.Combine(_pooledBuffer?.GetHashCode() ?? 0, Length);
}
