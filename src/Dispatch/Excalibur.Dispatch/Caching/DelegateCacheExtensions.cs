// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Extension methods for delegate caching.
/// </summary>
/// <remarks>
/// Uses <see cref="DelegateCacheKey"/> struct for zero-allocation cache key generation.
/// Previous implementation used string interpolation which allocated on every lookup.
/// </remarks>
public static class DelegateCacheExtensions
{
	/// <summary>
	/// Creates a cached continuation delegate.
	/// </summary>
	/// <remarks>
	/// Uses struct-based cache key to avoid string allocation.
	/// Previous: $"continuation_{key}_{typeof(T).Name}_{typeof(TResult).Name}" (allocates on every call).
	/// New: DelegateCacheKey struct with Type references (zero allocation - Type objects are runtime-cached).
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Func<Task<T>, Task<TResult>> GetContinuation<T, TResult>(
		this string key,
		Func<T, TResult> selector) =>
		DelegateCache.GetOrCreate(
			new DelegateCacheKey("continuation", key, typeof(T), typeof(TResult)),
			() => new Func<Task<T>, Task<TResult>>(async task => selector(await task.ConfigureAwait(false))));

	/// <summary>
	/// Creates a cached error handler delegate.
	/// </summary>
	/// <remarks>
	/// Uses struct-based cache key to avoid string allocation.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Func<Exception, Task<bool>> GetErrorHandler(
		this string key,
		Func<Exception, bool> predicate) =>
		DelegateCache.GetOrCreate(
			new DelegateCacheKey("error", key),
			() => new Func<Exception, Task<bool>>(ex => Task.FromResult(predicate(ex))));

	/// <summary>
	/// Creates a cached transform delegate.
	/// </summary>
	/// <remarks>
	/// Uses struct-based cache key to avoid string allocation.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Func<TInput, TOutput> GetTransform<TInput, TOutput>(
		this string key,
		Func<TInput, TOutput> transform) =>
		DelegateCache.GetOrCreate(
			new DelegateCacheKey("transform", key, typeof(TInput), typeof(TOutput)),
			() => transform);
}
