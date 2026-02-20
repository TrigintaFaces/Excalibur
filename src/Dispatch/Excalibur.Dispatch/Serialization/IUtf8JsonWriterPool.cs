// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Text.Json;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Defines a pool for managing <see cref="Utf8JsonWriter" /> instances to reduce allocations.
/// </summary>
public interface IUtf8JsonWriterPool
{
	/// <summary>
	/// Gets the maximum number of writers that can be pooled.
	/// </summary>
	/// <value>
	/// The maximum number of writers that can be pooled.
	/// </value>
	int MaxPoolSize { get; }

	/// <summary>
	/// Gets the current number of writers in the pool.
	/// </summary>
	/// <value>
	/// The current number of writers in the pool.
	/// </value>
	int Count { get; }

	/// <summary>
	/// Gets the total number of writers that have been rented from the pool.
	/// </summary>
	/// <value>
	/// The total number of writers that have been rented from the pool.
	/// </value>
	long TotalRented { get; }

	/// <summary>
	/// Gets the total number of writers that have been returned to the pool.
	/// </summary>
	/// <value>
	/// The total number of writers that have been returned to the pool.
	/// </value>
	long TotalReturned { get; }

	/// <summary>
	/// Rents a <see cref="Utf8JsonWriter" /> from the pool or creates a new one if the pool is empty.
	/// </summary>
	/// <param name="bufferWriter"> The buffer writer to use for the JSON writer. </param>
	/// <param name="options"> Optional JSON writer options. If null, default options are used. </param>
	/// <returns> A rented or newly created <see cref="Utf8JsonWriter" />. </returns>
	Utf8JsonWriter Rent(IBufferWriter<byte> bufferWriter, JsonWriterOptions? options = null);

	/// <summary>
	/// Returns a <see cref="Utf8JsonWriter" /> to the pool for reuse.
	/// </summary>
	/// <param name="writer"> The writer to return to the pool. </param>
	void ReturnToPool(Utf8JsonWriter writer);

	/// <summary>
	/// Clears all writers from the pool and disposes them.
	/// </summary>
	void Clear();

	/// <summary>
	/// Pre-warms the pool by creating the specified number of writers.
	/// </summary>
	/// <param name="count"> The number of writers to pre-create. </param>
	void PreWarm(int count);
}
