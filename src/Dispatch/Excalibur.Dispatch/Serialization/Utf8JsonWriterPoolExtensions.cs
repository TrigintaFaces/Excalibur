// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Text.Json;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Extension methods for <see cref="IUtf8JsonWriterPool" />.
/// </summary>
internal static class Utf8JsonWriterPoolExtensions
{
	/// <summary>
	/// Gets the total number of writers rented from the pool.
	/// </summary>
	/// <param name="pool">The writer pool.</param>
	/// <returns>The total number of rented writers, or 0 if the pool does not support diagnostics.</returns>
	public static long TotalRented(this IUtf8JsonWriterPool pool)
	{
		ArgumentNullException.ThrowIfNull(pool);
		if (pool is IUtf8JsonWriterPoolDiagnostics diag)
		{
			return diag.TotalRented;
		}

		return 0;
	}

	/// <summary>
	/// Gets the total number of writers returned to the pool.
	/// </summary>
	/// <param name="pool">The writer pool.</param>
	/// <returns>The total number of returned writers, or 0 if the pool does not support diagnostics.</returns>
	public static long TotalReturned(this IUtf8JsonWriterPool pool)
	{
		ArgumentNullException.ThrowIfNull(pool);
		if (pool is IUtf8JsonWriterPoolDiagnostics diag)
		{
			return diag.TotalReturned;
		}

		return 0;
	}

	/// <summary>
	/// Pre-warms the pool by creating the specified number of writers.
	/// </summary>
	/// <param name="pool">The writer pool.</param>
	/// <param name="count">The number of writers to pre-create.</param>
	public static void PreWarm(this IUtf8JsonWriterPool pool, int count)
	{
		ArgumentNullException.ThrowIfNull(pool);
		if (pool is IUtf8JsonWriterPoolDiagnostics diag)
		{
			diag.PreWarm(count);
		}
	}

	/// <summary>
	/// Rents a <see cref="Utf8JsonWriter" /> from the pool wrapped in a <see cref="PooledUtf8JsonWriter" /> that automatically returns to
	/// the pool when disposed.
	/// </summary>
	/// <param name="pool">The writer pool.</param>
	/// <param name="bufferWriter">The buffer writer to write to.</param>
	/// <param name="options">Optional JSON writer options.</param>
	/// <returns>A <see cref="PooledUtf8JsonWriter"/> that returns the writer to the pool on dispose.</returns>
	public static PooledUtf8JsonWriter RentWriter(this IUtf8JsonWriterPool pool, IBufferWriter<byte> bufferWriter,
		JsonWriterOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(pool);
		ArgumentNullException.ThrowIfNull(bufferWriter);

		var writer = pool.Rent(bufferWriter, options);
		return new PooledUtf8JsonWriter(pool, writer);
	}

	/// <summary>
	/// Executes an action with a rented writer that is automatically returned to the pool.
	/// </summary>
	/// <param name="pool">The writer pool.</param>
	/// <param name="bufferWriter">The buffer writer to write to.</param>
	/// <param name="action">The action to execute with the rented writer.</param>
	/// <param name="options">Optional JSON writer options.</param>
	public static void WithWriter(this IUtf8JsonWriterPool pool, IBufferWriter<byte> bufferWriter, Action<Utf8JsonWriter> action,
		JsonWriterOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(pool);
		ArgumentNullException.ThrowIfNull(bufferWriter);
		ArgumentNullException.ThrowIfNull(action);

		using var pooledWriter = pool.RentWriter(bufferWriter, options);
		action(pooledWriter.Writer);
	}

	/// <summary>
	/// Executes a function with a rented writer that is automatically returned to the pool.
	/// </summary>
	/// <typeparam name="T">The return type of the function.</typeparam>
	/// <param name="pool">The writer pool.</param>
	/// <param name="bufferWriter">The buffer writer to write to.</param>
	/// <param name="func">The function to execute with the rented writer.</param>
	/// <param name="options">Optional JSON writer options.</param>
	/// <returns>The result of the function.</returns>
	public static T WithWriter<T>(this IUtf8JsonWriterPool pool, IBufferWriter<byte> bufferWriter, Func<Utf8JsonWriter, T> func,
		JsonWriterOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(pool);
		ArgumentNullException.ThrowIfNull(bufferWriter);
		ArgumentNullException.ThrowIfNull(func);

		using var pooledWriter = pool.RentWriter(bufferWriter, options);
		return func(pooledWriter.Writer);
	}
}
