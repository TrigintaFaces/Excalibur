// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Compliance;
using Excalibur.Compliance.Stores.MongoDb;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the MongoDB compliance store with dependency injection.
/// </summary>
/// <remarks>
/// MongoDB storage is not part of <c>Excalibur.Compliance</c>. Install the
/// <c>Excalibur.Compliance.MongoDb</c> package and call one of these methods to bind
/// <see cref="IComplianceStore"/> to MongoDB; only that package brings the MongoDB driver into a
/// consumer's dependency graph.
/// </remarks>
public static class MongoDbComplianceStoreServiceCollectionExtensions
{
	/// <summary>
	/// Adds the MongoDB compliance store for durable GDPR record storage.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Configuration for MongoDB compliance options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbComplianceStore(
		this IServiceCollection services,
		Action<MongoDbComplianceOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		var optionsBuilder = services.AddOptions<MongoDbComplianceOptions>()
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MongoDbComplianceOptions>, MongoDbComplianceOptionsValidator>());
		_ = optionsBuilder.Configure(configureOptions);

		// Idempotent single-tenant default, so GetRequiredService<ITenantContext>() below always resolves for a
		// consumer who never opted into multi-tenancy; TryAdd means the multi-tenancy composition's ambient
		// context wins when it is present.
		_ = services.AddDefaultTenantContext();

		// Dep-gated registration: MongoDbComplianceStore's constructors declare an ITenantContext
		// parameter, so the seam resolves it (fail-closed) before construction, threads it in via
		// ActivatorUtilities, and emits the ITenantScopingCapability<IComplianceStore> marker inseparably
		// from that wiring -- an unwired store cannot carry a truthful-looking capability marker. Registering
		// the store plainly, as this did, left it with NO marker at all, so a host that composed this store
		// with multi-tenancy was refused by the gate for a store that does in fact scope by tenant.
		services.AddTenantAwareStore<IComplianceStore, MongoDbComplianceStore>();

		// The seam registers the CONCRETE store (so the capability marker is bound to a real instance); the
		// contract itself is mapped here, forwarding to that same singleton rather than constructing a second,
		// unwired instance.
		services.TryAddSingleton<IComplianceStore>(static sp => sp.GetRequiredService<MongoDbComplianceStore>());
		return services;
	}

	/// <summary>
	/// Adds the MongoDB compliance store using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="MongoDbComplianceOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbComplianceStore(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<MongoDbComplianceOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MongoDbComplianceOptions>, MongoDbComplianceOptionsValidator>());

		// Idempotent single-tenant default, so GetRequiredService<ITenantContext>() below always resolves for a
		// consumer who never opted into multi-tenancy; TryAdd means the multi-tenancy composition's ambient
		// context wins when it is present.
		_ = services.AddDefaultTenantContext();

		// Dep-gated registration: MongoDbComplianceStore's constructors declare an ITenantContext
		// parameter, so the seam resolves it (fail-closed) before construction, threads it in via
		// ActivatorUtilities, and emits the ITenantScopingCapability<IComplianceStore> marker inseparably
		// from that wiring -- an unwired store cannot carry a truthful-looking capability marker. Registering
		// the store plainly, as this did, left it with NO marker at all, so a host that composed this store
		// with multi-tenancy was refused by the gate for a store that does in fact scope by tenant.
		services.AddTenantAwareStore<IComplianceStore, MongoDbComplianceStore>();

		// The seam registers the CONCRETE store (so the capability marker is bound to a real instance); the
		// contract itself is mapped here, forwarding to that same singleton rather than constructing a second,
		// unwired instance.
		services.TryAddSingleton<IComplianceStore>(static sp => sp.GetRequiredService<MongoDbComplianceStore>());
		return services;
	}
}
