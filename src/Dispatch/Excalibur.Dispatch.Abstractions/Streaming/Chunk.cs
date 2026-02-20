// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Streaming;

/// <summary>
/// Represents a single chunk within a streamed sequence, providing positional metadata.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Chunk{T}"/> is a lightweight, stack-allocated value type designed for high-performance
/// streaming scenarios. It wraps individual data items with positional metadata that enables:
/// </para>
/// <list type="bullet">
/// <item>First/last chunk detection for initialization and finalization logic</item>
/// <item>Zero-based indexing for progress tracking and ordered processing</item>
/// <item>Efficient memory usage through <see langword="readonly"/> <see langword="struct"/> semantics</item>
/// </list>
/// <para>
/// This type is intended for use with <see cref="IAsyncEnumerable{T}"/> streams where handlers
/// need to know their position within the overall sequence without buffering.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of data contained in the chunk.</typeparam>
/// <param name="Data">The data payload for this chunk.</param>
/// <param name="Index">The zero-based position of this chunk within the stream.</param>
/// <param name="IsFirst">
/// <see langword="true"/> if this is the first chunk in the stream; otherwise, <see langword="false"/>.
/// </param>
/// <param name="IsLast">
/// <see langword="true"/> if this is the last chunk in the stream; otherwise, <see langword="false"/>.
/// </param>
public readonly record struct Chunk<T>(
	T Data,
	long Index,
	bool IsFirst,
	bool IsLast)
{
	/// <summary>
	/// Gets a value indicating whether this chunk is neither the first nor the last in the stream.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if this chunk is in the middle of the stream;
	/// otherwise, <see langword="false"/>.
	/// </value>
	public bool IsMiddle => !IsFirst && !IsLast;

	/// <summary>
	/// Gets a value indicating whether this is the only chunk in the stream.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if this chunk is both the first and last (single-item stream);
	/// otherwise, <see langword="false"/>.
	/// </value>
	public bool IsSingle => IsFirst && IsLast;
}
