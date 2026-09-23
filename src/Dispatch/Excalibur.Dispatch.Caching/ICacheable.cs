// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Declares that a message's result may be cached, and describes how.
/// </summary>
/// <remarks>
/// None of these members depends on the message's result type, so they live here rather than on
/// <see cref="ICacheable{T}" />. That is what lets the caching middleware recognise a cacheable message
/// with a type test and call these directly. While they sat on the generic interface the middleware
/// could not name a closed type to match against, so it resolved them by walking the runtime type's
/// interfaces and invoking each member through reflection — on the cache hot path, and unanalysable by
/// the trimmer.
/// <para>
/// Implement <see cref="ICacheable{T}" />; it derives from this and carries the result type.
/// </para>
/// </remarks>
public interface ICacheable
{
	/// <summary>
	/// Gets how long a cached result stays valid, in seconds.
	/// </summary>
	/// <value> Defaults to 60. Zero or less falls back to the configured default expiration. </value>
	int ExpirationSeconds => 60;

	/// <summary>
	/// Builds the key this message's result is cached under.
	/// </summary>
	/// <returns> The cache key. </returns>
	string GetCacheKey();

	/// <summary>
	/// Gets the tags the cached result is filed under, so it can be invalidated by tag.
	/// </summary>
	/// <returns> The tags, or <see langword="null" /> for none. </returns>
	string[]? GetCacheTags() => null;

	/// <summary>
	/// Decides whether a particular result is worth caching.
	/// </summary>
	/// <param name="result"> The result the handler produced. </param>
	/// <returns> <see langword="true" /> to cache it; otherwise <see langword="false" />. </returns>
	bool ShouldCache(object? result) => true;

	/// <summary>
	/// Builds a strongly-typed cache-hit result wrapping <paramref name="value" />.
	/// </summary>
	/// <param name="value"> The deserialized cached value. </param>
	/// <returns>
	/// A message result wrapping <paramref name="value" />, or <see langword="null" /> if
	/// <paramref name="value" /> is not assignable to this message's declared result type -- the same
	/// fail-open signal a cache-hit type mismatch already produces elsewhere.
	/// </returns>
	/// <remarks>
	/// <see cref="ICacheable{T}" /> overrides this with a default interface method that knows
	/// <c>T</c> at its declaration site, so the caching middleware can build the typed result directly
	/// through the interface instead of resolving <c>IDispatchAction&lt;TResponse&gt;</c> and invoking
	/// a constructor by reflection. The base implementation returns <see langword="null" /> because the
	/// non-generic interface has no type to construct.
	/// </remarks>
	IMessageResult? CreateCachedResult(object? value) => null;
}

/// <summary>
/// Declares that a message returning <typeparamref name="T" /> may have its result cached.
/// </summary>
/// <typeparam name="T"> The result type. </typeparam>
/// <remarks>
/// The cache members are on the non-generic <see cref="ICacheable" /> this derives from; the type
/// parameter only ties the message to its result. Implementing this satisfies both.
/// </remarks>
public interface ICacheable<T> : ICacheable, IDispatchAction<T>
{
	/// <inheritdoc cref="ICacheable.CreateCachedResult" />
	/// <remarks>
	/// <c>T</c> is known here at compile time, so this builds <see cref="CachedMessageResult{T}" />
	/// directly -- no <c>Type.MakeGenericType</c>, no constructor reflection.
	/// </remarks>
	IMessageResult? ICacheable.CreateCachedResult(object? value) =>
		value is T typed ? new CachedMessageResult<T>(typed) : null;
}
