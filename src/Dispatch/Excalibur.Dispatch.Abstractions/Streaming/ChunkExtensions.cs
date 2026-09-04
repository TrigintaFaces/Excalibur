// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Streaming;

/// <summary>
/// Extension methods that attach positional metadata to a stream, producing <see cref="Chunk{T}"/> values.
/// </summary>
public static class ChunkExtensions
{
	/// <summary>
	/// Wraps each element of a stream in a <see cref="Chunk{T}"/> carrying its position.
	/// </summary>
	/// <typeparam name="T">The element type.</typeparam>
	/// <param name="source">The stream to annotate.</param>
	/// <returns>A stream of chunks, each describing its own position within the sequence.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
	public static IAsyncEnumerable<Chunk<T>> WithChunkInfo<T>(this IAsyncEnumerable<T> source) =>
		source.WithChunkInfo(CancellationToken.None);

	/// <summary>
	/// Wraps each element of a stream in a <see cref="Chunk{T}"/> carrying its position.
	/// </summary>
	/// <typeparam name="T">The element type.</typeparam>
	/// <param name="source">The stream to annotate.</param>
	/// <param name="cancellationToken">A token that cancels enumeration.</param>
	/// <returns>A stream of chunks, each describing its own position within the sequence.</returns>
	/// <remarks>
	/// The source is read one element ahead so that each chunk can report whether it is the last.
	/// An empty source yields no chunks; a single-element source yields one chunk that is both
	/// first and last (<see cref="Chunk{T}.IsSingle"/>).
	/// </remarks>
	/// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
	public static async IAsyncEnumerable<Chunk<T>> WithChunkInfo<T>(
		this IAsyncEnumerable<T> source,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(source);

		var enumerator = source.GetAsyncEnumerator(cancellationToken);
		try
		{
			if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
			{
				yield break;
			}

			var current = enumerator.Current;
			var index = 0L;

			while (true)
			{
				var hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
				yield return new Chunk<T>(current, index, index == 0, !hasNext);

				if (!hasNext)
				{
					yield break;
				}

				current = enumerator.Current;
				index++;
			}
		}
		finally
		{
			await enumerator.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Presents a single value as a one-element chunked stream.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="data">The value to wrap.</param>
	/// <returns>
	/// A stream of exactly one chunk, which is both the first and the last
	/// (<see cref="Chunk{T}.IsSingle"/> is <see langword="true"/>).
	/// </returns>
	/// <remarks>
	/// Use this where an API requires a chunked stream but the result is known to be a single value,
	/// so the caller does not have to hand-construct the positional metadata.
	/// </remarks>
#pragma warning disable CS1998, IDE0390 // The sequence is known without awaiting, but an async iterator is the only way to produce IAsyncEnumerable.
	public static async IAsyncEnumerable<Chunk<T>> AsSingleChunk<T>(this T data)
	{
		yield return new Chunk<T>(data, 0, true, true);
	}
#pragma warning restore CS1998, IDE0390
}
