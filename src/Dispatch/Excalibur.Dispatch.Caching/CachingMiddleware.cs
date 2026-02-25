// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Caching.Diagnostics;

using Microsoft.Extensions.Caching.Hybrid;
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
/// <remarks>
/// <para>
/// <b>AOT Compatibility:</b> This middleware uses JSON serialization and dynamic type inspection
/// for cache key generation and entry deserialization, which may not be fully compatible with
/// Native AOT publishing in strict trim mode.
/// </para>
/// <para>
/// For AOT scenarios, consider:
/// <list type="bullet">
/// <item>Disabling caching middleware if not required</item>
/// <item>Suppressing trim warnings after testing with your specific message types</item>
/// <item>Using <c>TrimMode=partial</c> instead of <c>TrimMode=full</c></item>
/// <item>Waiting for v1.5 source-generated alternatives (Q2-Q3 2026)</item>
/// </list>
/// </para>
/// </remarks>
public sealed class CachingMiddleware(
	IMeterFactory meterFactory,
	HybridCache cache,
	ICacheKeyBuilder keyBuilder,
	IServiceProvider services,
	IOptions<CacheOptions> options,
	ILogger<CachingMiddleware> logger,
	IResultCachePolicy? globalPolicy = null) : IDispatchMiddleware
{
	private static readonly ConcurrentDictionary<Type, Type?> _cacheableInterfaceCache = new();
	private static readonly ConcurrentDictionary<Type, Type?> _actionInterfaceCache = new();

	private static readonly CompositeFormat MessageTypeNotDispatchActionFormat =
		CompositeFormat.Parse(Resources.CachingMiddleware_MessageTypeNotDispatchActionFormat);

	private readonly Counter<long> _cacheHitCounter = meterFactory.Create(DispatchCachingTelemetryConstants.MeterName).CreateCounter<long>("dispatch.cache.hits", description: "Number of cache hits");
	private readonly Counter<long> _cacheMissCounter = meterFactory.Create(DispatchCachingTelemetryConstants.MeterName).CreateCounter<long>("dispatch.cache.misses", description: "Number of cache misses");
	private readonly Counter<long> _cacheTimeoutCounter = meterFactory.Create(DispatchCachingTelemetryConstants.MeterName).CreateCounter<long>("dispatch.cache.timeouts", description: "Number of cache operation timeouts");
	private readonly Histogram<double> _cacheLatencyHistogram = meterFactory.Create(DispatchCachingTelemetryConstants.MeterName).CreateHistogram<double>("dispatch.cache.duration", unit: "ms", description: "Cache operation latency in milliseconds");

	[ThreadStatic]
	private static Random? t_jitterRandom;

#pragma warning disable CA5394 // Random is not cryptographic — jitter does not need crypto-strength randomness
	private static Random JitterRandom => t_jitterRandom ??= new Random();
#pragma warning restore CA5394

	private readonly CacheOptions _options = options.Value;
	private readonly IResultCachePolicy? _globalPolicy = globalPolicy ?? options.Value.GlobalPolicy;
	private bool _startupWarningEmitted;

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Cache;

	/// <inheritdoc />
	[RequiresUnreferencedCode("CachingMiddleware uses MakeGenericType and Type.GetInterfaces for dynamic cache policy resolution. Use attribute-based caching for AOT scenarios.")]
	[RequiresDynamicCode("CachingMiddleware uses MakeGenericType for CachedMessageResult<T> and IResultCachePolicy<T> resolution.")]
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

		// Check if message implements any ICacheable<T> interface
		var messageType = message.GetType();
		var cacheableInterface = _cacheableInterfaceCache.GetOrAdd(messageType, static type =>
			type.GetInterfaces()
				.FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICacheable<>)));
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
	/// Extracts the return value from a message result.
	/// </summary>
	/// <param name="processedResult">The message result to extract from.</param>
	/// <returns>The extracted return value, or null if not found.</returns>
	[UnconditionalSuppressMessage("Trimming", "IL2075:Type.GetProperty may break with trimming",
		Justification = "ReturnValue property is a well-known property in MessageResult pattern")]
	private static object? ExtractReturnValue(IMessageResult? processedResult)
	{
		if (processedResult?.GetType().IsGenericType == true)
		{
			var returnValueProperty = processedResult.GetType().GetProperty("ReturnValue");
			if (returnValueProperty != null)
			{
				return returnValueProperty.GetValue(processedResult);
			}
		}

		return null;
	}

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
		var messageType = message.GetType();
		var cacheableInterface = _cacheableInterfaceCache.GetOrAdd(messageType, static type =>
			type.GetInterfaces()
				.FirstOrDefault(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICacheable<>)));

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
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Deserialize(String, Type, JsonSerializerOptions)")]
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

		return cachedValue!;
	}

	/// <summary>
	/// Handles a cached result by deserializing and returning it or executing the handler.
	/// </summary>
	/// <param name="cachedResult">The cached result, or null if not cached.</param>
	/// <param name="message">The message to process.</param>
	/// <param name="context">The message context.</param>
	/// <param name="nextDelegate">The next middleware delegate.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The message result.</returns>
	[RequiresDynamicCode("Calls DeserializeCachedValue which uses dynamic code for deserialization")]
	private static async Task<IMessageResult> HandleCachedResultAsync(
		CachedValue? cachedResult,
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
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

				return (IMessageResult)originalResult;
			}

			if (cachedResult is { ShouldCache: true, Value: not null })
			{
				// Cache hit - deserialize value and return directly without calling handler
				var cachedValue = DeserializeCachedValue(cachedResult);

				// Determine the return type from the message type
				var messageType = message.GetType();
				var actionInterface = _actionInterfaceCache.GetOrAdd(messageType, static type =>
					type.GetInterfaces()
						.FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDispatchAction<>)));

				if (actionInterface is not null)
				{
					// Get the TResponse type from IDispatchAction<TResponse>
					var returnType = actionInterface.GetGenericArguments()[0];

					// Create CachedMessageResult<T> using reflection
					var resultInstance = CreateCachedMessageResult(returnType, cachedValue);
					context.Result = cachedValue;

					return resultInstance;
				}

				var implementedInterfaces = string.Join(
					", ",
					messageType.GetInterfaces().Select(static i => i.Name));
				throw new InvalidOperationException(
					string.Format(
						CultureInfo.CurrentCulture,
						MessageTypeNotDispatchActionFormat,
						messageType.FullName,
						implementedInterfaces));
			}
		}

		// Fallback
		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
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

		// Use cache with timeout
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		cts.CancelAfter(options.Value.Behavior.CacheTimeout);

		var sw = ValueStopwatch.StartNew();
		try
		{
			var cachedResult = await cache.GetOrCreateAsync(
				key,
				async ct => await CreateCacheValueAsync(message, context, nextDelegate, ct).ConfigureAwait(false),
				new HybridCacheEntryOptions
				{
					Expiration = expiration,
					Flags = _options.CacheMode == CacheMode.Distributed
						? HybridCacheEntryFlags.DisableLocalCache
						: HybridCacheEntryFlags.None,
				},
				tags,
				cts.Token).ConfigureAwait(false);

			_cacheLatencyHistogram.Record(sw.Elapsed.TotalMilliseconds);
			RecordHitOrMiss(cachedResult, context);

			return await HandleCachedResultAsync(cachedResult, message, context, nextDelegate, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Timeout or cancellation - execute without caching
			_cacheTimeoutCounter.Add(1);
			_cacheLatencyHistogram.Record(sw.Elapsed.TotalMilliseconds);
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
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
		Justification = "Policy evaluation is acceptable when caching is enabled")]
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
		Justification = "ShouldCache is only called when caching is enabled and AOT limitations are acceptable")]
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
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The cached value.</returns>
	[UnconditionalSuppressMessage("AOT", "IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "ShouldCache is only called when caching is enabled and AOT limitations are acceptable")]
	private async Task<CachedValue> CreateCacheValueAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		// Cache miss - execute the delegate
		var messageResult = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

		// Get the return value if it's a generic result
		var returnValue = ExtractReturnValue(messageResult);
		var shouldCache = ShouldCache(message, returnValue);

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

		using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		cts.CancelAfter(options.Value.Behavior.CacheTimeout);

		var sw = ValueStopwatch.StartNew();
		try
		{
			var cachedResult = await cache.GetOrCreateAsync(
				key,
				async ct => await CreateAttributeCacheValueAsync(message, context, nextDelegate, attr, ct).ConfigureAwait(false),
				new HybridCacheEntryOptions
				{
					Expiration = expiration,
					Flags = _options.CacheMode == CacheMode.Distributed
						? HybridCacheEntryFlags.DisableLocalCache
						: HybridCacheEntryFlags.None,
				},
				tags,
				cts.Token).ConfigureAwait(false);

			_cacheLatencyHistogram.Record(sw.Elapsed.TotalMilliseconds);
			RecordHitOrMiss(cachedResult, context);

			return await HandleCachedResultAsync(cachedResult, message, context, nextDelegate, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			_cacheTimeoutCounter.Add(1);
			_cacheLatencyHistogram.Record(sw.Elapsed.TotalMilliseconds);
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Determines if a result should be cached based on policies and attributes.
	/// </summary>
	/// <param name="message"> The message being processed. </param>
	/// <param name="returnValue"> The return value to potentially cache. </param>
	/// <param name="attr"> The cache result attribute. </param>
	/// <param name="context"> The message context. </param>
	/// <returns> True if the result should be cached; otherwise, false. </returns>
	[UnconditionalSuppressMessage("AOT", "IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "ShouldCache is only called when caching is enabled and AOT limitations are acceptable")]
	private bool ShouldCacheBasedOnPolicy(
		IDispatchMessage message,
		object? returnValue,
		CacheResultAttribute? attr,
		IMessageContext context) =>
		ShouldCache(message, returnValue)
		&& ((!attr?.OnlyIfSuccess ?? true)
			|| (((context.ValidationResult() as dynamic)?.IsValid ?? true)
				&& ((context.AuthorizationResult() as dynamic)?.IsAuthorized ?? true)))
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
	[RequiresDynamicCode("Calls System.Type.MakeGenericType(params Type[])")]
	private object? GetMessagePolicy(IDispatchMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		var messageType = message.GetType();
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
	[RequiresDynamicCode("Calls GetMessagePolicy which uses MakeGenericType")]
	[UnconditionalSuppressMessage("Trimming", "IL2075:Type.GetMethod may break with trimming",
		Justification = "ShouldCache method lookup is handled with null checks and try-catch for AOT compatibility")]
	private bool ShouldCache(IDispatchMessage message, object? result)
	{
		ArgumentNullException.ThrowIfNull(message);

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
					_ = ex;
				}
			}
		}

		return _globalPolicy?.ShouldCache(message, result) ?? true;
	}

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

	private static IMessageResult CreateCachedMessageResult(Type returnType, object cachedValue)
	{
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
	/// Records a cache hit or miss metric based on whether the result was served from cache.
	/// </summary>
	/// <param name="cachedResult">The cached value returned from HybridCache.</param>
	/// <param name="context">The message context (used to detect fresh execution via OriginalResult key).</param>
	private void RecordHitOrMiss(CachedValue? cachedResult, IMessageContext context)
	{
		if (cachedResult?.HasExecuted == true
			&& cachedResult is { ShouldCache: true, Value: not null }
			&& !context.Items.ContainsKey("Dispatch:OriginalResult"))
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
		var factor = 1.0 + ((JitterRandom.NextDouble() * 2.0 * ratio) - ratio);
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
