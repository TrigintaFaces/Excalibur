// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Caching.AdaptiveTtl;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the adaptive-TTL distributed cache decorator.
/// </summary>
public static class AdaptiveTtlCacheServiceCollectionExtensions
{
	/// <summary>
	/// Decorates the currently-registered <see cref="IDistributedCache"/> with
	/// <see cref="AdaptiveTtlCache"/>, wiring the rule-based TTL strategy and the default
	/// system-load monitor.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">An optional delegate to configure <see cref="RuleBasedTtlOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when no <see cref="IDistributedCache"/> has been registered before this call. Register a
	/// base cache first (for example <c>AddDistributedMemoryCache()</c> or <c>AddStackExchangeRedisCache(...)</c>).
	/// </exception>
	/// <remarks>
	/// The strategy (<see cref="RuleBasedAdaptiveTtlStrategy"/>) and monitor
	/// (<see cref="DefaultSystemLoadMonitor"/>) are registered with <c>TryAdd</c> semantics, so a
	/// consumer may override either by registering their own implementation first. Options are validated
	/// via data annotations at startup (<see cref="OptionsBuilderExtensions.ValidateOnStart{TOptions}"/>).
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "RuleBasedTtlOptions is a fixed, statically-known options type; its data-annotation members are preserved.")]
	public static IServiceCollection AddAdaptiveTtlCache(
		this IServiceCollection services,
		Action<RuleBasedTtlOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var optionsBuilder = services.AddOptions<RuleBasedTtlOptions>();
		if (configure is not null)
		{
			_ = optionsBuilder.Configure(configure);
		}

		_ = optionsBuilder.ValidateDataAnnotations().ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<RuleBasedTtlOptions>, RuleBasedTtlOptionsValidator>());
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<RuleBasedTtlOptions>, AdaptiveTtlOptionsValidator<RuleBasedTtlOptions>>());

		// The strategy consumes the RuleBasedTtlOptions value object directly (not IOptions<T>).
		services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<RuleBasedTtlOptions>>().Value);
		services.TryAddSingleton<ISystemLoadMonitor, DefaultSystemLoadMonitor>();
		services.TryAddSingleton<IAdaptiveTtlStrategy, RuleBasedAdaptiveTtlStrategy>();

		DecorateDistributedCache(services);

		return services;
	}

	private static void DecorateDistributedCache(IServiceCollection services)
	{
		var innerDescriptor = services.LastOrDefault(static d => d.ServiceType == typeof(IDistributedCache))
			?? throw new InvalidOperationException(
				"AddAdaptiveTtlCache requires an IDistributedCache to be registered first. " +
				"Register a base cache (e.g. AddDistributedMemoryCache() or AddStackExchangeRedisCache(...)) before calling AddAdaptiveTtlCache.");

		_ = services.Remove(innerDescriptor);

		services.Add(new ServiceDescriptor(
			typeof(IDistributedCache),
			sp =>
			{
				var inner = (IDistributedCache)InstantiateInner(innerDescriptor, sp);
				return new AdaptiveTtlCache(
					inner,
					sp.GetRequiredService<IAdaptiveTtlStrategy>(),
					sp.GetRequiredService<ILogger<AdaptiveTtlCache>>(),
					sp.GetRequiredService<ISystemLoadMonitor>(),
					sp.GetService<TimeProvider>());
			},
			innerDescriptor.Lifetime));
	}

	private static object InstantiateInner(ServiceDescriptor descriptor, IServiceProvider serviceProvider)
	{
		if (descriptor.GetImplementationInstance() is { } instance)
		{
			return instance;
		}

		if (descriptor.GetImplementationFactory() is { } factory)
		{
			return factory(serviceProvider);
		}

		return ActivatorUtilities.CreateInstance(serviceProvider, descriptor.GetImplementationType()!);
	}
}
