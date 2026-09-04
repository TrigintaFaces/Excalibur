// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Text.Json;

using Microsoft.Extensions.ObjectPool;

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Benchmarks.Serialization;

/// <summary>
/// A writer pool built on <see cref="DefaultObjectPool{T}"/> rather than on a hand-written one.
/// </summary>
/// <remarks>
/// <para>
/// Kept alongside the hand-written pool so the two can be measured against each other rather than
/// argued about. The trade is not obvious in either direction: the hand-written pool holds a
/// thread-local tier that this one has no equivalent for, and pays several interlocked increments
/// per rent to maintain counters that this one does not keep.
/// </para>
/// <para>
/// One behavioural difference is not a matter of taste. <see cref="DefaultObjectPool{T}"/> holds a
/// single policy, so every writer it returns carries the options that policy was built with. The
/// hand-written pool tracks options per writer and can serve several option sets from one pool. This
/// implementation therefore serves its configured options only, and falls back to constructing a
/// writer when a caller asks for anything else.
/// </para>
/// </remarks>
/// <remarks>Lives in the benchmark project, not in src: it exists to be measured against the
/// shipping pool, and the measurement says the shipping pool wins where it counts.</remarks>
internal sealed class PooledObjectUtf8JsonWriterPool : IUtf8JsonWriterPool
{
	private readonly ObjectPool<Utf8JsonWriter> _pool;
	private readonly JsonWriterOptions _options;
	private int _count;

	public PooledObjectUtf8JsonWriterPool(JsonWriterOptions options, int maxPoolSize)
	{
		_options = options;
		MaxPoolSize = maxPoolSize;
		_pool = new DefaultObjectPool<Utf8JsonWriter>(new Policy(options), maxPoolSize);
	}

	public int MaxPoolSize { get; }

	/// <summary>Gets the number of writers believed to be idle in the pool.</summary>
	/// <value>
	/// An approximation. <see cref="DefaultObjectPool{T}"/> does not report its occupancy, so this is
	/// counted alongside it and can drift under concurrency. It is reported for parity with the
	/// hand-written pool, and is not load-bearing.
	/// </value>
	public int Count => Volatile.Read(ref _count);

	public Utf8JsonWriter Rent(IBufferWriter<byte> bufferWriter, JsonWriterOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(bufferWriter);

		// A pool holding one policy cannot honour a different option set; constructing is correct
		// here, and silently returning differently-configured writers would not be.
		if (options is { } requested && !OptionsMatch(requested, _options))
		{
			return new Utf8JsonWriter(bufferWriter, requested);
		}

		var writer = _pool.Get();
		_ = Interlocked.Decrement(ref _count);
		writer.Reset(bufferWriter);
		return writer;
	}

	public void ReturnToPool(Utf8JsonWriter writer)
	{
		ArgumentNullException.ThrowIfNull(writer);

		_pool.Return(writer);
		_ = Interlocked.Increment(ref _count);
	}

	public void Clear() => Volatile.Write(ref _count, 0);

	private static bool OptionsMatch(JsonWriterOptions a, JsonWriterOptions b) =>
		a.Indented == b.Indented
		&& a.SkipValidation == b.SkipValidation
		&& a.Encoder == b.Encoder;

	private sealed class Policy(JsonWriterOptions options) : IPooledObjectPolicy<Utf8JsonWriter>
	{
		// A writer needs a buffer, and the pool has none to give at creation time. It is created
		// against a throwaway buffer and Reset onto the real one at every rent.
		public Utf8JsonWriter Create() => new(new ArrayBufferWriter<byte>(), options);

		public bool Return(Utf8JsonWriter obj)
		{
			obj.Reset();
			return true;
		}
	}
}
