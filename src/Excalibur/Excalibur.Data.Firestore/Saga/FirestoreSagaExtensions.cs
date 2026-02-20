// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Saga;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Abstractions.Serialization;

using Google.Cloud.Firestore;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Firestore saga store services.
/// </summary>
public static class FirestoreSagaExtensions
{
	/// <summary>
	/// Adds the Firestore saga store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure saga store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers <see cref="FirestoreSagaStore"/> as the implementation of <see cref="ISagaStore"/>.
	/// The store uses Firestore documents per saga instance.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddFirestoreSagaStore(options =>
	/// {
	///     options.ProjectId = "my-project";
	///     options.CollectionName = "sagas";
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddFirestoreSagaStore(
		this IServiceCollection services,
		Action<FirestoreSagaOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = services.AddOptions<FirestoreSagaOptions>()
			.Configure(configureOptions)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton<FirestoreSagaStore>();
		services.TryAddSingleton<ISagaStore>(sp => sp.GetRequiredService<FirestoreSagaStore>());

		return services;
	}

	/// <summary>
	/// Adds the Firestore saga store to the service collection with project ID.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="projectId">The Google Cloud project ID.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddFirestoreSagaStore(
		this IServiceCollection services,
		string projectId)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

		return services.AddFirestoreSagaStore(options =>
		{
			options.ProjectId = projectId;
		});
	}

	/// <summary>
	/// Adds the Firestore saga store to the service collection with an existing Firestore database.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="dbProvider">A factory function that provides the Firestore database.</param>
	/// <param name="configureOptions">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddFirestoreSagaStore(
		this IServiceCollection services,
		Func<IServiceProvider, FirestoreDb> dbProvider,
		Action<FirestoreSagaOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(dbProvider);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = services.AddOptions<FirestoreSagaOptions>()
			.Configure(configureOptions)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton(sp =>
		{
			var db = dbProvider(sp);
			var options = sp.GetRequiredService<IOptions<FirestoreSagaOptions>>();
			var logger = sp.GetRequiredService<ILogger<FirestoreSagaStore>>();
			var serializer = sp.GetRequiredService<IJsonSerializer>();
			return new FirestoreSagaStore(db, options, logger, serializer);
		});
		services.TryAddSingleton<ISagaStore>(sp => sp.GetRequiredService<FirestoreSagaStore>());

		return services;
	}
}
