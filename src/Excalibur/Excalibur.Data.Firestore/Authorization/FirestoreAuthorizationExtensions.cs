// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.Firestore.Authorization;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Firestore authorization services.
/// </summary>
public static class FirestoreAuthorizationExtensions
{
	/// <summary>
	/// Adds Firestore-based authorization services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddFirestoreAuthorization(
		this IServiceCollection services,
		Action<FirestoreAuthorizationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		services.TryAddSingleton<IGrantRequestProvider, FirestoreGrantService>();
		services.TryAddSingleton<IActivityGroupGrantService, FirestoreActivityGroupGrantService>();

		return services;
	}

	/// <summary>
	/// Adds Firestore-based authorization services to the service collection with emulator configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="projectId">The Google Cloud project ID.</param>
	/// <param name="emulatorHost">The Firestore emulator host (e.g., "localhost:8080").</param>
	/// <param name="grantsCollectionName">The grants collection name. Defaults to "authorization_grants".</param>
	/// <param name="activityGroupsCollectionName">The activity groups collection name. Defaults to "authorization_activity_groups".</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddFirestoreAuthorization(
		this IServiceCollection services,
		string projectId,
		string emulatorHost,
		string grantsCollectionName = "authorization_grants",
		string activityGroupsCollectionName = "authorization_activity_groups")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
		ArgumentException.ThrowIfNullOrWhiteSpace(emulatorHost);

		return services.AddFirestoreAuthorization(options =>
		{
			options.ProjectId = projectId;
			options.EmulatorHost = emulatorHost;
			options.GrantsCollectionName = grantsCollectionName;
			options.ActivityGroupsCollectionName = activityGroupsCollectionName;
		});
	}

	/// <summary>
	/// Adds only the Firestore grant service to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddFirestoreGrantService(
		this IServiceCollection services,
		Action<FirestoreAuthorizationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		services.TryAddSingleton<IGrantRequestProvider, FirestoreGrantService>();

		return services;
	}

	/// <summary>
	/// Adds only the Firestore activity group grant service to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddFirestoreActivityGroupGrantService(
		this IServiceCollection services,
		Action<FirestoreAuthorizationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		services.TryAddSingleton<IActivityGroupGrantService, FirestoreActivityGroupGrantService>();

		return services;
	}
}
