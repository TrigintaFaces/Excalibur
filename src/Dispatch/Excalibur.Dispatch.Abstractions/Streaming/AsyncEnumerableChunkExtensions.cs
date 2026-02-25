// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Abstractions.Streaming;

/// <summary>
/// Provides extension methods for adding positional metadata to <see cref="IAsyncEnumerable{T}"/> streams.
/// </summary>
public static class AsyncEnumerableChunkExtensions
{
	/// <summary>
	/// Wraps each element of an async sequence with positional metadata as a <see cref="Chunk{T}"/>.
	/// </summary>
	/// <typeparam name="T">The type of elements in the source sequence.</typeparam>
	/// <param name="source">The source async enumerable to wrap.</param>
	/// <param name="cancellationToken">A cancellation token to observe.</param>
	/// <returns>
	/// An async enumerable where each element is wrapped in a <see cref="Chunk{T}"/> containing
	/// the original data plus positional metadata (index, first/last flags).
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="source"/> is <see langword="null"/>.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This method uses single-item lookahead to determine when an element is the last in the sequence.
	/// Memory overhead is minimal: only two elements are ever held in memory simultaneously.
	/// </para>
	/// <para>
	/// For empty sequences, no chunks are yielded.
	/// </para>
	/// <example>
	/// <code>
	/// await foreach (var chunk in stream.WithChunkInfo())
	/// {
	///     if (chunk.IsFirst) Console.WriteLine("Starting stream...");
	///     Console.WriteLine($"[{chunk.Index}] {chunk.Data}");
	///     if (chunk.IsLast) Console.WriteLine("Stream complete!");
	/// }
	/// </code>
	/// </example>
	/// </remarks>
	public static async IAsyncEnumerable<Chunk<T>> WithChunkInfo<T>(
		this IAsyncEnumerable<T> source,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(source);

		await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

		if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
		{
			// Empty sequence - yield nothing
			yield break;
		}

		var current = enumerator.Current;
		long index = 0;
		var hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);

		while (true)
		{
			var isFirst = index == 0;
			var isLast = !hasNext;

			yield return new Chunk<T>(current, index, isFirst, isLast);

			if (!hasNext)
			{
				break;
			}

			current = enumerator.Current;
			index++;
			hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Creates a single-element async sequence wrapped as a <see cref="Chunk{T}"/>.
	/// </summary>
	/// <typeparam name="T">The type of the element.</typeparam>
	/// <param name="value">The single value to wrap.</param>
	/// <param name="cancellationToken">A cancellation token to observe.</param>
	/// <returns>
	/// An async enumerable containing a single <see cref="Chunk{T}"/> where both
	/// <see cref="Chunk{T}.IsFirst"/> and <see cref="Chunk{T}.IsLast"/> are <see langword="true"/>.
	/// </returns>
	/// <remarks>
	/// This is a convenience method for creating a chunked stream from a single value,
	/// useful when integrating single-item results with streaming APIs.
	/// </remarks>
#pragma warning disable CS1998 // Async method lacks 'await' operators - intentional for async enumerable yield
	public static async IAsyncEnumerable<Chunk<T>> AsSingleChunk<T>(
		this T value,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
#pragma warning restore CS1998
	{
		cancellationToken.ThrowIfCancellationRequested();
		yield return new Chunk<T>(value, Index: 0, IsFirst: true, IsLast: true);
	}
}
