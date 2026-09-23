// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers;
using System.Text.Json;

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Serialization;

/// <summary>
/// The global pool keeps one queue per distinct <see cref="JsonWriterOptions"/> shape and caps how many of
/// those queues it will create. These arms pin what happens to a writer returned once that cap is reached.
/// </summary>
/// <remarks>
/// <para>
/// <c>ReturnToPool</c> has exactly two honest outcomes: the writer is kept, and <c>Count</c> rises to say so;
/// or the writer is not kept, and it is disposed. There is no third outcome in which the pool reports a
/// writer it is not holding. A count that includes writers the pool dropped is not a cosmetic error -- the
/// return path refuses once the count reaches <c>MaxPoolSize</c>, so an inflated count eventually stops the
/// pool from pooling anything at all, and every dropped writer is also never disposed.
/// </para>
/// <para>
/// The thread-local cache is switched off so every return takes the global path under test, and adaptive
/// sizing is switched off so no timer can move <c>MaxPoolSize</c> mid-arm.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Serialization)]
public sealed class Utf8JsonWriterPoolBoundedReturnShould
{
	private const int DistinctShapesBeyondTheCap = 40;

	[Fact]
	public void NotCountWritersItRefusedToKeep()
	{
		using var pool = CreatePool();

		ReturnWritersWithDistinctOptions(pool, DistinctShapesBeyondTheCap);

		pool.Clear();

		pool.Count.ShouldBe(
			0,
			"Clear disposes and decrements every writer the pool actually holds, so a non-zero remainder is "
			+ "the count of writers it reported keeping and never had -- a drift that only ever grows and "
			+ "eventually pins the pool at MaxPoolSize with nothing in it");
	}

	[Fact]
	public void NotRaiseItsCountForAWriterItRefused()
	{
		using var pool = CreatePool();

		ReturnWritersWithDistinctOptions(pool, DistinctShapesBeyondTheCap);

		var before = pool.Count;

		var buffer = new ArrayBufferWriter<byte>();
		var overflow = pool.Rent(buffer, new JsonWriterOptions { MaxDepth = DistinctShapesBeyondTheCap + 1 });
		pool.ReturnToPool(overflow);

		pool.Count.ShouldBe(
			before,
			"there is no queue for this writer's options and no room to open one, so the pool did not keep "
			+ "it -- and the return path's own contract is that a writer it does not keep is disposed, "
			+ "which only holds if the count says the same thing");
	}

	[Fact]
	public void StillKeepWritersWithinTheBucketCap()
	{
		using var pool = CreatePool();

		ReturnWritersWithDistinctOptions(pool, 4);

		pool.Count.ShouldBe(
			4,
			"refusing everything would satisfy the arms above while destroying the pool, so the ordinary "
			+ "case must still be kept");
	}

	private static Utf8JsonWriterPool CreatePool() => new(
		maxPoolSize: 4096,
		threadLocalCacheSize: 0,
		defaultOptions: null,
		enableAdaptiveSizing: false,
		enableTelemetry: false);

	private static void ReturnWritersWithDistinctOptions(Utf8JsonWriterPool pool, int count)
	{
		for (var i = 1; i <= count; i++)
		{
			var buffer = new ArrayBufferWriter<byte>();
			var writer = pool.Rent(buffer, new JsonWriterOptions { MaxDepth = i });
			pool.ReturnToPool(writer);
		}
	}
}
