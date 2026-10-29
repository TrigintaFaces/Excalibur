// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.Firestore.Projections;
using Excalibur.EventSourcing;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Firestore projection stores.
/// </summary>
public static class FirestoreProjectionStoreExtensions
{
	/// <summary>
	/// Adds a Firestore projection store for the specified projection type.
	/// </summary>
	/// <typeparam name="TProjection">The projection type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Optional configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Settings are written to a NAMED options instance keyed on the projection type, so registering a
	/// second projection in the same container scopes its collection name rather than overwriting the
	/// first's. Validation is wired per name and eagerly at start-up: a validator registered for the
	/// unnamed instance would never run against a named one, so a misconfigured collection name would
	/// surface only on first use.
	/// </remarks>
	public static IServiceCollection AddFirestoreProjectionStore<TProjection>(
		this IServiceCollection services,
		Action<FirestoreProjectionStoreOptions>? configure = null)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);

		var optionsName = FirestoreProjectionCollectionConvention.GetOptionsName<TProjection>();

		if (configure != null)
		{
			_ = services.Configure(optionsName, configure);
		}

		_ = services.AddOptions<FirestoreProjectionStoreOptions>(optionsName).ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<FirestoreProjectionStoreOptions>,
				FirestoreProjectionStoreOptionsValidator>());

		services.TryAddSingleton<IProjectionStore<TProjection>, FirestoreProjectionStore<TProjection>>();

		return services;
	}
}
