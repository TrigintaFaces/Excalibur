// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Pipeline;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for optimizing Dispatch performance after host build.
/// </summary>
/// <remarks>
/// PERF-13/PERF-14: These extensions freeze internal caches after the DI container has been built
/// and all handlers have been registered. Once frozen, caches use <see cref="System.Collections.Frozen.FrozenDictionary{TKey, TValue}"/>
/// for O(1) lookups with zero synchronization overhead.
/// </remarks>
public static class DispatchOptimizationHostExtensions
{
	/// <summary>
	/// Optimizes Dispatch for production workloads by freezing all internal caches.
	/// </summary>
	/// <param name="host">The host to configure.</param>
	/// <returns>The configured host for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Call this method after building the host and before running it to enable
	/// production-optimized lookup performance. This freezes the following caches:
	/// </para>
	/// <list type="bullet">
	/// <item><see cref="HandlerInvoker"/> - Handler invocation delegates</item>
	/// <item><see cref="HandlerInvokerRegistry"/> - Manual handler registrations</item>
	/// <item><see cref="HandlerActivator"/> - Handler activation plans</item>
	/// <item><see cref="FinalDispatchHandler"/> - Result factory delegates</item>
	/// <item><see cref="MiddlewareApplicabilityEvaluator"/> - Middleware metadata</item>
	/// </list>
	/// <para>
	/// Once frozen, caches use <see cref="System.Collections.Frozen.FrozenDictionary{TKey, TValue}"/>
	/// which provides O(1) lookups with zero synchronization overhead. The warmup
	/// dictionaries are released for garbage collection.
	/// </para>
	/// <para>
	/// This method is idempotent - calling it multiple times has no additional effect.
	/// </para>
	/// <example>
	/// <code>
	/// var builder = Host.CreateApplicationBuilder(args);
	/// builder.Services.AddDispatch(dispatch => dispatch.AddHandlersFromAssembly());
	///
	/// var app = builder.Build();
	/// app.UseOptimizedDispatch(); // Freeze caches after build
	/// await app.RunAsync();
	/// </code>
	/// </example>
	/// </remarks>
	public static IHost UseOptimizedDispatch(this IHost host)
	{
		ArgumentNullException.ThrowIfNull(host);

		// Freeze all handler-related caches
		HandlerInvoker.FreezeCache();
		HandlerInvokerRegistry.FreezeCache();
		HandlerActivator.FreezeCache();

		// Freeze result factory cache
		FinalDispatchHandler.FreezeResultFactoryCache();

		// Freeze middleware metadata cache
		MiddlewareApplicabilityEvaluator.FreezeCache();

		return host;
	}

	/// <summary>
	/// Gets a value indicating whether all Dispatch caches have been frozen.
	/// </summary>
	/// <param name="host">The host to check.</param>
	/// <returns><see langword="true"/> if all caches are frozen; otherwise, <see langword="false"/>.</returns>
	/// <remarks>
	/// This method checks if <see cref="UseOptimizedDispatch"/> has been called and
	/// all caches are in their frozen state.
	/// </remarks>
	public static bool IsDispatchOptimized(this IHost host)
	{
		ArgumentNullException.ThrowIfNull(host);

		return HandlerInvoker.IsCacheFrozen
			&& HandlerInvokerRegistry.IsCacheFrozen
			&& HandlerActivator.IsCacheFrozen
			&& FinalDispatchHandler.IsResultFactoryCacheFrozen
			&& MiddlewareApplicabilityEvaluator.IsCacheFrozen;
	}
}
