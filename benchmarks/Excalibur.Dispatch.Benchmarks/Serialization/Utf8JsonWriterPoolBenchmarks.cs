// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Text.Json;

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Benchmarks.Serialization;

/// <summary>
/// Decides whether the hand-written writer pool earns its keep against
/// <c>Microsoft.Extensions.ObjectPool</c>, which this project already references.
/// </summary>
/// <remarks>
/// <para>
/// The two are not obviously ordered. The hand-written pool has a thread-local tier the BCL pool has
/// no equivalent for, and pays several interlocked increments per rent for counters the BCL pool does
/// not keep. Which dominates depends on contention, so both arms run single-threaded and contended.
/// </para>
/// <para>
/// Rent-and-return is measured rather than serialization end to end: a full serialize would bury the
/// difference under JSON writing and answer a question nobody asked.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class Utf8JsonWriterPoolBenchmarks
{
	private const int MaxPoolSize = 100;

	private Utf8JsonWriterPool _handWritten = null!;
	private PooledObjectUtf8JsonWriterPool _bclPool = null!;

	[Params(1, 8)]
	public int Threads { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		var options = new JsonWriterOptions { SkipValidation = true };
		_handWritten = new Utf8JsonWriterPool(maxPoolSize: MaxPoolSize, defaultOptions: options);
		_bclPool = new PooledObjectUtf8JsonWriterPool(options, MaxPoolSize);
	}

	[GlobalCleanup]
	public void Cleanup() => _handWritten.Dispose();

	[Benchmark(Baseline = true)]
	public void HandWritten() => RentReturn(w => _handWritten.Rent(w), _handWritten.ReturnToPool);

	[Benchmark]
	public void ObjectPool() => RentReturn(w => _bclPool.Rent(w), _bclPool.ReturnToPool);

	private void RentReturn(Func<IBufferWriter<byte>, Utf8JsonWriter> rent, Action<Utf8JsonWriter> ret)
	{
		if (Threads == 1)
		{
			Cycle(rent, ret);
			return;
		}

		// Contended arm: the thread-local tier only pays for itself when threads compete, so a
		// single-threaded number alone would flatter whichever pool has the cheaper fast path.
		var work = new Task[Threads];
		for (var i = 0; i < Threads; i++)
		{
			work[i] = Task.Run(() => Cycle(rent, ret));
		}

		Task.WaitAll(work);
	}


	private static void Cycle(Func<IBufferWriter<byte>, Utf8JsonWriter> rent, Action<Utf8JsonWriter> ret)
	{
		// Each thread gets its own buffer. Sharing one across threads is not a contention test --
		// Utf8JsonWriter is not thread-safe against a shared IBufferWriter, so it measures a race.
		var buffer = new ArrayBufferWriter<byte>(1024);
		for (var i = 0; i < 1_000; i++)
		{
			var writer = rent(buffer);
			ret(writer);
		}
	}
}
