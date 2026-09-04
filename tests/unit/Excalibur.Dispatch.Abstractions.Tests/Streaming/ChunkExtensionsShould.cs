// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Streaming;

namespace Excalibur.Dispatch.Tests.Streaming;

/// <summary>
/// Locks the positional metadata attached by <see cref="ChunkExtensions"/>.
/// </summary>
/// <remarks>
/// <para>
/// The last-element flag is the part that can silently be wrong: it requires reading one element
/// ahead, so an implementation that reports position from the current element alone can never set
/// it, and one that buffers the whole source sets it correctly while defeating the point of a stream.
/// </para>
/// <para>
/// <b>Non-vacuity:</b> the safety arm is that exactly one chunk is last; the liveness arm is that
/// elements are yielded <i>before</i> the source completes (<see cref="StreamLazily"/>), which fails
/// on any implementation that drains the source into a list first — the obvious way to make the
/// last-element flag easy.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ChunkExtensionsShould
{
	[Fact]
	public async Task MarkTheFirstAndLastElementOfAMultiElementStream()
	{
		var chunks = await Source("a", "b", "c").WithChunkInfo(TestContext.Current.CancellationToken)
			.ToListAsync();

		chunks.Count.ShouldBe(3);
		chunks.Select(c => c.Data).ShouldBe(["a", "b", "c"]);
		chunks.Select(c => c.Index).ShouldBe([0L, 1L, 2L]);

		chunks.Count(c => c.IsFirst).ShouldBe(1, "exactly one chunk is the first");
		chunks.Count(c => c.IsLast).ShouldBe(1, "exactly one chunk is the last");
		chunks[0].IsFirst.ShouldBeTrue();
		chunks[2].IsLast.ShouldBeTrue();
		chunks[1].IsMiddle.ShouldBeTrue("a chunk that is neither first nor last is a middle chunk");
	}

	[Fact]
	public async Task MarkALoneElementAsBothFirstAndLast()
	{
		var chunks = await Source("only").WithChunkInfo(TestContext.Current.CancellationToken).ToListAsync();

		var single = chunks.ShouldHaveSingleItem();
		single.IsFirst.ShouldBeTrue();
		single.IsLast.ShouldBeTrue();
		single.IsSingle.ShouldBeTrue();
		single.IsMiddle.ShouldBeFalse();
	}

	[Fact]
	public async Task YieldNothingForAnEmptyStream()
	{
		var chunks = await Source<string>().WithChunkInfo(TestContext.Current.CancellationToken).ToListAsync();

		chunks.ShouldBeEmpty("an empty source has no first element to mark");
	}

	[Fact]
	public async Task StreamLazily()
	{
		// Liveness arm. An implementation that buffers the source to discover the last element
		// would report every chunk only after the source had fully completed, which is the
		// behaviour a chunked stream exists to avoid.
		var produced = 0;

		async IAsyncEnumerable<int> Counted()
		{
			for (var i = 0; i < 3; i++)
			{
				produced++;
				yield return i;
				await Task.Yield();
			}
		}

		await foreach (var chunk in Counted().WithChunkInfo(TestContext.Current.CancellationToken))
		{
			// One element of lookahead is required to know IsLast, so the producer runs at most
			// one ahead of the consumer -- never to completion.
			((long)produced).ShouldBeLessThanOrEqualTo(chunk.Index + 2);
		}

		produced.ShouldBe(3);
	}

	[Fact]
	public async Task PresentASingleValueAsAOneChunkStream()
	{
		var chunks = await "summary".AsSingleChunk().ToListAsync();

		var single = chunks.ShouldHaveSingleItem();
		single.Data.ShouldBe("summary");
		single.Index.ShouldBe(0);
		single.IsSingle.ShouldBeTrue();
	}

	[Fact]
	public async Task RejectANullStream()
	{
		IAsyncEnumerable<string> source = null!;

		_ = await Should.ThrowAsync<ArgumentNullException>(
			async () => await source.WithChunkInfo(TestContext.Current.CancellationToken).ToListAsync());
	}

	private static async IAsyncEnumerable<T> Source<T>(params T[] items)
	{
		foreach (var item in items)
		{
			yield return item;
			await Task.Yield();
		}
	}
}
