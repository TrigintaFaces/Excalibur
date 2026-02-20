// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Manual handler invoker registry implementation for development/testing. This is a temporary workaround while the build environment issue
/// prevents the source generator from running. Once the generator works, this file should be integrated with the generated version.
/// </summary>
/// <remarks>
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
public static partial class HandlerInvokerRegistry
{
	/// <summary>
	/// Delegate type for invoker functions.
	/// </summary>
	private delegate Task<object?> InvokerFunc(object handler, IDispatchMessage message, CancellationToken cancellationToken);

	/// <summary>
	/// Warmup cache for thread-safe population during startup (PERF-13/PERF-14).
	/// Null after freeze is called.
	/// </summary>
	private static ConcurrentDictionary<Type, Func<object, IDispatchMessage, CancellationToken, Task<object?>>>? _warmupCache = new();

	/// <summary>
	/// Frozen cache for optimal read performance after warmup (PERF-13/PERF-14).
	/// Null until freeze is called.
	/// </summary>
	private static FrozenDictionary<Type, Func<object, IDispatchMessage, CancellationToken, Task<object?>>>? _frozenCache;

	/// <summary>
	/// Flag indicating if the cache has been frozen.
	/// </summary>
	private static volatile bool _isFrozen;

	/// <summary>
	/// Gets a value indicating whether the cache has been frozen.
	/// </summary>
	public static bool IsCacheFrozen => _isFrozen;

	/// <summary>
	/// Manually register a handler invoker.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown if the cache has been frozen.</exception>
	public static void RegisterInvoker<THandler, TMessage>(
		Func<THandler, TMessage, CancellationToken, Task> invoker)
		where THandler : class
		where TMessage : IDispatchMessage
	{
		if (_isFrozen)
		{
			throw new InvalidOperationException(
				$"Cannot register invoker for {typeof(THandler).Name} after cache has been frozen. " +
				"Register all handlers before calling FreezeCache().");
		}

		_warmupCache[typeof(THandler)] = async (handler, message, ct) =>
		{
			await invoker((THandler)handler, (TMessage)message, ct).ConfigureAwait(false);
			return null;
		};
	}

	/// <summary>
	/// Manually register a handler invoker with result.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown if the cache has been frozen.</exception>
	public static void RegisterInvoker<THandler, TMessage, TResult>(
		Func<THandler, TMessage, CancellationToken, Task<TResult>> invoker)
		where THandler : class
		where TMessage : IDispatchMessage
	{
		if (_isFrozen)
		{
			throw new InvalidOperationException(
				$"Cannot register invoker for {typeof(THandler).Name} after cache has been frozen. " +
				"Register all handlers before calling FreezeCache().");
		}

		_warmupCache[typeof(THandler)] = async (handler, message, ct) =>
		{
			var result = await invoker((THandler)handler, (TMessage)message, ct).ConfigureAwait(false);
			return result;
		};
	}

	/// <summary>
	/// Gets the invoker for the specified handler type.
	/// </summary>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode")]
	internal static Func<object, IDispatchMessage, CancellationToken, Task<object?>>? GetInvoker(Type handlerType)
	{
		// PERF-13/PERF-14: Three-phase lazy freeze pattern
		// Phase 3 (frozen): Fast path with zero synchronization overhead
		if (_isFrozen)
		{
			if (_frozenCache.TryGetValue(handlerType, out var frozenInvoker))
			{
				return frozenInvoker;
			}

			// Cache miss after freeze - build but don't cache (rare case)
			return CreateInvoker(handlerType);
		}

		// Phase 1 (warmup): Thread-safe population using ConcurrentDictionary
		return _warmupCache.GetOrAdd(handlerType, CreateInvoker);
	}

	[RequiresUnreferencedCode("Uses reflection to create handler invokers")]
	[RequiresDynamicCode("Uses reflection to invoke handler methods at runtime")]
	private static Func<object, IDispatchMessage, CancellationToken, Task<object?>> CreateInvoker(Type handlerType)
	{
		// Find the HandleAsync method
		var method = handlerType.GetMethod("HandleAsync", BindingFlags.Instance | BindingFlags.Public) ??
					 throw new InvalidOperationException($"No HandleAsync method found on {handlerType}");

		// Validate method signature upfront (once, not per-invocation)
		var parameters = method.GetParameters();
		if (parameters.Length != 2)
		{
			throw new InvalidOperationException($"HandleAsync on {handlerType} must have exactly 2 parameters");
		}

		// Determine if method returns Task or Task<T>
		var returnsVoidTask = method.ReturnType == typeof(Task);

		// Create a compiled invoker using async/await instead of ContinueWith
		return async (handler, message, ct) =>
		{
			var result = method.Invoke(handler, [message, ct]);

			if (result is Task task)
			{
				await task.ConfigureAwait(false);

				if (returnsVoidTask)
				{
					return null;
				}

				// Task<T> - get the Result property value after await completes
				var resultProperty = task.GetType().GetProperty("Result");
				return resultProperty?.GetValue(task);
			}

			throw new InvalidOperationException($"HandleAsync on {handlerType} must return Task or Task<T>");
		};
	}

	/// <summary>
	/// Freezes the invoker registry for optimal read performance (PERF-13/PERF-14).
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
		_warmupCache = new ConcurrentDictionary<Type, Func<object, IDispatchMessage, CancellationToken, Task<object?>>>();
	}
}
