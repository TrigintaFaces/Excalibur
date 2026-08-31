// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.DynamoDb.Snapshots;
using Excalibur.EventSourcing;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring DynamoDB snapshot store services.
/// </summary>
public static class DynamoDbSnapshotStoreExtensions
{
	/// <summary>
	/// Adds DynamoDB snapshot store services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the snapshot store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddDynamoDbSnapshotStore(options =>
	/// {
	///     options.Connection.Region = "us-east-1";
	///     options.TableName = "snapshots";
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddDynamoDbSnapshotStore(
		this IServiceCollection services,
		Action<DynamoDbSnapshotStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<DynamoDbSnapshotStoreOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DynamoDbSnapshotStoreOptions>, DynamoDbSnapshotStoreOptionsValidator>());

		_ = services.AddDefaultTenantContext();

		// AddTenantAwareStore builds the store (injecting ITenantContext, since this store's constructors
		// declare one) AND emits the ITenantScopingCapability<ISnapshotStore> marker inseparably. A bare
		// AddSingleton here registered a store that honors the ambient tenant while attesting nothing, so
		// RowDiscriminator rejected a snapshot store that was in fact tenant-scoped.
		_ = services.AddTenantAwareStore<ISnapshotStore, DynamoDbSnapshotStore>();

		// The seam registers the store under its own concrete type, so the contract needs an alias to stay
		// resolvable, at the same singleton lifetime it had before.
		services.TryAddSingleton<ISnapshotStore>(sp => sp.GetRequiredService<DynamoDbSnapshotStore>());

		return services;
	}

	/// <summary>
	/// Adds DynamoDB snapshot store services with a named options configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="name">The name of the options configuration.</param>
	/// <param name="configure">Action to configure the snapshot store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDynamoDbSnapshotStore(
		this IServiceCollection services,
		string name,
		Action<DynamoDbSnapshotStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<DynamoDbSnapshotStoreOptions>(name)
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DynamoDbSnapshotStoreOptions>, DynamoDbSnapshotStoreOptionsValidator>());

		_ = services.AddDefaultTenantContext();

		// AddTenantAwareStore builds the store (injecting ITenantContext, since this store's constructors
		// declare one) AND emits the ITenantScopingCapability<ISnapshotStore> marker inseparably. A bare
		// AddSingleton here registered a store that honors the ambient tenant while attesting nothing, so
		// RowDiscriminator rejected a snapshot store that was in fact tenant-scoped.
		_ = services.AddTenantAwareStore<ISnapshotStore, DynamoDbSnapshotStore>();

		// The seam registers the store under its own concrete type, so the contract needs an alias to stay
		// resolvable, at the same singleton lifetime it had before.
		services.TryAddSingleton<ISnapshotStore>(sp => sp.GetRequiredService<DynamoDbSnapshotStore>());

		return services;
	}
}
