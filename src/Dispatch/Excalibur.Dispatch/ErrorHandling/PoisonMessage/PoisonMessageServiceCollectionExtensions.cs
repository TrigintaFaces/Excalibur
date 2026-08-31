// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Options.ErrorHandling;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring poison message handling services.
/// </summary>
public static class PoisonMessageServiceCollectionExtensions
{
	/// <summary>
	/// Adds poison message handling services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration section for poison message options. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming. Ensure options types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode("Configuration binding requires dynamic code generation for property reflection and value conversion.")]
	public static IServiceCollection AddPoisonMessageHandling(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<PoisonMessageOptions>, PoisonMessageOptionsValidator>());

		_ = services.AddOptions<PoisonMessageOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		return services.AddPoisonMessageHandling();
	}

	/// <summary>
	/// Adds poison message handling services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Action to configure poison message options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddPoisonMessageHandling(
		this IServiceCollection services,
		Action<PoisonMessageOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<PoisonMessageOptions>, PoisonMessageOptionsValidator>());

		var optionsBuilder = services.AddOptions<PoisonMessageOptions>();
		if (configureOptions != null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		_ = optionsBuilder.ValidateOnStart();

		// Register core services
		services.TryAddSingleton<IPoisonMessageHandler, PoisonMessageHandler>();

		// Register default detectors
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IPoisonMessageDetector, RetryCountPoisonDetector>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IPoisonMessageDetector, ExceptionTypePoisonDetector>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IPoisonMessageDetector, TimespanPoisonDetector>());

		// Register composite detector as the primary detector
		services.TryAddSingleton<IPoisonMessageDetector, CompositePoisonDetector>();

		// Register middleware concrete type for pipeline resolution
		services.TryAddSingleton<PoisonMessageMiddleware>();

		// Decided BEFORE the default is added, and the order is load-bearing. The framework's own default
		// is registered by a FACTORY (see below), and a factory descriptor exposes no implementation type —
		// so asking this question afterwards cannot tell "our default, which does implement the facet" from
		// "a consumer factory, which may not", and would answer no for both. Asking first means the
		// no-descriptor case unambiguously means "our default will be in effect".
		var adminFacetAvailable = ImplementsAdminFacet(services);

		// Register default in-memory store if no store is registered.
		// AddTenantAwareStore constructs the store (injecting ITenantContext, since its constructor
		// requires one) AND emits the ITenantScopingCapability<IDeadLetterStore> marker inseparably. The
		// TryAdd semantics match this call site exactly: a consumer's own IDeadLetterStore, registered
		// before AddPoisonMessageHandling(), still wins.
		_ = services.AddDefaultTenantContext();
		_ = services.AddTenantAwareStore<IDeadLetterStore, InMemoryDeadLetterStore>();
		services.TryAddSingleton<IDeadLetterStore>(sp => sp.GetRequiredService<InMemoryDeadLetterStore>());

		// The admin facet is an OPTIONAL capability, not part of the store contract: a consumer-supplied
		// IDeadLetterStore is a supported extension point (TryAdd = consumer wins) and is not required to
		// implement IDeadLetterStoreAdmin. Every consumer of the facet inside this subsystem tests for it
		// rather than assuming it (PoisonMessageHandler, PoisonMessageCleanupService), so the registration
		// must agree with that contract.
		//
		// It is therefore registered only when the store actually in effect is known to implement it, and
		// it delegates to the resolved IDeadLetterStore so the admin facet is the SAME INSTANCE as the
		// store doing the work — never a second one. A store whose implementation type cannot be known at
		// registration time (a factory registration) is treated as non-admin: the facet stays unresolvable,
		// which surfaces as a missing service rather than an InvalidCastException from a blind cast.
		// A provider whose store does implement the facet registers it explicitly, as the SQL Server,
		// PostgreSQL, and AddInMemoryDeadLetterStore paths do.
		if (adminFacetAvailable)
		{
			services.TryAddSingleton<IDeadLetterStoreAdmin>(
				sp => sp.GetRequiredService<IDeadLetterStore>() as IDeadLetterStoreAdmin
					?? throw new InvalidOperationException(
						$"The registered {nameof(IDeadLetterStore)} does not implement {nameof(IDeadLetterStoreAdmin)}, "
						+ "so administrative operations are unavailable. This happens when a custom store is registered "
						+ "after AddPoisonMessageHandling(); register it before, or register the admin facet explicitly "
						+ "alongside the store. Poison-message handling itself does not require the admin facet."));
		}

		// Register cleanup service if auto-cleanup is enabled
		_ = services.AddHostedService<PoisonMessageCleanupService>();

		return services;
	}

	/// <summary>
	/// Adds poison message handling services with a custom detector to the service collection.
	/// </summary>
	/// <typeparam name="TDetector"> The custom poison message detector type. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Optional action to configure poison message options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddPoisonMessageHandling<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TDetector>(
		this IServiceCollection services,
		Action<PoisonMessageOptions>? configureOptions = null)
		where TDetector : class, IPoisonMessageDetector
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddPoisonMessageHandling(configureOptions);
		_ = services.AddPoisonMessageDetector<TDetector>();

		return services;
	}

	/// <summary>
	/// Configures poison message handling to use an in-memory dead letter store.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddInMemoryDeadLetterStore(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// This method's contract is an unconditional override -- REPLACE whatever store is currently
		// registered, and hand the caller a FRESH instance every time it is called. AddTenantAwareStore
		// registers the concrete store via TryAddSingleton, which is first-wins by design, so a second
		// call would otherwise resolve back to the FIRST instance and silently keep whatever state had
		// accumulated. Clearing the concrete descriptor first restores the fresh-per-call semantics
		// WITHOUT hand-rolling the construction: the seam still builds the store from the resolved
		// ITenantContext and emits ITenantScopingCapability<IDeadLetterStore> as one inseparable act.
		//
		// Hand-rolling it here was a real hole rather than a style choice, and it only became reachable
		// once IDeadLetterStore was marked tenant-owned. This override path emitted no capability marker
		// at all, so a host that called it and then composed row-discriminator multi-tenancy would be
		// refused at startup for a store that does scope correctly -- and, worse, the shape invited the
		// opposite repair of registering the marker beside the store, which is exactly the marker that
		// can be true while the property is false.
		_ = services.RemoveAll<IDeadLetterStore>();
		_ = services.RemoveAll<IDeadLetterStoreAdmin>();
		_ = services.RemoveAll<InMemoryDeadLetterStore>();
		_ = services.AddDefaultTenantContext();
		_ = services.AddTenantAwareStore<IDeadLetterStore, InMemoryDeadLetterStore>();
		_ = services.AddSingleton<IDeadLetterStore>(sp => sp.GetRequiredService<InMemoryDeadLetterStore>());
		_ = services.AddSingleton<IDeadLetterStoreAdmin>(sp => sp.GetRequiredService<InMemoryDeadLetterStore>());

		return services;
	}

	// NOTE: SQL dead letter store moved to Excalibur.Data.SqlServer.AddSqlServerDeadLetterStore()

	/// <summary>
	/// Determines whether the <see cref="IDeadLetterStore"/> currently in effect is known to implement the
	/// optional <see cref="IDeadLetterStoreAdmin"/> facet.
	/// </summary>
	/// <param name="services"> The service collection to inspect. </param>
	/// <returns>
	/// <see langword="true"/> when the last registered store descriptor names a concrete type implementing
	/// <see cref="IDeadLetterStoreAdmin"/>; otherwise <see langword="false"/>.
	/// </returns>
	/// <remarks>
	/// The LAST descriptor is the one that answers, because a later <c>Add</c> of the same service type wins
	/// at resolution. A factory-only descriptor exposes no implementation type, so it is reported as
	/// non-admin: declining to register a facet that may not exist is the fail-safe direction, since the
	/// alternative is a cast that throws at resolve time.
	/// </remarks>
	private static bool ImplementsAdminFacet(IServiceCollection services)
	{
		for (var i = services.Count - 1; i >= 0; i--)
		{
			var descriptor = services[i];
			if (descriptor.ServiceType != typeof(IDeadLetterStore))
			{
				continue;
			}

			var implementationType = descriptor.GetImplementationType()
				?? descriptor.GetImplementationInstance()?.GetType();

			return implementationType is not null
				&& typeof(IDeadLetterStoreAdmin).IsAssignableFrom(implementationType);
		}

		// No store registered: the framework's default InMemoryDeadLetterStore will be in effect, and it
		// does implement the admin facet. This branch is only sound because the caller asks BEFORE adding
		// that default — see the call site.
		return true;
	}

	/// <summary>
	/// Adds a custom poison message detector.
	/// </summary>
	/// <typeparam name="TDetector"> The type of the detector to add. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddPoisonMessageDetector<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TDetector>(this IServiceCollection services)
		where TDetector : class, IPoisonMessageDetector
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddEnumerable(ServiceDescriptor.Singleton<IPoisonMessageDetector, TDetector>());

		return services;
	}

	/// <summary>
	/// Removes a poison message detector.
	/// </summary>
	/// <typeparam name="TDetector"> The type of the detector to remove. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection RemovePoisonMessageDetector<TDetector>(this IServiceCollection services)
		where TDetector : class, IPoisonMessageDetector
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.RemoveAll<TDetector>();

		return services;
	}
}
