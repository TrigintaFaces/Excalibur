// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Default handler invoker that uses runtime expression compilation for high-performance method invocation.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses expression trees and runtime compilation which is not AOT-compatible.
/// For AOT scenarios, use <see cref="HandlerInvokerAot"/> instead.
/// </para>
/// <para>
/// PERF-13/PERF-14: Uses three-phase lazy freeze pattern for optimal lookup performance:
/// <list type="number">
/// <item>Warmup phase: ConcurrentDictionary for thread-safe population during startup</item>
/// <item>Freeze transition: ToFrozenDictionary() when cache stabilizes</item>
/// <item>Frozen phase: FrozenDictionary for zero-sync O(1) lookups</item>
/// </list>
/// Call <see cref="FreezeCache"/> after handler registration is complete (e.g., via UseOptimizedDispatch).
/// </para>
/// </remarks>
[RequiresUnreferencedCode("Uses reflection to find and invoke handler methods dynamically")]
[RequiresDynamicCode("Uses expression compilation which requires runtime code generation")]
public sealed class HandlerInvoker : IHandlerInvoker, IValueTaskHandlerInvoker
{
	/// <summary>
	/// Delegate type for the invoker function.
	/// </summary>
	private delegate ValueTask<object?> InvokerFunc(object handler, IDispatchMessage message, CancellationToken cancellationToken);

	/// <summary>
	/// Warmup cache for thread-safe population during startup (PERF-13/PERF-14).
	/// Null after freeze is called.
	/// </summary>
	/// <remarks>
	/// Declared <see langword="volatile" /> so the hot path can read it into a local exactly once and
	/// observe a concurrent <see cref="FreezeCache" /> nulling it out; this prevents a torn read /
	/// <see cref="NullReferenceException" /> in the window between the <see cref="_isFrozen" /> check and
	/// the warmup-dictionary dereference.
	/// </remarks>
	private static volatile ConcurrentDictionary<(Type HandlerType, Type MessageType), InvokerFunc>? _warmupCache = new();

	/// <summary>
	/// Frozen cache for optimal read performance after warmup (PERF-13/PERF-14).
	/// Null until freeze is called.
	/// </summary>
	private static FrozenDictionary<(Type HandlerType, Type MessageType), InvokerFunc>? _frozenCache;

	/// <summary>
	/// Flag indicating if the cache has been frozen.
	/// </summary>
	private static volatile bool _isFrozen;

	private static readonly ConcurrentDictionary<(Type HandlerType, Type MessageType), InvokerFunc> _knownInvokerCache = new();
	private static readonly ValueTask<object?> NullResultValueTask = new(result: null);

	// PERF: ThreadStatic one-element cache for the most recent invoker lookup.
	// Eliminates ConcurrentDictionary lookup overhead (~30ns) when the same handler/message
	// type pair is dispatched repeatedly on the same thread (common hot-path pattern).
	[ThreadStatic] private static Type? s_cachedHandlerType;
	[ThreadStatic] private static Type? s_cachedMessageType;
	[ThreadStatic] private static InvokerFunc? s_cachedInvoker;

	/// <summary>
	/// Invokes a handler using runtime-compiled delegates.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public Task<object?> InvokeAsync(object handler, IDispatchMessage message, CancellationToken cancellationToken)
	{
		return InvokeValueTaskAsync(handler, message, cancellationToken).AsTask();
	}

	/// <summary>
	/// Invokes a handler using runtime-compiled delegates and returns a ValueTask.
	/// </summary>
	/// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
	public ValueTask<object?> InvokeValueTaskAsync(object handler, IDispatchMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(handler);
		ArgumentNullException.ThrowIfNull(message);

		var handlerType = handler.GetType();
		var messageType = message.GetType();

		// PERF: ThreadStatic one-element cache — O(1) with no synchronization.
		if (ReferenceEquals(s_cachedHandlerType, handlerType) &&
			ReferenceEquals(s_cachedMessageType, messageType) &&
			s_cachedInvoker is { } fastInvoker)
		{
			return fastInvoker(handler, message, cancellationToken);
		}

		var cacheKey = (handlerType, messageType);

		// Startup-known handlers bypass runtime fallback branching entirely.
		if (_knownInvokerCache.TryGetValue(cacheKey, out var knownInvoker))
		{
			s_cachedHandlerType = handlerType;
			s_cachedMessageType = messageType;
			s_cachedInvoker = knownInvoker;
			return knownInvoker(handler, message, cancellationToken);
		}

		// Source-generated invokers via HandlerInvokerRegistry are the default fast path when available.
		if (TryUseRegisteredInvoker(handlerType, handler, message, cancellationToken, out var registeredResult))
		{
			return registeredResult;
		}

		// Fall back to runtime compilation for handlers not known at compile time.

		// PERF-13/PERF-14: Three-phase lazy freeze pattern
		// Phase 3 (frozen): Fast path with zero synchronization overhead
		if (_isFrozen)
		{
			if (_frozenCache!.TryGetValue(cacheKey, out var frozenInvoker))
			{
				return frozenInvoker(handler, message, cancellationToken);
			}

			// Cache miss after freeze - build and invoke but don't cache (rare case)
			var lateInvoker = BuildInvoker(cacheKey.handlerType, cacheKey.messageType);
			return lateInvoker(handler, message, cancellationToken);
		}

		// Phase 1 (warmup): Thread-safe population using ConcurrentDictionary.
		// Read the volatile field exactly once into a local. FreezeCache can null _warmupCache out
		// concurrently after the _isFrozen check above observed false; binding it to a local means we
		// either use a live dictionary or detect the race deterministically — never dereference null.
		var warmup = _warmupCache;
		if (warmup is not null)
		{
			var invoker = warmup.GetOrAdd(cacheKey, static key => BuildInvoker(key.HandlerType, key.MessageType));
			return invoker(handler, message, cancellationToken);
		}

		// Lost the race with FreezeCache between the _isFrozen check and here: the warmup dictionary was
		// frozen and cleared. _frozenCache is guaranteed published (FreezeCache writes it before nulling
		// the volatile _warmupCache), so consult it before falling back to an uncached build.
		if (_frozenCache is { } frozen && frozen.TryGetValue(cacheKey, out var racedInvoker))
		{
			return racedInvoker(handler, message, cancellationToken);
		}

		// Frozen but this pair was never warmed: build and invoke without caching (rare cold path).
		var fallbackInvoker = BuildInvoker(cacheKey.handlerType, cacheKey.messageType);
		return fallbackInvoker(handler, message, cancellationToken);
	}

	/// <summary>
	/// Attempts to use a source-generated handler invoker from <see cref="HandlerInvokerRegistry"/> if available.
	/// </summary>
	/// <remarks>
	/// Uses <see cref="HandlerInvokerRegistry.TryGetRegisteredInvoker"/> which only returns invokers
	/// explicitly registered via <c>RegisterInvoker&lt;T&gt;</c> (source-generated module initializers).
	/// Does NOT use the registry's reflection fallback, since <see cref="BuildInvoker"/> handles
	/// all return types (<c>Task</c>, <c>Task&lt;T&gt;</c>, <c>ValueTask</c>, <c>ValueTask&lt;T&gt;</c>)
	/// and properly propagates exceptions without <see cref="System.Reflection.TargetInvocationException"/> wrapping.
	/// </remarks>
	private static bool TryUseRegisteredInvoker(
		Type handlerType,
		object handler,
		IDispatchMessage message,
		CancellationToken cancellationToken,
		out ValueTask<object?> result)
	{
		if (!HandlerInvokerRegistry.TryGetRegisteredInvoker(handlerType, out var registryInvoker))
		{
			result = default;
			return false;
		}

		result = new ValueTask<object?>(registryInvoker(handler, message, cancellationToken));
		return true;
	}

	/// <summary>
	/// Builds a runtime-compiled invoker for the specified handler type.
	/// </summary>
	/// <exception cref="InvalidOperationException"></exception>
	[RequiresUnreferencedCode("Uses reflection and expression compilation which is not AOT compatible")]
	[RequiresDynamicCode("Compiles expressions at runtime")]
	private static InvokerFunc BuildInvoker(Type handlerType, Type messageType)
	{
		_ = messageType; // Parameter not used - message type is discovered dynamically via reflection
		var method = handlerType.GetMethod("HandleAsync", BindingFlags.Instance | BindingFlags.Public)
					 ?? throw new InvalidOperationException($"No HandleAsync method found on {handlerType}");

		var handlerParam = Expression.Parameter(typeof(object), "handler");
		var messageParam = Expression.Parameter(typeof(IDispatchMessage), "message");
		var cancellationTokenParameter = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

		var castHandler = Expression.Convert(handlerParam, handlerType);
		var castMessage = Expression.Convert(messageParam, method.GetParameters()[0].ParameterType);

		var call = Expression.Call(castHandler, method, castMessage, cancellationTokenParameter);

		// Check the return type and handle appropriately
		if (method.ReturnType == typeof(Task))
		{
			// For Task (no result), convert to ValueTask<object?>.
			var convertMethod = typeof(HandlerInvoker)
				.GetMethod(nameof(ConvertTaskToNullObjectValueTask), BindingFlags.NonPublic | BindingFlags.Static)!;

			var convertCall = Expression.Call(convertMethod, call);

			return Expression
				.Lambda<InvokerFunc>(convertCall, handlerParam, messageParam, cancellationTokenParameter)
				.Compile();
		}

		if (method.ReturnType == typeof(ValueTask))
		{
			// For ValueTask (no result), convert to ValueTask<object?> returning null.
			var convertMethod = typeof(HandlerInvoker)
				.GetMethod(nameof(ConvertValueTaskToObjectValueTask), BindingFlags.NonPublic | BindingFlags.Static)!;

			var convertCall = Expression.Call(convertMethod, call);

			return Expression
				.Lambda<InvokerFunc>(convertCall, handlerParam, messageParam, cancellationTokenParameter)
				.Compile();
		}

		if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
		{
			// For ValueTask<T>, convert to ValueTask<object?>.
			var resultType = method.ReturnType.GetGenericArguments()[0];
			var convertMethod = typeof(HandlerInvoker)
				.GetMethod(nameof(ConvertValueTaskTToObjectValueTask), BindingFlags.NonPublic | BindingFlags.Static)!
				.MakeGenericMethod(resultType);

			var convertCall = Expression.Call(convertMethod, call);

			return Expression
				.Lambda<InvokerFunc>(convertCall, handlerParam, messageParam, cancellationTokenParameter)
				.Compile();
		}

		if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
		{
			// For Task<T>, we need to await the result and box it properly.
			var resultType = method.ReturnType.GetGenericArguments()[0];
			var convertMethod = typeof(HandlerInvoker)
				.GetMethod(nameof(ConvertTaskToObjectValueTask), BindingFlags.NonPublic | BindingFlags.Static)!
				.MakeGenericMethod(resultType);

			var convertCall = Expression.Call(convertMethod, call);

			return Expression
				.Lambda<InvokerFunc>(convertCall, handlerParam, messageParam, cancellationTokenParameter)
				.Compile();
		}

		throw new InvalidOperationException(
			$"Unsupported return type {method.ReturnType} on handler {handlerType}. " +
			"Handler methods must return Task, Task<T>, ValueTask, or ValueTask<T>.");
	}

	/// <summary>
	/// Helper method to convert Task&lt;T&gt; to ValueTask&lt;object?&gt; by awaiting and boxing the result.
	/// </summary>
	/// <remarks>
	/// For reference types, no boxing occurs since they're already objects.
	/// For value types (bool, int, structs), boxing is unavoidable when returning as object.
	/// We optimize by caching common boxed values like true/false.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ValueTask<object?> ConvertTaskToObjectValueTask<T>(Task<T> task)
	{
		// PERF: Fast path for synchronously completed tasks avoids async state machine allocation
		// (~96-144B) and Task<object?> wrapper (~72B). This is the common case when handlers
		// return Task.FromResult(value).
		if (task.IsCompletedSuccessfully)
		{
#pragma warning disable CA1849, RS0030 // Safe: IsCompletedSuccessfully guarantees no blocking
			var result = task.Result;
#pragma warning restore CA1849, RS0030
			if (typeof(T) == typeof(bool))
			{
				return new ValueTask<object?>(result is true ? CachedTrue : CachedFalse);
			}

			return new ValueTask<object?>(result);
		}

		return new ValueTask<object?>(AwaitTaskToObjectAsync(task));
	}

	private static async Task<object?> AwaitTaskToObjectAsync<T>(Task<T> task)
	{
		var result = await task.ConfigureAwait(false);

		if (typeof(T) == typeof(bool))
		{
			return result is true ? CachedTrue : CachedFalse;
		}

		return result;
	}

	/// <summary>
	/// Helper method to convert Task to ValueTask&lt;object?&gt; by awaiting and returning null.
	/// Properly propagates exceptions and cancellation unlike ContinueWith.
	/// </summary>
	private static ValueTask<object?> ConvertTaskToNullObjectValueTask(Task task)
	{
		if (task.IsCompletedSuccessfully)
		{
			return NullResultValueTask;
		}

		return new ValueTask<object?>(AwaitTaskToNullObjectAsync(task));
	}

	private static async Task<object?> AwaitTaskToNullObjectAsync(Task task)
	{
		await task.ConfigureAwait(false);
		return null;
	}

	/// <summary>
	/// Helper method to convert ValueTask to ValueTask&lt;object?&gt; by awaiting and returning null.
	/// </summary>
	private static ValueTask<object?> ConvertValueTaskToObjectValueTask(ValueTask valueTask)
	{
		if (valueTask.IsCompletedSuccessfully)
		{
			return NullResultValueTask;
		}

		return new ValueTask<object?>(AwaitValueTaskToObjectAsync(valueTask));
	}

	private static async Task<object?> AwaitValueTaskToObjectAsync(ValueTask valueTask)
	{
		await valueTask.ConfigureAwait(false);
		return null;
	}

	/// <summary>
	/// Helper method to convert ValueTask&lt;T&gt; to ValueTask&lt;object?&gt; by awaiting and boxing the result.
	/// </summary>
	private static ValueTask<object?> ConvertValueTaskTToObjectValueTask<T>(ValueTask<T> valueTask)
	{
		if (valueTask.IsCompletedSuccessfully)
		{
			var result = valueTask.Result;
			if (typeof(T) == typeof(bool))
			{
				return new ValueTask<object?>(result is true ? CachedTrue : CachedFalse);
			}

			return new ValueTask<object?>(result);
		}

		return new ValueTask<object?>(AwaitValueTaskTToObjectAsync(valueTask));
	}

	private static async Task<object?> AwaitValueTaskTToObjectAsync<T>(ValueTask<T> valueTask)
	{
		var result = await valueTask.ConfigureAwait(false);

		// Optimize for common value types to avoid repeated boxing
		if (typeof(T) == typeof(bool))
		{
			return result is true ? CachedTrue : CachedFalse;
		}

		return result;
	}

	// ==========================================
	// CACHED BOXED VALUES
	// Avoid repeated boxing of common value types
	// ==========================================

	/// <summary>
	/// Cached boxed true value to avoid repeated allocations.
	/// </summary>
	private static readonly object CachedTrue = true;

	/// <summary>
	/// Cached boxed false value to avoid repeated allocations.
	/// </summary>
	private static readonly object CachedFalse = false;

	/// <summary>
	/// Gets a value indicating whether the cache has been frozen.
	/// </summary>
	/// <remarks>
	/// When frozen, lookups use <see cref="FrozenDictionary{TKey, TValue}"/> for optimal performance.
	/// </remarks>
	public static bool IsCacheFrozen => _isFrozen;

	/// <summary>
	/// Pre-warms generated invoker resolution for known handler/message pairs.
	/// </summary>
	/// <remarks>
	/// This makes source-generated invokers the hot default path from first dispatch
	/// for handlers discovered at startup.
	/// </remarks>
	internal static void PreWarmGeneratedInvokerCache(IEnumerable<HandlerRegistryEntry> entries)
	{
		ArgumentNullException.ThrowIfNull(entries);

		foreach (var entry in entries)
		{
			var cacheKey = (entry.HandlerType, entry.MessageType);
			_ = _knownInvokerCache.GetOrAdd(
				cacheKey,
				static key =>
				{
					// Try source-generated invoker first (explicitly registered, AOT-safe)
					if (HandlerInvokerRegistry.TryGetRegisteredInvoker(key.HandlerType, out var registryInvoker))
					{
						var captured = registryInvoker;
						return (handler, message, ct) =>
							new ValueTask<object?>(captured(handler, message, ct));
					}

					// Fall back to expression-compiled invoker (handles ValueTask, proper exception propagation)
					return BuildInvoker(key.HandlerType, key.MessageType);
				});
		}
	}

	/// <summary>
	/// Freezes the invoker cache for optimal read performance (PERF-13/PERF-14).
	/// </summary>
	/// <remarks>
	/// <para>
	/// Call this method after all handlers have been registered (e.g., after DI container build).
	/// Once frozen, the cache uses <see cref="FrozenDictionary{TKey, TValue}"/> for O(1) lookups
	/// with zero synchronization overhead.
	/// </para>
	/// <para>
	/// This method is idempotent - calling it multiple times has no effect after the first call.
	/// </para>
	/// </remarks>
	public static void FreezeCache()
	{
		if (_isFrozen)
		{
			return;
		}

		var warmup = _warmupCache;
		if (warmup is null)
		{
			return;
		}

		// Phase 2 (freeze transition): Convert to FrozenDictionary
		_frozenCache = warmup.ToFrozenDictionary();
		_isFrozen = true;
		_warmupCache = null; // Allow GC to collect warmup dictionary
	}

	/// <summary>
	/// Clears the internal invoker cache. Primarily intended for testing scenarios.
	/// </summary>
	/// <remarks>
	/// This method resets the cache to its initial state (unfrozen, empty warmup dictionary).
	/// </remarks>
	internal static void ClearCache()
	{
		_isFrozen = false;
		_frozenCache = null;
		_warmupCache = new();
		_knownInvokerCache.Clear();
		s_cachedHandlerType = null;
		s_cachedMessageType = null;
		s_cachedInvoker = null;
	}
}
