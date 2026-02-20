// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Extensions;

/// <summary>
/// Provides extension methods for converting collections to different collection types with optimized performance.
/// </summary>
/// <remarks>
/// These extension methods provide efficient conversions between different collection types, avoiding unnecessary allocations when possible
/// and providing thread-safe operations.
/// </remarks>
public static class GenericCollectionExtensions
{
	/// <summary>
	/// Asynchronously converts a Task of IEnumerable to a List.
	/// </summary>
	/// <typeparam name="T"> The type of elements in the collection. </typeparam>
	/// <param name="sourceTask"> The task that will return the source enumerable. </param>
	/// <returns> A task that represents the asynchronous operation. The task result contains the converted List. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when sourceTask is null. </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static async Task<List<T>> AsListAsync<T>(this Task<IEnumerable<T>> sourceTask)
	{
		ArgumentNullException.ThrowIfNull(sourceTask);
		var source = await sourceTask.ConfigureAwait(false);
		return source.AsList();
	}

	/// <summary>
	/// Converts an IEnumerable to a List with optimized handling for common collection types.
	/// </summary>
	/// <typeparam name="T"> The type of elements in the collection. </typeparam>
	/// <param name="source"> The source enumerable to convert. </param>
	/// <returns> A List containing all elements from the source. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when source is null. </exception>
	/// <remarks>
	/// This method optimizes for common cases:
	/// - If source is already a List, it creates a new List to avoid external modifications
	/// - If source is an array or ICollection with known count, it pre-allocates the List capacity
	/// - Otherwise, it uses LINQ's ToList implementation.
	/// </remarks>
#pragma warning disable CA1002 // Do not expose generic lists
	public static List<T> AsList<T>(this IEnumerable<T> source)
#pragma warning restore CA1002
	{
		ArgumentNullException.ThrowIfNull(source);

		return source switch
		{
			List<T> list => [.. list],
			T[] array => [.. array],
			ICollection<T> collection => [.. collection],
			_ => [.. source],
		};
	}

	/// <summary>
	/// Asynchronously converts a Task of IEnumerable to a Collection.
	/// </summary>
	/// <typeparam name="T"> The type of elements in the collection. </typeparam>
	/// <param name="sourceTask"> The task that will return the source enumerable. </param>
	/// <returns> A task that represents the asynchronous operation. The task result contains the converted Collection. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when sourceTask is null. </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static async Task<Collection<T>> AsCollectionAsync<T>(this Task<IEnumerable<T>> sourceTask)
	{
		ArgumentNullException.ThrowIfNull(sourceTask);
		var source = await sourceTask.ConfigureAwait(false);
		return source.AsCollection();
	}

	/// <summary>
	/// Converts an IEnumerable to a Collection.
	/// </summary>
	/// <typeparam name="T"> The type of elements in the collection. </typeparam>
	/// <param name="source"> The source enumerable to convert. </param>
	/// <returns> A Collection containing all elements from the source. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when source is null. </exception>
	public static Collection<T> AsCollection<T>(this IEnumerable<T> source)
	{
		ArgumentNullException.ThrowIfNull(source);

		return source switch
		{
			Collection<T> collection => new Collection<T>([.. collection]),
			List<T> list => new Collection<T>([.. list]),
			_ => new Collection<T>([.. source]),
		};
	}

	/// <summary>
	/// Asynchronously converts a Task of IEnumerable to a ReadOnlyCollection.
	/// </summary>
	/// <typeparam name="T"> The type of elements in the collection. </typeparam>
	/// <param name="sourceTask"> The task that will return the source enumerable. </param>
	/// <returns> A task that represents the asynchronous operation. The task result contains the converted ReadOnlyCollection. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when sourceTask is null. </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static async Task<ReadOnlyCollection<T>> AsReadOnlyCollectionAsync<T>(this Task<IEnumerable<T>> sourceTask)
	{
		ArgumentNullException.ThrowIfNull(sourceTask);
		var source = await sourceTask.ConfigureAwait(false);
		return source.AsReadOnlyCollection();
	}

	/// <summary>
	/// Converts an IEnumerable to a ReadOnlyCollection with optimized handling.
	/// </summary>
	/// <typeparam name="T"> The type of elements in the collection. </typeparam>
	/// <param name="source"> The source enumerable to convert. </param>
	/// <returns> A ReadOnlyCollection containing all elements from the source. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when source is null. </exception>
	public static ReadOnlyCollection<T> AsReadOnlyCollection<T>(this IEnumerable<T> source)
	{
		ArgumentNullException.ThrowIfNull(source);

		return source switch
		{
			ReadOnlyCollection<T> roc => new ReadOnlyCollection<T>([.. roc]),
			IList<T> list => new ReadOnlyCollection<T>([.. list]),
			_ => new ReadOnlyCollection<T>([.. source]),
		};
	}

	/// <summary>
	/// Asynchronously converts a Task of IEnumerable to an array.
	/// </summary>
	/// <typeparam name="T"> The type of elements in the collection. </typeparam>
	/// <param name="sourceTask"> The task that will return the source enumerable. </param>
	/// <returns> A task that represents the asynchronous operation. The task result contains the converted array. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when sourceTask is null. </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static async Task<T[]> AsArrayAsync<T>(this Task<IEnumerable<T>> sourceTask)
	{
		ArgumentNullException.ThrowIfNull(sourceTask);
		var source = await sourceTask.ConfigureAwait(false);
		return source.AsArray();
	}

	/// <summary>
	/// Converts an IEnumerable to an array with optimized handling for collections with known count.
	/// </summary>
	/// <typeparam name="T"> The type of elements in the collection. </typeparam>
	/// <param name="source"> The source enumerable to convert. </param>
	/// <returns> An array containing all elements from the source. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when source is null. </exception>
	public static T[] AsArray<T>(this IEnumerable<T> source)
	{
		ArgumentNullException.ThrowIfNull(source);

		return source switch
		{
			T[] array => (T[])array.Clone(),
			ICollection<T> collection => [.. collection],
			_ => [.. source],
		};
	}

	/// <summary>
	/// Asynchronously converts a Task of IEnumerable to an IReadOnlyList.
	/// </summary>
	/// <typeparam name="T"> The type of elements in the collection. </typeparam>
	/// <param name="sourceTask"> The task that will return the source enumerable. </param>
	/// <returns> A task that represents the asynchronous operation. The task result contains the converted IReadOnlyList. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when sourceTask is null. </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static async Task<IReadOnlyList<T>> AsReadOnlyListAsync<T>(this Task<IEnumerable<T>> sourceTask)
	{
		ArgumentNullException.ThrowIfNull(sourceTask);
		var source = await sourceTask.ConfigureAwait(false);
		return source.AsReadOnlyList();
	}

	/// <summary>
	/// Converts an IEnumerable to an IReadOnlyList with optimized handling.
	/// </summary>
	/// <typeparam name="T"> The type of elements in the collection. </typeparam>
	/// <param name="source"> The source enumerable to convert. </param>
	/// <returns> An IReadOnlyList containing all elements from the source. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when source is null. </exception>
	public static IReadOnlyList<T> AsReadOnlyList<T>(this IEnumerable<T> source)
	{
		ArgumentNullException.ThrowIfNull(source);

		return source switch
		{
			IReadOnlyList<T> readOnlyList => new List<T>(readOnlyList).AsReadOnly(),
			IList<T> list => new List<T>(list).AsReadOnly(),
			_ => source.ToList().AsReadOnly(),
		};
	}

	/// <summary>
	/// Safely checks if a collection is null or empty.
	/// </summary>
	/// <typeparam name="T"> The type of elements in the collection. </typeparam>
	/// <param name="source"> The source collection to check. </param>
	/// <returns> True if the collection is null or empty; otherwise, false. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsNullOrEmpty<T>(this IEnumerable<T>? source)
	{
		if (source is null)
		{
			return true;
		}

		return source switch
		{
			ICollection<T> collection => collection.Count == 0,
			IReadOnlyCollection<T> readOnlyCollection => readOnlyCollection.Count == 0,
			string str => str.Length == 0,
			_ => !source.Any(),
		};
	}
}
