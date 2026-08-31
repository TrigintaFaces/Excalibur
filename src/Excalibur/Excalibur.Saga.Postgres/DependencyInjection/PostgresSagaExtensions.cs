// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.Postgres;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Postgres saga store.
/// </summary>
public static class PostgresSagaExtensions
{
	/// <summary>
	/// Adds Postgres saga store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers <see cref="PostgresSagaStore"/> as the implementation of <see cref="ISagaStore"/>.
	/// The store uses Postgres's JSONB column type for efficient saga state serialization.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddPostgresSagaStore(options =>
	/// {
	///     options.ConnectionString = "Host=localhost;Database=myapp;";
	///     options.Schema = "dispatch";
	///     options.TableName = "sagas";
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Registers the reflection-based dispatch pipeline, which requires types that trimming may remove. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Registers the reflection-based dispatch pipeline, which constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	public static IServiceCollection AddPostgresSagaStore(
		this IServiceCollection services,
		Action<PostgresSagaOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Ensure core saga registration, the way AddExcaliburSaga ensures AddDispatch: this is a public
		// entry point, so a consumer who calls only this must still receive a resolvable ISagaStore. Core
		// owns the non-keyed contract alias rather than each provider re-registering it. Idempotent, so a
		// consumer who also calls AddSagas composes cleanly.
		_ = services.AddExcaliburSaga();

		_ = services.AddOptions<PostgresSagaOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<PostgresSagaOptions>, PostgresSagaOptionsValidator>());

		// Fail-closed single-tenant default so the dep-gated AddTenantAwareStore seam resolves
		// ITenantContext. AddMultiTenancy REPLACES this registration (never TryAdd), so an ambient
		// multi-tenant context still wins regardless of composition order.
		services.AddDefaultTenantContext();
		// The remaining constructor dependencies, so this entry point can build its own store rather than
		// only working in hosts that happen to have composed them already. Both are TryAdd-based and so
		// defer to a host that registers them itself.
		_ = services.AddLogging();
		services.TryAddSingleton<DispatchJsonSerializer>();
		// AddTenantAwareStore threads the resolved ITenantContext into construction (dep-gated: absent
		// context ⇒ resolution fails closed, since this store's constructor declares one) AND emits the
		// ITenantScopingCapability<ISagaStore> marker inseparably ((B) — a store built without
		// the ambient tenant is inexpressible here).
		services.AddTenantAwareStore<ISagaStore, PostgresSagaStore>();
		services.AddKeyedSingleton<ISagaStore>("postgres", (sp, _) => sp.GetRequiredService<PostgresSagaStore>());
		services.TryAddKeyedSingleton<ISagaStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ISagaStore>("postgres"));

		// Admin/query surface (dashboard + operational tooling) — the same store instance.
		services.TryAddSingleton<ISagaStoreAdmin>(static sp => sp.GetRequiredService<PostgresSagaStore>());

		return services;
	}

	/// <summary>
	/// Adds Postgres saga store to the service collection with a connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactoryProvider">
	/// A factory function that creates <see cref="NpgsqlConnection"/> instances from the service provider.
	/// </param>
	/// <param name="configure">Action to configure the options (used for table names, timeouts, etc.).</param>
	/// <returns>The service collection for chaining.</returns>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Registers the reflection-based dispatch pipeline, which requires types that trimming may remove. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Registers the reflection-based dispatch pipeline, which constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	public static IServiceCollection AddPostgresSagaStore(
		this IServiceCollection services,
		Func<IServiceProvider, Func<NpgsqlConnection>> connectionFactoryProvider,
		Action<PostgresSagaOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);
		ArgumentNullException.ThrowIfNull(configure);

		// Ensure core saga registration, the way AddExcaliburSaga ensures AddDispatch: this is a public
		// entry point, so a consumer who calls only this must still receive a resolvable ISagaStore. Core
		// owns the non-keyed contract alias rather than each provider re-registering it. Idempotent, so a
		// consumer who also calls AddSagas composes cleanly.
		_ = services.AddExcaliburSaga();

		_ = services.AddOptions<PostgresSagaOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<PostgresSagaOptions>, PostgresSagaOptionsValidator>());

		// Fail-closed single-tenant default so the dep-gated AddTenantAwareStore seam resolves
		// ITenantContext. AddMultiTenancy REPLACES this registration (never TryAdd), so an ambient
		// multi-tenant context still wins regardless of composition order.
		services.AddDefaultTenantContext();
		// AddTenantAwareStore builds the store (injecting ITenantContext, since this store's constructor
		// declares one) AND emits the ITenantScopingCapability<ISagaStore> marker as one inseparable act
		//.
		services.AddTenantAwareStore<ISagaStore, PostgresSagaStore>(sp =>
		{
			var connectionFactory = connectionFactoryProvider(sp);
			var options = sp.GetRequiredService<IOptions<PostgresSagaOptions>>().Value;
			var logger = sp.GetRequiredService<ILogger<PostgresSagaStore>>();
			var serializer = sp.GetRequiredService<DispatchJsonSerializer>();
			return new PostgresSagaStore(connectionFactory, options, logger, serializer, sp.GetRequiredService<ITenantContext>());
		});
		services.AddKeyedSingleton<ISagaStore>("postgres", (sp, _) => sp.GetRequiredService<PostgresSagaStore>());
		services.TryAddKeyedSingleton<ISagaStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ISagaStore>("postgres"));

		// Admin/query surface (dashboard + operational tooling) — the same store instance.
		services.TryAddSingleton<ISagaStoreAdmin>(static sp => sp.GetRequiredService<PostgresSagaStore>());

		return services;
	}
}
