// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Immutable;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Caching.Diagnostics;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Validation;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Middleware that provides caching functionality for dispatch operations.
/// </summary>
/// <param name="meterFactory">The meter factory for DI-managed meter lifecycle.</param>
/// <param name="cache">The hybrid cache instance.</param>
/// <param name="keyBuilder">The cache key builder.</param>
/// <param name="services">The service provider for resolving dependencies.</param>
/// <param name="options">Configuration options for caching.</param>
/// <param name="logger">Logger for startup warnings and diagnostics.</param>
/// <param name="globalPolicy">Optional global cache policy.</param>
/// <param name="tagTracker">Optional tag tracker for registering cache key-to-tag mappings on cache miss.</param>
/// <param name="cacheCircuitBreaker">
/// Optional circuit breaker guarding cache-backend operations. When supplied and
/// <see cref="CacheCircuitBreakerOptions.Enabled"/> is <see langword="true"/>, repeated cache failures trip the
/// breaker and, while open, the middleware skips the cache and executes the handler directly (fail-open).
/// When <see langword="null"/>, no breaker gating is applied (fail-open via <see cref="CacheResilienceOptions.EnableFallback"/> still applies).
/// </param>
/// <remarks>
/// <para>
/// <b>AOT Compatibility:</b> This middleware supports dual-path execution for Native AOT:
/// </para>
/// <list type="bullet">
/// <item><b>JIT path:</b> Uses <c>MakeGenericType</c> for per-message cache policy resolution and
/// <c>CachedMessageResult&lt;T&gt;</c> construction.</item>
/// <item><b>AOT path:</b> Uses <see cref="CachePolicyRegistry"/> populated at DI composition time
/// via <c>AddCachePolicy&lt;TMessage, TPolicy&gt;()</c>. Result wrapping falls back to
/// <c>CachedObjectMessageResult</c>. Return value extraction uses <c>IMessageResult&lt;T&gt;</c>
/// pattern matching instead of reflection.</item>
/// </list>
/// <para>
/// For AOT scenarios, register per-message cache policies explicitly:
/// <code>services.AddCachePolicy&lt;MyQuery, MyCachePolicy&gt;();</code>
/// </para>
/// </remarks>
[SuppressMessage(
	"Design",
	"CA1506:AvoidExcessiveClassCoupling",
	Justification = "The caching middleware is the single seam every cacheable message kind, cache store, serializer and diagnostic surface passes through; the entry-attribution guard added here reads types it already handled.")]
internal sealed class CachingMiddleware(
	IMeterFactory meterFactory,
	HybridCache cache,
	ICacheKeyBuilder keyBuilder,
	IServiceProvider services,
	IOptions<CacheOptions> options,
	ILogger<CachingMiddleware> logger,
	IResultCachePolicy? globalPolicy = null,
	ICacheTagTracker? tagTracker = null,
	ICircuitBreakerPolicy? cacheCircuitBreaker = null) : IDispatchMiddleware
{
	/// <summary>
	/// Maximum number of entries allowed in each interface resolution cache.
	/// When the cap is reached, new lookups compute the interface without caching to prevent unbounded memory growth.
	/// </summary>
	private const int MaxCacheEntries = 1024;

	private static readonly ConcurrentDictionary<Type, Type?> _cacheableInterfaceCache = new();
	private static readonly ConcurrentDictionary<Type, Type?> _actionInterfaceCache = new();

	private static readonly CompositeFormat MessageTypeNotDispatchActionFormat =
		CompositeFormat.Parse(Resources.CachingMiddleware_MessageTypeNotDispatchActionFormat);

	private readonly Counter<long> _cacheHitCounter = meterFactory.Create(DispatchCachingTelemetryConstants.MeterName).CreateCounter<long>("dispatch.cache.hits", description: "Number of cache hits");
	private readonly Counter<long> _cacheMissCounter = meterFactory.Create(DispatchCachingTelemetryConstants.MeterName).CreateCounter<long>("dispatch.cache.misses", description: "Number of cache misses");
	private readonly Counter<long> _cacheTimeoutCounter = meterFactory.Create(DispatchCachingTelemetryConstants.MeterName).CreateCounter<long>("dispatch.cache.timeouts", description: "Number of cache operation timeouts");
	private readonly Histogram<double> _cacheLatencyHistogram = meterFactory.Create(DispatchCachingTelemetryConstants.MeterName).CreateHistogram<double>("dispatch.cache.duration", unit: "ms", description: "Cache operation latency in milliseconds");

	// Random.Shared is thread-safe (.NET 6+) -- no need for ThreadStatic

	// Cache keys whose value factory is running on THIS logical call. HybridCache collapses concurrent
	// callers of one key onto a single in-flight operation, so a cached handler that dispatches -- directly
	// or transitively -- a message resolving to the SAME key would join its own stampede and await a
	// completion only its own caller can produce. That is a cycle in the wait-for graph, and it does not
	// resolve.
	//
	// It used to be survivable by accident: a per-caller deadline sat around the cache call and turned the
	// cycle into a timeout plus a duplicate execution. That deadline was removed because it also bounded
	// handler execution and destroyed stampede protection, which means the accidental escape is gone and
	// the cycle would now hang for good. AsyncLocal flows into the nested dispatch, so re-entry is
	// detectable exactly where it happens -- and a thrown error naming both messages is worth far more to
	// the consumer than a hang with no stack to read.
	private static readonly AsyncLocal<ImmutableHashSet<string>?> KeysInFlight = new();

	private readonly CacheOptions _options = options.Value;
	private readonly IResultCachePolicy? _globalPolicy = globalPolicy ?? options.Value.GlobalPolicy;
	private bool _startupWarningEmitted;

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Cache;

	/// <inheritdoc />
	[UnconditionalSuppressMessage("AOT", "IL2046:RequiresUnreferencedCode mismatch",
		Justification = "This implementation reflects; IDispatchMiddleware does not declare that, because a consumer-authored "
		+ "middleware need not reflect and must not inherit the claim. This type is internal, so the requirement cannot reach a "
		+ "consumer through it; it is declared on the caching registration methods, which is where a consumer opts in.")]
	[UnconditionalSuppressMessage("AOT", "IL3051:RequiresDynamicCode mismatch",
		Justification = "This implementation requires runtime code generation; IDispatchMiddleware does not declare that, because "
		+ "a consumer-authored middleware need not. This type is internal, so the requirement is declared on the caching "
		+ "registration methods, which is where a consumer opts in.")]
	[RequiresUnreferencedCode("JIT path uses MakeGenericType and Type.GetInterfaces for dynamic cache policy resolution. AOT path uses CachePolicyRegistry.")]
	[RequiresDynamicCode("JIT path uses MakeGenericType for CachedMessageResult<T> and IResultCachePolicy<T> resolution. AOT path uses registry lookups.")]
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		if (!_options.Enabled)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		if (!_startupWarningEmitted)
		{
			_startupWarningEmitted = true;
			EmitStartupWarnings();
		}

		if (message is not IDispatchAction { } action)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var key = keyBuilder.CreateKey(action, context);

		// No derivable cache identity (e.g. an unresolvable ICacheable<T> key, an unnamed type, or an
		// unserializable action) → fail open: skip caching and invoke the underlying operation. The key builder
		// is infallible (never throws for a "cannot derive a key" condition), so a null key is the documented
		// "do not cache" signal — a cross-cutting cache must never break the core operation.
		if (key is null)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Check if message implements any ICacheable<T> interface
#pragma warning disable IL2072 // DynamicallyAccessedMembers requirement on GetCacheableInterface
		var messageType = message.GetType();
		var cacheableInterface = GetCacheableInterface(messageType);
#pragma warning restore IL2072
		var isInterfaceCacheable = cacheableInterface != null;
		var isAttrCacheable = messageType.IsDefined(typeof(CacheResultAttribute), inherit: true);

		// If not cacheable at all, short-circuit
		if (!isInterfaceCacheable && !isAttrCacheable)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Handle interface-based caching via reflection
		if (isInterfaceCacheable)
		{
			return await HandleInterfaceCacheableReflectionAsync(message, key, context, nextDelegate, cancellationToken)
				.ConfigureAwait(false);
		}

		// Fallback to attribute-based cache
		if (isAttrCacheable)
		{
			return await HandleAttributeCacheableAsync(message, key, context, nextDelegate, cancellationToken).ConfigureAwait(false);
		}

		return MessageResult.Success();
	}

	/// <summary>
	/// Resolves the ICacheable interface for a type using a bounded cache.
	/// </summary>
	[UnconditionalSuppressMessage("AOT", "IL2070:DynamicallyAccessedMembers",
		Justification = "GetInterfaces is used for well-known ICacheable<> interface resolution. Types implementing ICacheable are preserved by DI registration.")]
	private static Type? GetCacheableInterface([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type messageType)
	{
		if (_cacheableInterfaceCache.TryGetValue(messageType, out var cached))
		{
			return cached;
		}

		if (_cacheableInterfaceCache.Count >= MaxCacheEntries)
		{
			// Cache full -- compute without caching
			return messageType.GetInterfaces()
				.FirstOrDefault(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICacheable<>));
		}

		var resolved = messageType.GetInterfaces()
			.FirstOrDefault(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICacheable<>));

		// Resolution is a pure function of the type, so a lost race stores the same answer.
		_ = _cacheableInterfaceCache.TryAdd(messageType, resolved);

		return resolved;
	}

	/// <summary>
	/// Resolves the IDispatchAction interface for a type using a bounded cache.
	/// </summary>
	[UnconditionalSuppressMessage("AOT", "IL2070:DynamicallyAccessedMembers",
		Justification = "GetInterfaces is used for well-known IDispatchAction<> interface resolution. Types implementing IDispatchAction are preserved by DI registration.")]
	private static Type? GetActionInterface([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type messageType)
	{
		if (_actionInterfaceCache.TryGetValue(messageType, out var cached))
		{
			return cached;
		}

		if (_actionInterfaceCache.Count >= MaxCacheEntries)
		{
			// Cache full -- compute without caching
			return messageType.GetInterfaces()
				.FirstOrDefault(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDispatchAction<>));
		}

		var resolved = messageType.GetInterfaces()
			.FirstOrDefault(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDispatchAction<>));

		// Resolution is a pure function of the type, so a lost race stores the same answer.
		_ = _actionInterfaceCache.TryAdd(messageType, resolved);

		return resolved;
	}

	/// <summary>
	/// Extracts the return value from a message result using interface pattern match.
	/// </summary>
	/// <param name="processedResult">The message result to extract from.</param>
	/// <returns>The extracted return value, or null if not found.</returns>
	/// <remarks>
	/// Uses the non-generic <see cref="IMessageResult.UntypedReturnValue"/> accessor instead of reflection,
	/// making this AOT-safe without <c>Type.GetProperty</c>. Unlike an <c>IMessageResult&lt;object&gt;</c>
	/// pattern match, this also extracts value-type results (e.g. <c>IMessageResult&lt;int&gt;</c>), which
	/// are not covariantly assignable to <c>IMessageResult&lt;object&gt;</c>.
	/// </remarks>
	private static object? ExtractReturnValue(IMessageResult? processedResult)
		=> processedResult?.UntypedReturnValue;

	/// <summary>
	/// Gets the ICacheable interface information from a message.
	/// </summary>
	/// <param name="message">The message to extract cacheable information from.</param>
	/// <returns>The cacheable information, or null if the message does not implement ICacheable.</returns>
	[UnconditionalSuppressMessage("Trimming", "IL2075:Type.GetInterfaces may break with trimming",
		Justification = "ICacheable interface is a well-known pattern with stable member names")]
	[UnconditionalSuppressMessage("Trimming", "IL2075:Type.GetMethod may break with trimming",
		Justification = "ICacheable interface members are accessed via nameof for stability")]
	[UnconditionalSuppressMessage("Trimming", "IL2075:Type.GetProperty may break with trimming",
		Justification = "ICacheable interface members are accessed via nameof for stability")]
	private static CacheableInfo? GetCacheableInfo(IDispatchMessage message)
	{
#pragma warning disable IL2072 // DynamicallyAccessedMembers requirement on GetCacheableInterface
		var messageType = message.GetType();
		var cacheableInterface = GetCacheableInterface(messageType);
#pragma warning restore IL2072

		if (cacheableInterface == null)
		{
			return null;
		}

		return new CacheableInfo
		{
			Interface = cacheableInterface,
			ShouldCacheMethod = cacheableInterface.GetMethod(nameof(ICacheable<>.ShouldCache))!,
			ExpirationProperty = cacheableInterface.GetProperty(nameof(ICacheable<>.ExpirationSeconds))!,
			GetCacheTagsMethod = cacheableInterface.GetMethod(nameof(ICacheable<>.GetCacheTags))!,
		};
	}

	/// <summary>
	/// Deserializes a cached value from its stored form.
	/// </summary>
	/// <param name="cachedResult">The cached value to deserialize.</param>
	/// <returns>The deserialized value.</returns>
	[RequiresUnreferencedCode("Resolves the stored type name against the loaded assemblies and deserializes by System.Type, both of which require types that trimming may remove.")]
	[RequiresDynamicCode("Deserializes the stored value by System.Type, which requires runtime code generation.")]
	private static object DeserializeCachedValue(CachedValue cachedResult)
	{
		var cachedValue = cachedResult.Value;

		// Handle JsonElement deserialization if needed
		if (cachedValue is JsonElement jsonElement && !string.IsNullOrEmpty(cachedResult.TypeName))
		{
			try
			{
				var targetType = ResolveTypeByName(cachedResult.TypeName);
				if (targetType != null)
				{
					var json = jsonElement.GetRawText();
					cachedValue = JsonSerializer.Deserialize(json, targetType)!;
				}
			}
			catch (JsonException)
			{
				// If JSON deserialization fails, continue with JsonElement as fallback
			}
			catch (InvalidOperationException)
			{
				// Type resolution failed — expected in trimmed/AOT scenarios
			}
		}

		if (cachedValue is null)
		{
			throw new InvalidOperationException(
				$"Failed to deserialize cached value for type '{cachedResult.TypeName}'.");
		}

		return cachedValue;
	}

	/// <summary>
	/// Handles a cached result by deserializing and returning it or executing the handler.
	/// </summary>
	/// <param name="cachedResult">The cached result, or null if not cached.</param>
	/// <param name="message">The message to process.</param>
	/// <param name="context">The message context.</param>
	/// <param name="nextDelegate">The next middleware delegate.</param>
	/// <param name="logger">Logger used to report an entry whose stored type does not match the requested one.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The message result.</returns>
	[RequiresUnreferencedCode("Calls DeserializeCachedValue, which resolves the stored type name by reflection.")]
	[RequiresDynamicCode("Calls DeserializeCachedValue which uses dynamic code for deserialization")]
	private static async Task<(IMessageResult Result, bool ServedFromCache)> HandleCachedResultAsync(
		CachedValue? cachedResult,
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		ILogger logger,
		CancellationToken cancellationToken)
	{
		// If we have a cache hit
		if (cachedResult?.HasExecuted == true)
		{
			// Check if this was from cache or freshly executed
			if (context.Items.TryGetValue("Dispatch:OriginalResult", out var originalResult))
			{
				// Fresh execution - return the original result
				_ = context.Items.Remove("Dispatch:OriginalResult");
				if (context.Result is null && originalResult is IMessageResult originalMessageResult)
				{
					context.Result = ExtractReturnValue(originalMessageResult);
				}

				return ((IMessageResult)originalResult, false);
			}

			if (cachedResult is { ShouldCache: true, Value: not null })
			{
				// A cache key does not commit to the action that produced it. On the ICacheable<T> path the
				// key is whatever GetCacheKey() returns, so two action types can return the same string and
				// address one entry. The response-type check further down catches that only when the two
				// declare DIFFERENT response types; where they share one — a name query and an email query
				// both returning string — the stored value is indistinguishable from a legitimate hit, and
				// the second caller would receive the first's data. Attribute the entry to its storing
				// action instead, and decline one that is not this action's. Checked before deserializing:
				// there is no reason to reconstruct a value that cannot be served.
				if (!BelongsToAction(cachedResult.ActionTypeName, message.GetType()))
				{
					// Fail open, matching the response-type mismatch below: serve a freshly computed result
					// rather than another action's value. The entry is left for its owner to overwrite or
					// expire, so the colliding action simply goes uncached rather than evicting in a loop.
					LogCachedActionMismatch(logger, cachedResult.ActionTypeName, DescribeActionType(message.GetType()));
					return (await nextDelegate(message, context, cancellationToken).ConfigureAwait(false), false);
				}

				// Cache hit - deserialize value and return directly without calling handler
				var cachedValue = DeserializeCachedValue(cachedResult);

				// Determine the return type from the message type
				var messageType = message.GetType();
#pragma warning disable IL2072 // DynamicallyAccessedMembers requirement on GetActionInterface
				var actionInterface = GetActionInterface(messageType);
#pragma warning restore IL2072

				if (actionInterface is not null)
				{
					// Get the TResponse type from IDispatchAction<TResponse>
					var returnType = actionInterface.GetGenericArguments()[0];

					// Create CachedMessageResult<T> using reflection
					var resultInstance = CreateCachedMessageResult(returnType, cachedValue);
					if (resultInstance is null)
					{
						// The entry under this key holds another type. Fail open: drop to the handler and
						// serve a freshly computed result rather than returning the wrong type. The stale
						// entry is left for its owner to overwrite or expire.
						LogCachedValueTypeMismatch(logger, returnType.FullName, cachedValue.GetType().FullName);
						return (await nextDelegate(message, context, cancellationToken).ConfigureAwait(false), false);
					}

					context.Result = cachedValue;

					return (resultInstance, true);
				}

#pragma warning disable IL2075 // GetInterfaces on runtime type for diagnostic message only
				var implementedInterfaces = string.Join(
					", ",
					messageType.GetInterfaces().Select(static i => i.Name));
#pragma warning restore IL2075
				throw new InvalidOperationException(
					string.Format(
						CultureInfo.CurrentCulture,
						MessageTypeNotDispatchActionFormat,
						messageType.FullName,
						implementedInterfaces));
			}
		}

		// Fallback
		return (await nextDelegate(message, context, cancellationToken).ConfigureAwait(false), false);
	}

	/// <summary>
	/// Handles caching for messages using reflection to invoke ICacheable interface methods.
	/// </summary>
	/// <param name="message">The message to cache.</param>
	/// <param name="key">The cache key.</param>
	/// <param name="context">The message context.</param>
	/// <param name="nextDelegate">The next middleware delegate.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The message result.</returns>
	[RequiresUnreferencedCode("Calls HandleCachedResultAsync, which resolves the stored type name by reflection.")]
	[RequiresDynamicCode("Calls HandleCachedResultAsync which uses dynamic code for deserialization")]
	private async Task<IMessageResult> HandleInterfaceCacheableReflectionAsync(
		IDispatchMessage message,
		string key,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		// Get ICacheable<T> interface
		var cacheableInfo = GetCacheableInfo(message);
		if (cacheableInfo == null)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Check if would cache null (to determine if we should use caching at all)
		var wouldCacheNull = ShouldCache(message, result: null);
		if (!wouldCacheNull)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Get expiration and tags
		var expiration = GetExpiration(cacheableInfo, message);
		var tags = GetCacheTags(cacheableInfo, message);

		return await ExecuteWithCacheAsync(
			key,
			async ct => await CreateCacheValueAsync(message, context, nextDelegate, cacheableInfo, ct).ConfigureAwait(false),
			expiration,
			tags,
			message,
			context,
			nextDelegate,
			cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Gets the cache configuration (expiration and tags) for an attribute-based cacheable message.
	/// </summary>
	/// <param name="attr">The cache result attribute.</param>
	/// <returns>A tuple containing expiration time and cache tags.</returns>
	private (TimeSpan Expiration, string[] Tags) GetAttributeCacheConfiguration(CacheResultAttribute? attr)
	{
		var expiration = attr?.ExpirationSeconds > 0
			? TimeSpan.FromSeconds(attr.ExpirationSeconds)
			: _options.Behavior.DefaultExpiration;

		expiration = ApplyJitter(expiration);

		var tags = attr?.Tags ?? [];
		if (_options.DefaultTags.Length > 0)
		{
			tags = [.. tags, .. _options.DefaultTags];
		}

		return (expiration, tags);
	}

	/// <summary>
	/// Checks if a message type should be cached based on policy.
	/// </summary>
	/// <param name="message">The message to check.</param>
	/// <returns>True if the message type should be cached; otherwise, false.</returns>
	[UnconditionalSuppressMessage("AOT", "IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "GetMessagePolicy and ShouldCache take the registry path when RuntimeFeature.IsDynamicCodeSupported is false, and the ahead-of-time compiler substitutes that switch and removes the MakeGenericType branch, so no dynamic code is reachable here.")]
	private bool ShouldCacheMessageType(IDispatchMessage message)
	{
		// Pre-check: only verify the policy decision (ShouldCache), not result-dependent checks
		// like IgnoreNullResult or OnlyIfSuccess. Those checks are evaluated after the handler executes
		// with the actual return value in CreateAttributeCacheValueAsync/CreateCacheValueAsync.
		var messagePolicy = GetMessagePolicy(message);
		return messagePolicy == null || ShouldCache(message, result: null);
	}

	/// <summary>
	/// Creates a cached value for attribute-based caching by executing the message handler.
	/// </summary>
	/// <param name="message">The message to process.</param>
	/// <param name="context">The message context.</param>
	/// <param name="nextDelegate">The next middleware delegate.</param>
	/// <param name="attr">The cache result attribute.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The cached value.</returns>
	[UnconditionalSuppressMessage("AOT", "IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "GetMessagePolicy and ShouldCache take the registry path when RuntimeFeature.IsDynamicCodeSupported is false, and the ahead-of-time compiler substitutes that switch and removes the MakeGenericType branch, so no dynamic code is reachable here.")]
	private async Task<CachedValue> CreateAttributeCacheValueAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CacheResultAttribute? attr,
		CancellationToken cancellationToken)
	{
		var messageResult = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		var returnValue = ExtractReturnValue(messageResult);
		var shouldCache = ShouldCacheBasedOnPolicy(message, returnValue, attr, context);

		if (returnValue != null)
		{
			context.Result = returnValue;
			context.Items["Dispatch:Result"] = returnValue;
		}

		if (messageResult != null)
		{
			context.Items["Dispatch:OriginalResult"] = messageResult;
		}

		return new CachedValue
		{

			Value = returnValue,
			ShouldCache = shouldCache,
			HasExecuted = true,
			TypeName = returnValue?.GetType().AssemblyQualifiedName,
			ActionTypeName = DescribeActionType(message.GetType()),
		};
	}

	/// <summary>
	/// Gets the cache expiration time for a cacheable message.
	/// </summary>
	/// <param name="cacheableInfo">The cacheable interface information.</param>
	/// <param name="message">The message to get expiration for.</param>
	/// <returns>The cache expiration time.</returns>
	private TimeSpan GetExpiration(CacheableInfo cacheableInfo, IDispatchMessage message)
	{
		var expirationSeconds = cacheableInfo.GetExpirationSeconds(message);
		var expiration = expirationSeconds > 0
			? TimeSpan.FromSeconds(expirationSeconds)
			: _options.Behavior.DefaultExpiration;

		return ApplyJitter(expiration);
	}

	/// <summary>
	/// Gets the cache tags for a cacheable message.
	/// </summary>
	/// <param name="cacheableInfo">The cacheable interface information.</param>
	/// <param name="message">The message to get tags for.</param>
	/// <returns>The cache tags.</returns>
	private string[] GetCacheTags(CacheableInfo cacheableInfo, IDispatchMessage message)
	{
		var tags = cacheableInfo.GetTags(message);
		if (_options.DefaultTags.Length > 0)
		{
			tags = [.. tags, .. _options.DefaultTags];
		}

		return tags;
	}

	/// <summary>
	/// Creates a cached value by executing the message handler.
	/// </summary>
	/// <param name="message">The message to process.</param>
	/// <param name="context">The message context.</param>
	/// <param name="nextDelegate">The next middleware delegate.</param>
	/// <param name="cacheableInfo">The resolved ICacheable interface information for the message.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The cached value.</returns>
	[UnconditionalSuppressMessage("AOT", "IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "GetMessagePolicy and ShouldCache take the registry path when RuntimeFeature.IsDynamicCodeSupported is false, and the ahead-of-time compiler substitutes that switch and removes the MakeGenericType branch, so no dynamic code is reachable here.")]
	private async Task<CachedValue> CreateCacheValueAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CacheableInfo cacheableInfo,
		CancellationToken cancellationToken)
	{
		// Cache miss - execute the delegate
		var messageResult = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

		// Get the return value if it's a generic result
		var returnValue = ExtractReturnValue(messageResult);

		// Honor BOTH the registered cache policy AND the message's own ICacheable<T>.ShouldCache
		// decision. On the interface caching path the policy alone previously drove the decision,
		// so an ICacheable result returning ShouldCache=false was silently cached anyway.
		var shouldCache = ShouldCache(message, returnValue)
			&& cacheableInfo.ShouldCache(message, returnValue);

		// Store the return value in context
		if (returnValue != null)
		{
			context.Result = returnValue;
			context.Items["Dispatch:Result"] = returnValue;
		}

		// Store the original message result in the item for returning
		if (messageResult != null)
		{
			context.Items["Dispatch:OriginalResult"] = messageResult;
		}

		// Create cached value that stores only the return value
		return new CachedValue
		{

			Value = returnValue,
			ShouldCache = shouldCache,
			HasExecuted = true,
			TypeName = returnValue?.GetType().AssemblyQualifiedName,
			ActionTypeName = DescribeActionType(message.GetType()),
		};
	}

	/// <summary>
	/// Handles caching for messages using the CacheResultAttribute.
	/// </summary>
	/// <param name="message">The message to cache.</param>
	/// <param name="key">The cache key.</param>
	/// <param name="context">The message context.</param>
	/// <param name="nextDelegate">The next middleware delegate.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The message result.</returns>
	[RequiresUnreferencedCode("Calls HandleCachedResultAsync, which resolves the stored type name by reflection.")]
	[RequiresDynamicCode("Calls HandleCachedResultAsync which uses dynamic code for deserialization")]
	private async Task<IMessageResult> HandleAttributeCacheableAsync(
		IDispatchMessage message,
		string key,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		var attr = message.GetType().GetCustomAttribute<CacheResultAttribute>(inherit: true);

		// Check if policy allows caching this message type
		if (!ShouldCacheMessageType(message))
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var (expiration, tags) = GetAttributeCacheConfiguration(attr);

		return await ExecuteWithCacheAsync(
			key,
			async ct => await CreateAttributeCacheValueAsync(message, context, nextDelegate, attr, ct).ConfigureAwait(false),
			expiration,
			tags,
			message,
			context,
			nextDelegate,
			cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Executes the cache lookup-or-create for a key with resilience: honors the cache circuit breaker
	/// (skip-when-open) and <see cref="CacheResilienceOptions.EnableFallback"/> fail-open on a
	/// non-cancellation cache-backend error.
	/// <para>
	/// The operation carries no deadline of its own. It is shared by every concurrent caller of the same
	/// key and it runs the handler inside itself, so a deadline placed here would bound handler execution
	/// and would be abandoned per-caller, destroying that sharing. Backend latency is bounded one layer
	/// down, by <see cref="TimeoutDistributedCache"/>.
	/// </para>
	/// </summary>
	/// <param name="key">The cache key.</param>
	/// <param name="valueFactory">Factory that executes the handler and produces the value to cache.</param>
	/// <param name="expiration">The cache entry expiration.</param>
	/// <param name="tags">The cache tags for the entry.</param>
	/// <param name="message">The message being processed.</param>
	/// <param name="context">The message context.</param>
	/// <param name="nextDelegate">The next middleware delegate.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The resolved message result.</returns>
	[RequiresUnreferencedCode("Calls CompleteCacheOperationAsync, which resolves the stored type name by reflection.")]
	[RequiresDynamicCode("Calls CompleteCacheOperationAsync which uses dynamic code for deserialization")]
	private async Task<IMessageResult> ExecuteWithCacheAsync(
		string key,
		Func<CancellationToken, ValueTask<CachedValue>> valueFactory,
		TimeSpan expiration,
		string[] tags,
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		// when the cache circuit breaker is open, skip the cache entirely and execute the handler
		// directly (fail-open). This avoids paying the per-request CacheTimeout while the backend is
		// known-unhealthy and gives it the configured OpenDuration to recover.
		if (IsCacheBreakerOpen())
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// No per-caller deadline is placed around GetOrCreateAsync. HybridCache runs the value factory --
		// the handler -- inside this call, and shares one in-flight operation between every concurrent
		// caller of the same key. A deadline here therefore bounds handler execution, and each caller
		// abandons the shared operation independently the moment its own timer fires: with a 200 ms budget
		// and a 400 ms handler, five concurrent callers produced SIX handler executions, because the
		// shared factory was cancelled and all five then fell open and ran the handler themselves. That is
		// worse than no caching, and it lands on exactly the slow handlers caching exists to protect.
		//
		// Backend latency is bounded where it is actually spent instead -- TimeoutDistributedCache bounds
		// each L2 call by Behavior.CacheTimeout from INSIDE the shared operation, so one slow read yields
		// one timeout, every waiting caller shares that outcome, and single-flight survives.
		var sw = ValueStopwatch.StartNew();
		CachedValue? cachedResult;

		// The cache runs the value factory -- which invokes the handler -- INSIDE GetOrCreateAsync, so a
		// fault raised by the factory's BODY and one raised by the CACHE escape that call at the same point.
		// They need opposite responses: a backend outage should fall open and run the handler, while a
		// handler that has already run and thrown must not be run a second time. Recording the fault here is
		// what lets the fail-open clause below tell them apart -- it is the last place the distinction still
		// exists. This is the same line the file draws for result handling after the try: ONLY a
		// cache-BACKEND failure fails open.
		Exception? factoryFault = null;

		async ValueTask<CachedValue> GuardedFactoryAsync(CancellationToken factoryToken)
		{
			try
			{
				return await valueFactory(factoryToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (factoryToken.IsCancellationRequested)
			{
				// The cache stack cancelled THIS factory -- it cancels by cancelling the token it handed in,
				// so an OperationCanceledException raised while that token is cancelled did not originate in
				// the handler's own logic. Deliberately NOT recorded as a factory fault: the cancellation
				// clause below must still fall open and run the handler, which for this case has not run.
				throw;
			}
			catch (Exception ex)
			{
				// Rethrown unchanged, so the caller observes exactly what its handler threw, with the
				// original stack trace. Nothing about this guard is observable from outside. A cancellation
				// reaching here has an UNCANCELLED factory token, so it is the handler's own -- the handler
				// ran, and must not run again.
				factoryFault = ex;
				throw;
			}
		}

		var inFlight = KeysInFlight.Value ?? ImmutableHashSet<string>.Empty;
		if (inFlight.Contains(key))
		{
			throw new CacheReentrancyException(
				$"Cached handler re-entered its own cache key '{key}' while producing it. A handler whose "
				+ "result is cached dispatched another message that resolves to the same cache key, directly "
				+ "or through a further dispatch. The inner dispatch would wait for the outer one to finish "
				+ "and the outer cannot finish until the inner returns, so the request would never complete. "
				+ "Give the messages distinct cache keys, or move the nested dispatch outside the cached "
				+ "handler.",
				key);
		}

		KeysInFlight.Value = inFlight.Add(key);
		try
		{
			cachedResult = await cache.GetOrCreateAsync(
				key,
				GuardedFactoryAsync,
				new HybridCacheEntryOptions
				{
					Expiration = expiration,
					Flags = _options.CacheMode == CacheMode.Distributed
						? HybridCacheEntryFlags.DisableLocalCache
						: HybridCacheEntryFlags.None,
				},
				tags,
				cancellationToken).ConfigureAwait(false);

			_cacheLatencyHistogram.Record(sw.Elapsed.TotalMilliseconds);

			// Deliberately NOT recording breaker success here. A normal return no longer means the backend
			// was healthy: TimeoutDistributedCache bounds each backend call and degrades a slow one to a
			// miss, so GetOrCreateAsync returns normally while the backend is failing every request.
			// Asserting success here would hold the breaker closed forever against a dead backend -- every
			// request paying the deadline twice and nothing ever cached. Backend health is reported by the
			// decorator, which is the only component that can observe it.
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested
			&& factoryFault is null)
		{
			// A cancellation that is neither the caller's nor the handler's came from the cache stack -
			// execute without caching. Real caller cancellation propagates. It is a cache-backend health
			// signal, so it counts toward the breaker.
			//
			// The two ARE distinguishable, and the factory token is where: the cache stack cancels by
			// cancelling the token it passes the factory, so a cancellation raised while that token is
			// uncancelled is the handler's own and is recorded as a factory fault above. Without this test a
			// handler that throws OperationCanceledException for its own reasons -- a domain deadline, a
			// linked token of its own -- falls open and runs a second time, performing its side effect twice
			// for one dispatch.
			_cacheTimeoutCounter.Add(1);
			_cacheLatencyHistogram.Record(sw.Elapsed.TotalMilliseconds);
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException
			and not CacheReentrancyException
			&& factoryFault is null
			&& _options.Resilience.EnableFallback)
		{
			// a non-cancellation cache-backend error (e.g. a RedisConnectionException that errors
			// FAST rather than slow) must NOT, under default options, become an application-level failure.
			// Fall back to executing the handler directly (fail-open, like IDistributedCache/HybridCache
			// skip-on-failure). When EnableFallback is false the error propagates (explicit fail-closed).
			_cacheLatencyHistogram.Record(sw.Elapsed.TotalMilliseconds);

			// The decorator already recorded this against the breaker on its way past; recording again here
			// would count one backend failure twice and trip the breaker at half its configured threshold.
			logger.LogWarning(
				ex,
				"Cache operation failed for key {CacheKey}; falling back to direct handler execution (EnableFallback=true).",
				key);
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			// Restore rather than Remove: AsyncLocal copy-on-write means a nested dispatch may have
			// published its own set, and assigning the value captured on entry is what unwinds this frame
			// exactly.
			//
			// Scope of the effect, measured rather than assumed: an AsyncLocal write inside an async method
			// does not flow back to that method's caller, because the state machine restores the captured
			// ExecutionContext at the end of the first synchronous segment. Writes therefore travel DOWN
			// into the value factory -- which is what lets the guard above see an outer key at all -- and
			// never back UP. So this restore does not, and cannot, protect a caller further out; its one
			// reachable effect is to unwind this frame before CompleteCacheOperationAsync runs after the
			// try. It is kept for that.
			KeysInFlight.Value = inFlight;
		}

		// Result handling (deserialization, poison-marker eviction, tag registration) runs OUTSIDE the
		// fail-open scope: ONLY a cache-BACKEND failure fails open. A result-handling fault — e.g. a corrupt
		// cached payload that fails to deserialize — is a data/logic error that MUST propagate, not be
		// silently swallowed as a cache miss (which would mask data corruption).
		return await CompleteCacheOperationAsync(key, cachedResult, tags, message, context, nextDelegate, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Gets a value indicating whether the cache circuit breaker is currently open (cache should be skipped).
	/// </summary>
	/// <returns><see langword="true"/> if a breaker is configured, enabled, and open; otherwise <see langword="false"/>.</returns>
	private bool IsCacheBreakerOpen()
		=> cacheCircuitBreaker is not null
			&& _options.Resilience.CircuitBreaker.Enabled
			&& cacheCircuitBreaker.State == CircuitState.Open;



	/// <summary>
	/// Determines if a result should be cached based on policies and attributes.
	/// </summary>
	/// <param name="message"> The message being processed. </param>
	/// <param name="returnValue"> The return value to potentially cache. </param>
	/// <param name="attr"> The cache result attribute. </param>
	/// <param name="context"> The message context. </param>
	/// <returns> True if the result should be cached; otherwise, false. </returns>
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "ShouldCache uses MakeGenericType which is guarded by RuntimeFeature.IsDynamicCodeSupported check.")]
	private bool ShouldCacheBasedOnPolicy(
		IDispatchMessage message,
		object? returnValue,
		CacheResultAttribute? attr,
		IMessageContext context) =>
		ShouldCache(message, returnValue)
		&& ((!attr?.OnlyIfSuccess ?? true)
			|| (((context.ValidationResult() as IValidationResult)?.IsValid ?? true)
				&& ((context.AuthorizationResult() as IAuthorizationResult)?.IsAuthorized ?? true)))
		&& ((!attr?.IgnoreNullResult ?? true) || (returnValue is not null));

	/// <summary>
	/// Gets the message-specific cache policy if one exists.
	/// </summary>
	/// <param name="message">The message to get the policy for.</param>
	/// <returns>The cache policy instance or null if none exists.</returns>
	/// <remarks>
	/// The runtime type of <paramref name="message" /> is used to build a closed <c>IResultCachePolicy&lt;TMessage&gt;</c> via
	/// <see cref="Type.MakeGenericType" />. If a service with an incompatible generic argument is registered, the lookup returns <see langword="null"/>,
	/// and the middleware falls back to the global policy.
	/// </remarks>
	[SuppressMessage("Design", "MA0038:Make method static", Justification = "Method uses services field from primary constructor")]
	[RequiresDynamicCode("JIT path calls System.Type.MakeGenericType(params Type[])")]
	private object? GetMessagePolicy(IDispatchMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		var messageType = message.GetType();

		if (!RuntimeFeature.IsDynamicCodeSupported)
		{
			// AOT path: use pre-populated CachePolicyRegistry instead of MakeGenericType.
			// Registry is populated at DI composition time via AddCachePolicy<TMessage, TPolicy>().
			return services.GetService<CachePolicyRegistry>()?.GetPolicy(messageType);
		}

		var policyType = typeof(IResultCachePolicy<>).MakeGenericType(messageType);
		return services.GetService(policyType);
	}

	/// <summary>
	/// Determines whether the result of a message should be cached based on configured policies.
	/// </summary>
	/// <param name="message">The message being processed.</param>
	/// <param name="result">The result of processing the message. Can be null for pre-checks.</param>
	/// <returns>True if the result should be cached; otherwise, false.</returns>
	/// <remarks>
	/// The message-specific policy is resolved with <see cref="GetMessagePolicy" /> and invoked via reflection. When an incompatible policy
	/// type is registered or the policy throws an exception, the invocation is caught and ignored so the global policy is evaluated instead.
	/// </remarks>
	[RequiresDynamicCode("JIT path calls GetMessagePolicy which uses MakeGenericType")]
	[UnconditionalSuppressMessage("Trimming", "IL2075:Type.GetMethod may break with trimming",
		Justification = "AOT-safe: AOT path uses CachePolicyRegistry delegate; JIT path uses reflection with null checks and try-catch")]
	private bool ShouldCache(IDispatchMessage message, object? result)
	{
		ArgumentNullException.ThrowIfNull(message);

		if (!RuntimeFeature.IsDynamicCodeSupported)
		{
			// AOT path: use pre-populated registry delegate — no reflection needed.
			var registry = services.GetService<CachePolicyRegistry>();
			var policyDelegate = registry?.GetPolicy(message.GetType());
			if (policyDelegate is not null)
			{
				try
				{
					return policyDelegate(services, message, result);
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					// Policy threw, fall back to global policy
					logger.LogWarning(ex,
						"Cache policy evaluation failed for message type {MessageType}, falling back to global policy",
						message.GetType().Name);
				}
			}

			return _globalPolicy?.ShouldCache(message, result) ?? true;
		}

		// JIT path: existing MakeGenericType + reflection invocation
		var policyInstance = GetMessagePolicy(message);
		if (policyInstance is not null)
		{
			var policyType = policyInstance.GetType();
			var method = policyType.GetMethod("ShouldCache");
			if (method != null)
			{
				try
				{
					var shouldCache = method.Invoke(policyInstance, [message, result]) as bool?;
					return shouldCache == true;
				}
				catch (TargetException)
				{
					// Type mismatch - policy doesn't match message type
				}
				catch (Exception ex)
				{
					// Policy threw, fall back to the global policy
					logger.LogWarning(ex,
						"Cache policy evaluation failed for message type {MessageType}, falling back to global policy",
						message.GetType().Name);
				}
			}
		}

		return _globalPolicy?.ShouldCache(message, result) ?? true;
	}

	[RequiresUnreferencedCode("Searches the loaded assemblies for the stored type name, which requires a type that trimming may remove.")]
	private static Type? ResolveTypeByName(string typeName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			var resolved = assembly.GetType(typeName, throwOnError: false, ignoreCase: false);
			if (resolved != null)
			{
				return resolved;
			}
		}

		var assemblySeparator = typeName.IndexOf(',', StringComparison.Ordinal);
		if (assemblySeparator <= 0)
		{
			return null;
		}

		var simpleTypeName = typeName[..assemblySeparator];
		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			var resolved = assembly.GetType(simpleTypeName, throwOnError: false, ignoreCase: false);
			if (resolved != null)
			{
				return resolved;
			}
		}

		return null;
	}

	/// <summary>Reports a cache entry whose stored value is not of the type the requesting action expects.</summary>
	/// <param name="logger"> The logger to report through. </param>
	/// <param name="expectedType"> The type the requesting action declared as its response. </param>
	/// <param name="actualType"> The type actually found under the cache key. </param>
	private static void LogCachedValueTypeMismatch(ILogger logger, string? expectedType, string? actualType) =>
		logger.LogWarning(
			"Cache entry holds '{ActualType}' but the requesting action expects '{ExpectedType}'. Two action types "
			+ "share a cache key. Serving the handler instead; give the actions distinct ICacheable<T>.GetCacheKey() values.",
			actualType,
			expectedType);

	/// <summary>Reports a cache entry that a different action type stored.</summary>
	/// <param name="logger"> The logger to report through. </param>
	/// <param name="storingAction"> The action identity recorded on the entry, if any. </param>
	/// <param name="requestingAction"> The action identity requesting the entry. </param>
	private static void LogCachedActionMismatch(ILogger logger, string? storingAction, string? requestingAction) =>
		logger.LogWarning(
			"Cache entry was stored by '{StoringAction}' but '{RequestingAction}' requested it. Two action types "
			+ "share a cache key. Serving the handler instead; give the actions distinct "
			+ "ICacheable<T>.GetCacheKey() values.",
			storingAction,
			requestingAction);

	/// <summary>
	/// Describes an action type for cache-entry attribution, or <see langword="null" /> when the type cannot
	/// be identified.
	/// </summary>
	/// <param name="actionType"> The runtime type of the action. </param>
	/// <returns> The attribution string, or <see langword="null" /> when the type has no full name. </returns>
	/// <remarks>
	/// <para>
	/// The assembly's simple name qualifies the type name so that two assemblies declaring the same type name
	/// stay distinct, while omitting the version, culture and public key so that upgrading a package does not
	/// invalidate every entry a consumer has stored. This is deliberately not the assembly-qualified name used
	/// by <see cref="CachedValue.TypeName" />, which needs maximum resolvability rather than stability because
	/// it drives deserialization.
	/// </para>
	/// <para>
	/// Internal rather than private so a test can build a correctly attributed entry from a type rather than
	/// by repeating this format as a literal, which would silently rot if the format ever changed.
	/// </para>
	/// </remarks>
	internal static string? DescribeActionType(Type actionType)
	{
		var fullName = actionType.FullName;
		return fullName is null ? null : $"{actionType.Assembly.GetName().Name}/{fullName}";
	}

	/// <summary>
	/// Determines whether an entry stored under <paramref name="storedActionTypeName" /> may be served to
	/// <paramref name="requestingActionType" />.
	/// </summary>
	/// <param name="storedActionTypeName"> The action identity recorded on the entry, if any. </param>
	/// <param name="requestingActionType"> The runtime type of the action requesting the entry. </param>
	/// <returns>
	/// <see langword="true" /> only when both identities are known and equal. An entry that carries no
	/// identity — written before this attribution existed, or by an action whose type could not be named —
	/// is not attributable, so it is never served; the caller falls through to the handler.
	/// </returns>
	private static bool BelongsToAction(string? storedActionTypeName, Type requestingActionType)
	{
		var requesting = DescribeActionType(requestingActionType);

		return storedActionTypeName is not null
			&& requesting is not null
			&& string.Equals(storedActionTypeName, requesting, StringComparison.Ordinal);
	}

	[RequiresDynamicCode("JIT path uses Type.MakeGenericType to construct CachedMessageResult<T>.")]
	private static IMessageResult? CreateCachedMessageResult(Type returnType, object cachedValue)
	{
		// A cache key is not unique across action types. The key comes from the action itself via
		// ICacheable<T>.GetCacheKey(), so two different actions can return the same string and address one
		// entry — "user:{UserId}" on both a user query and a permissions query is ordinary code. Handing
		// that entry to the wrong action returns another type's data to the caller.
		//
		// The dynamic-code path below happens to catch this, because invoking the generic wrapper's
		// constructor with a mismatched value throws. The ahead-of-time path takes object? and does not,
		// so it would return the wrong type silently and only where no test that runs under dynamic code
		// can observe it. The check therefore lives here, above the split, where both paths share it.
		if (!returnType.IsInstanceOfType(cachedValue))
		{
			return null;
		}

		if (!RuntimeFeature.IsDynamicCodeSupported)
		{
			// AOT path: MakeGenericType is unavailable. Use non-generic wrapper.
			return new CachedObjectMessageResult(cachedValue);
		}

		var resultWrapperType = typeof(CachedMessageResult<>).MakeGenericType(returnType);
		var constructor = resultWrapperType.GetConstructors()
			.FirstOrDefault(static ctor => ctor.GetParameters().Length == 1)
			?? throw new InvalidOperationException(
				$"No suitable constructor found for cached result wrapper type '{resultWrapperType.FullName}'.");

		var resultInstance = constructor.Invoke([cachedValue]);
		return (IMessageResult)resultInstance;
	}

	/// <summary>
	/// Emits one-time startup warnings for misconfigured cache scenarios.
	/// </summary>
	private void EmitStartupWarnings()
	{
		if (_options.CacheMode is CacheMode.Distributed or CacheMode.Hybrid)
		{
			var distributedCache = services.GetService(typeof(Microsoft.Extensions.Caching.Distributed.IDistributedCache));
			if (distributedCache?.GetType().Name == "MemoryDistributedCache")
			{
				logger.LogWarning(
					"Dispatch caching is configured for {CacheMode} mode but only MemoryDistributedCache is registered. " +
					"HybridCache treats MemoryDistributedCache as a no-op L2 backend, so entries will only be cached in-memory (L1). " +
					"Register a real IDistributedCache implementation (e.g., Redis, SQL Server) for distributed caching to work.",
					_options.CacheMode);
			}
		}
	}

	/// <summary>
	/// Completes a cache operation after <c>GetOrCreateAsync</c> returns: evicts non-cacheable markers,
	/// records hit/miss telemetry, registers tag→key mappings for cacheable entries, and resolves the result.
	/// </summary>
	/// <param name="key">The cache key.</param>
	/// <param name="cachedResult">The value returned from HybridCache (fresh or served).</param>
	/// <param name="tags">The cache tags for the entry.</param>
	/// <param name="message">The message being processed.</param>
	/// <param name="context">The message context.</param>
	/// <param name="nextDelegate">The next middleware delegate.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The resolved message result.</returns>
	[RequiresUnreferencedCode("Calls HandleCachedResultAsync, which resolves the stored type name by reflection.")]
	[RequiresDynamicCode("Calls HandleCachedResultAsync which uses dynamic code for deserialization")]
	private async Task<IMessageResult> CompleteCacheOperationAsync(
		string key,
		CachedValue? cachedResult,
		string[] tags,
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		// Dispatch:OriginalResult is set ONLY inside the factory on real execution, so its presence is the
		// authoritative "this request executed the handler" signal. Capture it before HandleCachedResultAsync
		// removes it.
		var freshlyExecuted = context.Items.ContainsKey("Dispatch:OriginalResult");

		// HybridCache stores whatever the factory returns — including non-cacheable
		// (ShouldCache=false) markers, because HybridCacheEntryFlags are per-call and cannot be set from
		// inside the factory. When the handler just executed and produced a non-cacheable result, evict the
		// stored marker so a subsequent identical request re-evaluates the handler instead of getting a
		// cache hit that still re-executes for the full TTL.
		if (freshlyExecuted && cachedResult is { ShouldCache: false })
		{
			await RemovePoisonMarkerAsync(key, cancellationToken).ConfigureAwait(false);
		}
		else
		{
		}

		// register a tag→key mapping exactly when a cacheable tagged entry is persisted — gate on the
		// cache OUTCOME (ShouldCache:true, Value:not null), not on "did the local factory run". This both
		// stops registering tags for non-cacheable markers AND registers the key when an entry was resolved
		// from shared L2 without the local factory running (cross-instance tag invalidation).
		if (tagTracker is not null
			&& tags is { Length: > 0 }
			&& cachedResult is { ShouldCache: true, Value: not null })
		{
			await RegisterTagKeysAsync(tagTracker, key, tags, cancellationToken).ConfigureAwait(false);
		}

		// Recorded AFTER the read-side guards, not before. An entry those guards reject is not served — the
		// handler runs — so counting it on the way in overstates the hit rate by exactly the number of key
		// collisions, which is the population the guards exist to catch.
		var (result, servedFromCache) = await HandleCachedResultAsync(
				cachedResult, message, context, nextDelegate, logger, cancellationToken)
			.ConfigureAwait(false);

		RecordHitOrMiss(servedFromCache);

		return result;
	}

	/// <summary>
	/// Evicts a non-cacheable marker entry, failing open so a removal error never surfaces to the caller.
	/// </summary>
	/// <param name="key">The cache key to remove.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	private async Task RemovePoisonMarkerAsync(string key, CancellationToken cancellationToken)
	{
		try
		{
			await cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// Fail-open: a cross-cutting cache must never break the core operation. Failing to evict the
			// non-cacheable marker only means a future request may see a stale marker — log, do not throw.
			logger.LogWarning(ex, "Failed to evict non-cacheable cache marker for key {CacheKey}", key);
		}
	}

	/// <summary>
	/// Registers a tag→key mapping for a cacheable tagged entry, failing open so a tag-store backend error
	/// (e.g. the tag store is down) never surfaces to the caller.
	/// </summary>
	/// <param name="tracker">The tag tracker (already confirmed non-null at the call site).</param>
	/// <param name="key">The cache key to register.</param>
	/// <param name="tags">The cache tags to associate with the key.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	private async Task RegisterTagKeysAsync(ICacheTagTracker tracker, string key, string[] tags, CancellationToken cancellationToken)
	{
		try
		{
			await tracker.RegisterKeyAsync(key, tags, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// Fail-open: a cross-cutting cache must never break the core operation. Failing to register the
			// tag→key mapping only means a later tag-based invalidation may miss this key — log, do not throw.
			logger.LogWarning(ex, "Failed to register tag-to-key mapping for cache key {CacheKey}", key);
		}
	}

	/// <summary>
	/// Records a cache hit or miss metric based on whether the result was served from cache.
	/// </summary>
	/// <param name="servedFromCache">
	/// <see langword="true" /> when a stored entry was actually returned to the caller; <see langword="false" />
	/// when the handler ran, including when a read-side guard declined the stored entry.
	/// </param>
	private void RecordHitOrMiss(bool servedFromCache)
	{
		if (servedFromCache)
		{
			_cacheHitCounter.Add(1);
		}
		else
		{
			_cacheMissCounter.Add(1);
		}
	}

	/// <summary>
	/// Applies random jitter to a cache TTL to prevent thundering-herd scenarios.
	/// The jitter range is controlled by <see cref="CacheBehaviorOptions.JitterRatio"/>.
	/// </summary>
	/// <param name="ttl">The base TTL to apply jitter to.</param>
	/// <returns>The TTL with jitter applied. Never returns a negative value.</returns>
	[SuppressMessage("Security", "CA5394:Do not use insecure randomness",
		Justification = "TTL jitter does not require cryptographic randomness")]
	private TimeSpan ApplyJitter(TimeSpan ttl)
	{
		var ratio = _options.Behavior.JitterRatio;
		if (ratio <= 0 || ttl <= TimeSpan.Zero)
		{
			return ttl;
		}

		// Generate a random factor in the range [1-ratio, 1+ratio]
		var factor = 1.0 + ((Random.Shared.NextDouble() * 2.0 * ratio) - ratio);
		var jitteredMs = ttl.TotalMilliseconds * factor;
		return TimeSpan.FromMilliseconds(Math.Max(jitteredMs, 1.0));
	}

	/// <summary>
	/// Helper class to hold reflection information for ICacheable interface methods.
	/// </summary>
	private sealed class CacheableInfo
	{
		/// <summary>
		/// Gets or sets the ICacheable interface type.
		/// </summary>
		public Type Interface { get; set; } = null!;

		/// <summary>
		/// Gets or sets the ShouldCache method info.
		/// </summary>
		public MethodInfo ShouldCacheMethod { get; set; } = null!;

		/// <summary>
		/// Gets or sets the ExpirationSeconds property info.
		/// </summary>
		public PropertyInfo ExpirationProperty { get; set; } = null!;

		/// <summary>
		/// Gets or sets the GetCacheTags method info.
		/// </summary>
		public MethodInfo GetCacheTagsMethod { get; set; } = null!;

		/// <summary>
		/// Determines if the result should be cached by invoking the ShouldCache method.
		/// </summary>
		/// <param name="message">The message to check.</param>
		/// <param name="returnValue">The return value to check.</param>
		/// <returns>True if the result should be cached; otherwise, false.</returns>
		public bool ShouldCache(object message, object? returnValue) => ShouldCacheMethod?.Invoke(message, [returnValue]) as bool? ?? true;

		/// <summary>
		/// Gets the expiration seconds by reading the ExpirationSeconds property.
		/// </summary>
		/// <param name="message">The message to get expiration for.</param>
		/// <returns>The expiration seconds.</returns>
		public int GetExpirationSeconds(object message) => ExpirationProperty?.GetValue(message) as int? ?? 60;

		/// <summary>
		/// Gets the cache tags by invoking the GetCacheTags method.
		/// </summary>
		/// <param name="message">The message to get tags for.</param>
		/// <returns>The cache tags.</returns>
		public string[] GetTags(object message) => GetCacheTagsMethod?.Invoke(message, parameters: null) as string[] ?? [];
	}

}
