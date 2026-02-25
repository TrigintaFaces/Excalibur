// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Firestore.Snapshots;
using Excalibur.EventSourcing.Abstractions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Firestore snapshot store services.
/// </summary>
public static class FirestoreSnapshotStoreExtensions
{
	/// <summary>
	/// Adds Firestore snapshot store services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the snapshot store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddFirestoreSnapshotStore(options =>
	/// {
	///     options.ProjectId = "my-gcp-project";
	///     options.CollectionName = "snapshots";
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddFirestoreSnapshotStore(
		this IServiceCollection services,
		Action<FirestoreSnapshotStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		_ = services.AddSingleton<ISnapshotStore, FirestoreSnapshotStore>();

		return services;
	}

	/// <summary>
	/// Adds Firestore snapshot store services with a named options configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="name">The name of the options configuration.</param>
	/// <param name="configure">Action to configure the snapshot store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddFirestoreSnapshotStore(
		this IServiceCollection services,
		string name,
		Action<FirestoreSnapshotStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(name, configure);
		_ = services.AddSingleton<ISnapshotStore, FirestoreSnapshotStore>();

		return services;
	}
}
