// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Default implementation of cache key builder that creates consistent cache keys for dispatch actions. Uses JSON serialization for
/// generating stable keys based on action content.
/// </summary>
/// <param name="serializer"> The JSON serializer for creating consistent keys from action data. </param>
/// <param name="logger"> Optional logger for reporting cache key generation issues. </param>
public sealed partial class DefaultCacheKeyBuilder(IJsonSerializer serializer, ILogger<DefaultCacheKeyBuilder>? logger = null) : ICacheKeyBuilder
{
	private const int ReflectionFallbackEventId = 2550;
	private readonly ILogger<DefaultCacheKeyBuilder> _logger = logger ?? NullLogger<DefaultCacheKeyBuilder>.Instance;

	/// <inheritdoc />
	[RequiresUnreferencedCode("Cache key generation may use JSON serialization which requires unreferenced code")]
	[RequiresDynamicCode("Cache key generation may use JSON serialization which requires dynamic code generation")]
	public string CreateKey(IDispatchAction action, IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(action);
		ArgumentNullException.ThrowIfNull(context);

		// Try to get cache key from ICacheable<T> interface (works with any T)
		var baseKey = TryGetCacheKeyFromInterface(action, out var cacheKey)
			? cacheKey
			:
			// Fallback to serialization if not ICacheable
			$"dispatch:{action.GetType().FullName}:{serializer.Serialize(action, action.GetType())}";

		var tenant = context.TenantId ?? "global";
		var user = context.UserId ?? "anonymous";

		var fullKey = $"{tenant}:{user}:{baseKey}";
		var hashedKey = Hash(fullKey);
		return hashedKey;
	}

	/// <summary>
	/// Attempts to extract the cache key by detecting ICacheable&lt;T&gt; interface implementation via reflection. This handles generic
	/// type variance by inspecting the action's interfaces at runtime.
	/// </summary>
	/// <param name="action"> The action to inspect. </param>
	/// <param name="cacheKey"> The extracted cache key if ICacheable&lt;T&gt; is implemented. </param>
	/// <returns> True if ICacheable&lt;T&gt; was found and GetCacheKey() was successfully invoked; otherwise false. </returns>
	[RequiresUnreferencedCode("Uses reflection to detect and invoke ICacheable<T>.GetCacheKey() method")]
	private bool TryGetCacheKeyFromInterface(IDispatchAction action, [NotNullWhen(true)] out string? cacheKey)
	{
		try
		{
			var actionType = action.GetType();

			// Find any ICacheable<T> interface (regardless of T)
			var cacheableInterface = actionType.GetInterfaces()
				.FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICacheable<>));

			if (cacheableInterface != null)
			{
				// Get the GetCacheKey() method from the interface definition
				var getCacheKeyMethod = cacheableInterface.GetMethod("GetCacheKey");

				if (getCacheKeyMethod != null)
				{
					// Invoke GetCacheKey() on the action instance
					var result = getCacheKeyMethod.Invoke(action, null);
					if (result is string key)
					{
						cacheKey = key;
						return true;
					}
				}
			}

			cacheKey = null;
			return false;
		}
		catch (Exception ex) when (ex is System.Reflection.TargetException
									or System.Reflection.TargetInvocationException
									or InvalidOperationException
									or MemberAccessException
									or TypeLoadException)
		{
			// Reflection failed — return fallback key based on type name + hash code
			// to avoid propagating exceptions from cache key building
			var typeName = action.GetType().Name;
			LogReflectionFallback(typeName, ex.GetType().Name);
			cacheKey = $"dispatch:fallback:{action.GetType().FullName ?? typeName}:{action.GetHashCode():X8}";
			return true;
		}
	}

	[LoggerMessage(ReflectionFallbackEventId, LogLevel.Debug,
		"Reflection failed for ICacheable<T> on type {TypeName} ({ExceptionType}). Using fallback cache key.")]
	private partial void LogReflectionFallback(string typeName, string exceptionType);

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
