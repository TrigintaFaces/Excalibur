// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Default implementation of cache key builder that creates consistent cache keys for dispatch actions. Uses JSON serialization for
/// generating stable keys based on action content.
/// </summary>
/// <param name="serializer"> The JSON serializer for creating consistent keys from action data. </param>
/// <param name="logger"> Optional logger for reporting cache key generation issues. </param>
public sealed partial class DefaultCacheKeyBuilder(DispatchJsonSerializer serializer, ILogger<DefaultCacheKeyBuilder>? logger = null) : ICacheKeyBuilder
{
	private const int ReflectionFallbackEventId = 2550;
	private const int SerializationFallbackEventId = 2551;
	private readonly ILogger<DefaultCacheKeyBuilder> _logger = logger ?? NullLogger<DefaultCacheKeyBuilder>.Instance;

	/// <inheritdoc />
	[RequiresUnreferencedCode("Cache key generation may use JSON serialization which requires unreferenced code")]
	[RequiresDynamicCode("Cache key generation may use JSON serialization which requires dynamic code generation")]
	public string? CreateKey(IDispatchAction action, IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(action);
		ArgumentNullException.ThrowIfNull(context);

		// Resolve the base key. A null result means "no derivable cache identity" — the caller skips caching.
		// Key building is infallible: a reflection failure, an unresolvable ICacheable<T> key, an unnamed runtime
		// type, or an unserializable action all yield null (skip) — never an exception, and never a fabricated key
		// (no identity hash, no serialize-guess on the reflection-failure path).
		var baseKey = ResolveBaseKey(action);
		if (baseKey is null)
		{
			return null;
		}

		var tenant = context.GetTenantId() ?? "global";
		var user = context.GetUserId() ?? "anonymous";

		var fullKey = $"{tenant}:{user}:{baseKey}";
		return Hash(fullKey);
	}

	/// <summary>
	/// Resolves the base cache key for <paramref name="action" />, or <see langword="null" /> when no cache
	/// identity can be derived (the caller then skips caching).
	/// </summary>
	/// <param name="action"> The action to derive a base key for. </param>
	/// <returns>
	/// The base key, or <see langword="null" /> when the action declares <c>ICacheable&lt;T&gt;</c> but its key
	/// cannot be resolved, when the runtime type has no <see cref="System.Type.FullName" />, or when a
	/// non-cacheable action cannot be serialized. Never throws for a "cannot derive a key" condition.
	/// </returns>
	[RequiresUnreferencedCode("Cache key generation may use JSON serialization which requires unreferenced code")]
	[RequiresDynamicCode("Cache key generation may use JSON serialization which requires dynamic code generation")]
	private string? ResolveBaseKey(IDispatchAction action)
	{
		switch (TryGetDeclaredCacheKey(action, out var cacheKey))
		{
			case CacheKeySource.Declared:
				return cacheKey;

			case CacheKeySource.ReflectionFailed:
				// The action declared ICacheable<T> but its key could not be resolved. Skip caching — never
				// fabricate a key (no identity hash, no serialize-guess): the action explicitly declared that
				// default serialization is NOT its cache identity, so guessing one risks a false cross-request hit.
				return null;

			default:
				// Not ICacheable: derive a content-stable key from serialization. Fail open — an unnamed or
				// unserializable action yields null (skip) rather than failing the request.
				var fullName = action.GetType().FullName;
				if (fullName is null)
				{
					return null;
				}

				try
				{
					return $"dispatch:{fullName}:{serializer.Serialize(action, action.GetType())}";
				}
				catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
				{
					LogSerializationFallback(action.GetType().Name, ex.GetType().Name);
					return null;
				}
		}
	}

	/// <summary>
	/// Detects an <c>ICacheable&lt;T&gt;</c> implementation via reflection and invokes <c>GetCacheKey()</c>, handling
	/// generic type variance by inspecting the action's interfaces at runtime.
	/// </summary>
	/// <param name="action"> The action to inspect. </param>
	/// <param name="cacheKey"> The resolved cache key when the result is <see cref="CacheKeySource.Declared" />; otherwise <see langword="null" />. </param>
	/// <returns> How the action's cache identity was (or was not) resolved. </returns>
	[RequiresUnreferencedCode("Uses reflection to detect and invoke ICacheable<T>.GetCacheKey() method")]
	private CacheKeySource TryGetDeclaredCacheKey(IDispatchAction action, out string? cacheKey)
	{
		try
		{
			var actionType = action.GetType();

			// Find any ICacheable<T> interface (regardless of T).
			var cacheableInterface = actionType.GetInterfaces()
				.FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICacheable<>));

			if (cacheableInterface != null
				&& cacheableInterface.GetMethod("GetCacheKey") is { } getCacheKeyMethod
				&& getCacheKeyMethod.Invoke(action, null) is string key)
			{
				cacheKey = key;
				return CacheKeySource.Declared;
			}

			cacheKey = null;
			return CacheKeySource.NotCacheable;
		}
		catch (Exception ex) when (ex is System.Reflection.TargetException
								or System.Reflection.TargetInvocationException
								or InvalidOperationException
								or MemberAccessException
								or TypeLoadException)
		{
			// Reflection failed for an action that declared ICacheable<T>. Skip caching — do NOT fabricate a key
			// (no identity hash, no serialize-guess), which would risk a false cross-request cache hit.
			LogReflectionFallback(action.GetType().Name, ex.GetType().Name);
			cacheKey = null;
			return CacheKeySource.ReflectionFailed;
		}
	}

	/// <summary> Describes how a cache key was (or was not) resolved for an action. </summary>
	private enum CacheKeySource
	{
		/// <summary> The action declares <c>ICacheable&lt;T&gt;</c> and its key was resolved. </summary>
		Declared,

		/// <summary> The action is not <c>ICacheable&lt;T&gt;</c>; derive a content key from serialization. </summary>
		NotCacheable,

		/// <summary> The action declares <c>ICacheable&lt;T&gt;</c> but reflection failed; skip caching. </summary>
		ReflectionFailed,
	}

	[LoggerMessage(ReflectionFallbackEventId, LogLevel.Debug,
		"Reflection failed for ICacheable<T> on type {TypeName} ({ExceptionType}). Skipping caching (no cache key).")]
	private partial void LogReflectionFallback(string typeName, string exceptionType);

	[LoggerMessage(SerializationFallbackEventId, LogLevel.Debug,
		"Serialization failed building a cache key for type {TypeName} ({ExceptionType}). Skipping caching (no cache key).")]
	private partial void LogSerializationFallback(string typeName, string exceptionType);

	private static string Hash(string input)
	{
		var bytes = Encoding.UTF8.GetBytes(input);
		var hash = SHA256.HashData(bytes);
		return Convert.ToBase64String(hash)
			.Replace("=", string.Empty, StringComparison.Ordinal)
			.Replace('/', '_')
			.Replace('+', '-');
	}
}
