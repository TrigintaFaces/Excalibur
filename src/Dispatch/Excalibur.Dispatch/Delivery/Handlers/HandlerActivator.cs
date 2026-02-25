// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Default handler activator that uses reflection to set context properties.
/// </summary>
/// <remarks>
/// This implementation uses reflection which may not be fully AOT-compatible. For AOT scenarios, the source generator will provide an
/// optimized implementation.
/// </remarks>
[RequiresUnreferencedCode("Uses reflection to find and set properties on handlers")]
[RequiresDynamicCode("Uses expression compilation which requires runtime code generation")]
public sealed class HandlerActivator : IHandlerActivator
{
	private static bool _precompiledAvailable;
	private static bool _precompiledChecked;
	private static readonly Type[] NoFactoryArgumentTypes = [];
	private static readonly object[] NoFactoryArguments = [];
	private static readonly Dictionary<Type, bool> _precompiledContextSupportCache = new();

	/// <summary>
	/// Cache of compiled context setters keyed by handler type.
	/// Uses FrozenDictionary after freeze for optimal read performance.
	/// </summary>
	private static readonly Dictionary<Type, Action<object, IMessageContext>?> _contextSetterCache = new();
	private static readonly Dictionary<Type, Func<IServiceProvider, IMessageContext, object>> _activationPlanCache = new();
	private static readonly Dictionary<Type, Func<IServiceProvider, IMessageContext, object>> _registeredActivationPlanCache = new();
	private static readonly Dictionary<Type, Func<IServiceProvider, IMessageContext, object>> _factoryActivationPlanCache = new();
	private static ConditionalWeakTable<IServiceProvider, ConcurrentDictionary<Type, ServiceResolutionMode>> _serviceResolutionModesByProvider = new();

	private static FrozenDictionary<Type, Action<object, IMessageContext>?>? _frozenSetterCache;
	private static FrozenDictionary<Type, Func<IServiceProvider, IMessageContext, object>>? _frozenActivationPlanCache;
	private static FrozenDictionary<Type, Func<IServiceProvider, IMessageContext, object>>? _frozenRegisteredActivationPlanCache;
	private static FrozenDictionary<Type, Func<IServiceProvider, IMessageContext, object>>? _frozenFactoryActivationPlanCache;
#if NET9_0_OR_GREATER
	private static readonly Lock _cacheLock = new();
#else
	private static readonly object _cacheLock = new();
#endif

	private enum ServiceResolutionMode
	{
		RegisteredRequiredService,
		RegisteredGetService,
		Factory,
	}

	/// <summary>
	/// Activates a handler and sets its context property if available.
	/// </summary>
	/// <remarks>
	/// This method uses cached compiled delegates instead of reflection.
	/// Property setters are compiled once at first access and cached for subsequent calls.
	/// In AOT scenarios, use the source-generated PrecompiledHandlerActivator instead.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public object ActivateHandler(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType,
		IMessageContext context,
		IServiceProvider provider)
	{
		ArgumentNullException.ThrowIfNull(handlerType);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(provider);

		// Activation plan captures service resolution path + context setting strategy once per handler type.
		var activationPlan = GetOrCreateActivationPlan(handlerType);
		return activationPlan(provider, context);
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	internal object ActivateRegisteredHandler(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType,
		IMessageContext context,
		IServiceProvider provider)
	{
		ArgumentNullException.ThrowIfNull(handlerType);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(provider);

		var activationPlan = GetOrCreateRegisteredActivationPlan(handlerType);
		return activationPlan(provider, context);
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	internal object ActivateFactoryHandler(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType,
		IMessageContext context,
		IServiceProvider provider)
	{
		ArgumentNullException.ThrowIfNull(handlerType);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(provider);

		var activationPlan = GetOrCreateFactoryActivationPlan(handlerType);
		return activationPlan(provider, context);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool RequiresContextInjection(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType)
	{
		ArgumentNullException.ThrowIfNull(handlerType);
		return BuildContextApplier(handlerType, GetOrCreateContextSetter(handlerType)) is not null;
	}

	/// <summary>
	/// Gets or creates an activation plan for the specified handler type.
	/// The activation plan resolves the handler and applies context via precompiled setter.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Func<IServiceProvider, IMessageContext, object> GetOrCreateActivationPlan(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType)
	{
		// Fast path: frozen cache lookup with zero synchronization overhead.
		if (_frozenActivationPlanCache != null)
		{
			return _frozenActivationPlanCache.TryGetValue(handlerType, out var cached)
				? cached
				: CreateAndCacheActivationPlan(handlerType);
		}

		// Warmup phase: populate mutable cache.
		lock (_cacheLock)
		{
			if (_activationPlanCache.TryGetValue(handlerType, out var cached))
			{
				return cached;
			}

			return CreateAndCacheActivationPlan(handlerType);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Func<IServiceProvider, IMessageContext, object> GetOrCreateRegisteredActivationPlan(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType)
	{
		if (_frozenRegisteredActivationPlanCache != null)
		{
			return _frozenRegisteredActivationPlanCache.TryGetValue(handlerType, out var cached)
				? cached
				: CreateAndCacheRegisteredActivationPlan(handlerType);
		}

		lock (_cacheLock)
		{
			if (_registeredActivationPlanCache.TryGetValue(handlerType, out var cached))
			{
				return cached;
			}

			return CreateAndCacheRegisteredActivationPlan(handlerType);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Func<IServiceProvider, IMessageContext, object> GetOrCreateFactoryActivationPlan(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType)
	{
		if (_frozenFactoryActivationPlanCache != null)
		{
			return _frozenFactoryActivationPlanCache.TryGetValue(handlerType, out var cached)
				? cached
				: CreateAndCacheFactoryActivationPlan(handlerType);
		}

		lock (_cacheLock)
		{
			if (_factoryActivationPlanCache.TryGetValue(handlerType, out var cached))
			{
				return cached;
			}

			return CreateAndCacheFactoryActivationPlan(handlerType);
		}
	}

	/// <summary>
	/// Creates and caches an activation plan for a handler type.
	/// </summary>
	private static Func<IServiceProvider, IMessageContext, object> CreateAndCacheActivationPlan(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType)
	{
		var contextApplier = BuildContextApplier(handlerType, GetOrCreateContextSetter(handlerType));
		var factory = ActivatorUtilities.CreateFactory(handlerType, NoFactoryArgumentTypes);

		Func<IServiceProvider, IMessageContext, object> activationPlan = contextApplier is null
			? (serviceProvider, _) =>
			{
				return ResolveHandlerInstance(handlerType, serviceProvider, factory);
			}
		: (serviceProvider, context) =>
		{
			var resolved = ResolveHandlerInstance(handlerType, serviceProvider, factory);
			contextApplier(resolved, context);
			return resolved;
		};

		lock (_cacheLock)
		{
			_activationPlanCache[handlerType] = activationPlan;
		}

		return activationPlan;
	}

	private static Func<IServiceProvider, IMessageContext, object> CreateAndCacheRegisteredActivationPlan(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType)
	{
		var contextApplier = BuildContextApplier(handlerType, GetOrCreateContextSetter(handlerType));

		Func<IServiceProvider, IMessageContext, object> activationPlan = contextApplier is null
			? (serviceProvider, _) => serviceProvider.GetRequiredService(handlerType)
			: (serviceProvider, context) =>
			{
				var resolved = serviceProvider.GetRequiredService(handlerType);
				contextApplier(resolved, context);
				return resolved;
			};

		lock (_cacheLock)
		{
			_registeredActivationPlanCache[handlerType] = activationPlan;
		}

		return activationPlan;
	}

	private static Func<IServiceProvider, IMessageContext, object> CreateAndCacheFactoryActivationPlan(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType)
	{
		var contextApplier = BuildContextApplier(handlerType, GetOrCreateContextSetter(handlerType));
		var factory = ActivatorUtilities.CreateFactory(handlerType, NoFactoryArgumentTypes);

		Func<IServiceProvider, IMessageContext, object> activationPlan = contextApplier is null
			? (serviceProvider, _) => factory(serviceProvider, NoFactoryArguments)
			: (serviceProvider, context) =>
			{
				var resolved = factory(serviceProvider, NoFactoryArguments);
				contextApplier(resolved, context);
				return resolved;
			};

		lock (_cacheLock)
		{
			_factoryActivationPlanCache[handlerType] = activationPlan;
		}

		return activationPlan;
	}

	/// <summary>
	/// Gets or creates a compiled context setter delegate for the specified handler type.
	/// Uses expression compilation to avoid per-call reflection overhead.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Action<object, IMessageContext>? GetOrCreateContextSetter(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType)
	{
		if (_frozenSetterCache != null)
		{
			return _frozenSetterCache.TryGetValue(handlerType, out var cached)
				? cached
				: CreateAndCacheSetter(handlerType);
		}

		lock (_cacheLock)
		{
			if (_contextSetterCache.TryGetValue(handlerType, out var cached))
			{
				return cached;
			}

			return CreateAndCacheSetter(handlerType);
		}
	}

	/// <summary>
	/// Creates a compiled setter delegate and caches it.
	/// </summary>
	private static Action<object, IMessageContext>? CreateAndCacheSetter(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType)
	{
		PropertyInfo? contextProperty = null;
		var properties = handlerType.GetProperties();
		foreach (var property in properties)
		{
			var setMethod = property.SetMethod;
			if (setMethod is null)
			{
				continue;
			}

			// Context injection only applies to writable instance properties.
			// Static IMessageContext properties (used by some tests/diagnostics handlers)
			// are not handler context targets and cannot be bound from an instance setter plan.
			if (!setMethod.IsStatic &&
				property.GetIndexParameters().Length == 0 &&
				property.PropertyType == typeof(IMessageContext))
			{
				contextProperty = property;
				break;
			}
		}

		var setter = contextProperty is null
			? null
			: CompilePropertySetter(handlerType, contextProperty);

		lock (_cacheLock)
		{
			_contextSetterCache[handlerType] = setter;
		}

		return setter;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Action<object, IMessageContext>? BuildContextApplier(
		Type handlerType,
		Action<object, IMessageContext>? fallbackSetter)
	{
		return SupportsPrecompiledContext(handlerType)
			? ApplyPrecompiledContext
			: fallbackSetter;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void ApplyPrecompiledContext(object handler, IMessageContext context)
	{
		PrecompiledHandlerActivator.SetContext(handler, context);
	}

	/// <summary>
	/// Compiles an expression tree to a delegate for setting the context property.
	/// This avoids reflection on every call.
	/// </summary>
	private static Action<object, IMessageContext> CompilePropertySetter(Type handlerType, PropertyInfo property)
	{
		// Build expression: (object handler, IMessageContext context) => ((HandlerType)handler).Property = context
		var handlerParam = Expression.Parameter(typeof(object), "handler");
		var contextParam = Expression.Parameter(typeof(IMessageContext), "context");

		var castHandler = Expression.Convert(handlerParam, handlerType);
		var propertyAccess = Expression.Property(castHandler, property);
		var assign = Expression.Assign(propertyAccess, contextParam);

		var lambda = Expression.Lambda<Action<object, IMessageContext>>(assign, handlerParam, contextParam);
		return lambda.Compile();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static object ResolveHandlerInstance(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type handlerType,
		IServiceProvider serviceProvider,
		ObjectFactory factory)
	{
		var modeCache = _serviceResolutionModesByProvider.GetValue(
			serviceProvider,
			static _ => new ConcurrentDictionary<Type, ServiceResolutionMode>());

		if (modeCache.TryGetValue(handlerType, out var cachedMode))
		{
			return ResolveWithMode(cachedMode, handlerType, serviceProvider, factory);
		}

		if (serviceProvider is IServiceProviderIsService providerIsService)
		{
			var resolvedMode = providerIsService.IsService(handlerType)
				? ServiceResolutionMode.RegisteredRequiredService
				: ServiceResolutionMode.Factory;
			_ = modeCache.TryAdd(handlerType, resolvedMode);
			return ResolveWithMode(resolvedMode, handlerType, serviceProvider, factory);
		}

		var resolved = serviceProvider.GetService(handlerType);
		if (resolved is not null)
		{
			_ = modeCache.TryAdd(handlerType, ServiceResolutionMode.RegisteredGetService);
			return resolved;
		}

		_ = modeCache.TryAdd(handlerType, ServiceResolutionMode.Factory);
		return factory(serviceProvider, NoFactoryArguments);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static object ResolveWithMode(
		ServiceResolutionMode mode,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type handlerType,
		IServiceProvider serviceProvider,
		ObjectFactory factory)
	{
		return mode switch
		{
			ServiceResolutionMode.RegisteredRequiredService => serviceProvider.GetRequiredService(handlerType),
			ServiceResolutionMode.RegisteredGetService => serviceProvider.GetService(handlerType)
				?? throw new InvalidOperationException($"Service of type {handlerType.FullName} is no longer available."),
			_ => factory(serviceProvider, NoFactoryArguments),
		};
	}

	/// <summary>
	/// Gets a value indicating whether activation caches have been frozen.
	/// </summary>
	internal static bool IsCacheFrozen =>
		_frozenSetterCache is not null &&
		_frozenActivationPlanCache is not null &&
		_frozenRegisteredActivationPlanCache is not null &&
		_frozenFactoryActivationPlanCache is not null;

	/// <summary>
	/// Freezes the context setter cache for optimal read performance.
	/// Should be called during application startup after all handler types are known.
	/// </summary>
	public static void FreezeCache()
	{
		lock (_cacheLock)
		{
			_frozenSetterCache = _contextSetterCache.ToFrozenDictionary();
			_frozenActivationPlanCache = _activationPlanCache.ToFrozenDictionary();
			_frozenRegisteredActivationPlanCache = _registeredActivationPlanCache.ToFrozenDictionary();
			_frozenFactoryActivationPlanCache = _factoryActivationPlanCache.ToFrozenDictionary();
		}
	}

	/// <summary>
	/// Pre-warms the cache with known handler types.
	/// Call this at startup with all registered handler types for optimal performance.
	/// </summary>
	public static void PreWarmCache(IEnumerable<Type> handlerTypes)
	{
		ArgumentNullException.ThrowIfNull(handlerTypes);

		foreach (var type in handlerTypes)
		{
			_ = GetOrCreateActivationPlan(type);
			_ = GetOrCreateRegisteredActivationPlan(type);
			_ = GetOrCreateFactoryActivationPlan(type);
			_ = GetOrCreateContextSetter(type);
		}
	}

	/// <summary>
	/// Pre-binds handler resolution modes for a specific service provider.
	/// This removes first-hit service probing on dispatch hot paths.
	/// </summary>
	internal static void PreBindResolutionModes(IServiceProvider serviceProvider, IEnumerable<Type> handlerTypes)
	{
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(handlerTypes);

		var modeCache = _serviceResolutionModesByProvider.GetValue(
			serviceProvider,
			static _ => new ConcurrentDictionary<Type, ServiceResolutionMode>());
		var providerIsService = serviceProvider as IServiceProviderIsService;

		foreach (var handlerType in handlerTypes)
		{
			if (modeCache.ContainsKey(handlerType))
			{
				continue;
			}

			ServiceResolutionMode mode;
			if (providerIsService is not null)
			{
				mode = providerIsService.IsService(handlerType)
					? ServiceResolutionMode.RegisteredRequiredService
					: ServiceResolutionMode.Factory;
			}
			else
			{
				try
				{
					mode = serviceProvider.GetService(handlerType) is not null
						? ServiceResolutionMode.RegisteredGetService
						: ServiceResolutionMode.Factory;
				}
				catch (InvalidOperationException)
				{
					// Avoid failing startup prebind when a registered handler has unresolved dependencies.
					// Actual activation will still fail at dispatch-time if dependencies remain missing.
					mode = ServiceResolutionMode.RegisteredRequiredService;
				}
			}

			_ = modeCache.TryAdd(handlerType, mode);
		}
	}

	/// <summary>
	/// Seeds the resolution mode for a handler on a specific provider without probing.
	/// Used by hot-path dispatch plans to avoid first-hit branching on scoped providers.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void EnsureResolutionMode(
		IServiceProvider serviceProvider,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type handlerType,
		bool isRegistered)
	{
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(handlerType);

		var modeCache = _serviceResolutionModesByProvider.GetValue(
			serviceProvider,
			static _ => new ConcurrentDictionary<Type, ServiceResolutionMode>());
		if (modeCache.ContainsKey(handlerType))
		{
			return;
		}

		var mode = isRegistered
			? ServiceResolutionMode.RegisteredRequiredService
			: ServiceResolutionMode.Factory;
		_ = modeCache.TryAdd(handlerType, mode);
	}

	/// <summary>
	/// Determines whether precompiled context setter support exists for the specified handler.
	/// </summary>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "PrecompiledHandlerActivator is generated at compile time and is AOT-safe")]
	private static bool SupportsPrecompiledContext(Type handlerType)
	{
		lock (_cacheLock)
		{
			if (!_precompiledChecked)
			{
				try
				{
					// Probe generated type availability once.
					_ = PrecompiledHandlerActivator.HasContextProperty(handlerType);
					_precompiledAvailable = true;
				}
				catch (TypeLoadException)
				{
					_precompiledAvailable = false;
				}

				_precompiledChecked = true;
			}

			if (!_precompiledAvailable)
			{
				return false;
			}

			if (_precompiledContextSupportCache.TryGetValue(handlerType, out var supported))
			{
				return supported;
			}

			try
			{
				supported = PrecompiledHandlerActivator.HasContextProperty(handlerType);
			}
			catch (InvalidOperationException)
			{
				supported = false;
			}

			_precompiledContextSupportCache[handlerType] = supported;
			return supported;
		}
	}

	/// <summary>
	/// Clears all activator caches. Intended for tests and benchmark setup.
	/// </summary>
	internal static void ClearCache()
	{
		lock (_cacheLock)
		{
			_contextSetterCache.Clear();
			_activationPlanCache.Clear();
			_registeredActivationPlanCache.Clear();
			_factoryActivationPlanCache.Clear();
			_precompiledContextSupportCache.Clear();
			_frozenSetterCache = null;
			_frozenActivationPlanCache = null;
			_frozenRegisteredActivationPlanCache = null;
			_frozenFactoryActivationPlanCache = null;
			_precompiledChecked = false;
			_precompiledAvailable = false;
			_serviceResolutionModesByProvider = new ConditionalWeakTable<IServiceProvider, ConcurrentDictionary<Type, ServiceResolutionMode>>();
		}
	}
}
