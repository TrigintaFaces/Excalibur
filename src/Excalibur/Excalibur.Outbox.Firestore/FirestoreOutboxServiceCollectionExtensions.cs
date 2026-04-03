// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Outbox.Firestore;

using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Firestore outbox store in dependency injection.
/// </summary>
public static class FirestoreOutboxServiceCollectionExtensions
{
	/// <summary>
	/// Adds the Firestore outbox store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddFirestoreOutboxStore(
		this IServiceCollection services,
		Action<FirestoreOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<FirestoreOutboxOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		_ = services.AddSingleton<FirestoreOutboxStore>();
		_ = services.AddSingleton<ICloudNativeOutboxStore>(sp => sp.GetRequiredService<FirestoreOutboxStore>());

		return services;
	}

	/// <summary>
	/// Adds the Firestore outbox store to the service collection with configuration from a section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section containing the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddFirestoreOutboxStore(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<FirestoreOutboxOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		_ = services.AddSingleton<FirestoreOutboxStore>();
		_ = services.AddSingleton<ICloudNativeOutboxStore>(sp => sp.GetRequiredService<FirestoreOutboxStore>());

		return services;
	}

	/// <summary>
	/// Adds the Firestore outbox store to the service collection with options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="options">The pre-configured options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddFirestoreOutboxStore(
		this IServiceCollection services,
		FirestoreOutboxOptions options)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(options);

		_ = services.AddOptions<FirestoreOutboxOptions>()
			.Configure(o =>
		{
			o.ProjectId = options.ProjectId;
			o.CredentialsPath = options.CredentialsPath;
			o.CredentialsJson = options.CredentialsJson;
			o.EmulatorHost = options.EmulatorHost;
			o.CollectionName = options.CollectionName;
			o.DefaultTimeToLiveSeconds = options.DefaultTimeToLiveSeconds;
			o.MaxBatchSize = options.MaxBatchSize;
			o.CreateCollectionIfNotExists = options.CreateCollectionIfNotExists;
			o.MaxRetryAttempts = options.MaxRetryAttempts;
		})
			.ValidateDataAnnotations()
			.ValidateOnStart();
		_ = services.AddSingleton<FirestoreOutboxStore>();
		_ = services.AddSingleton<ICloudNativeOutboxStore>(sp => sp.GetRequiredService<FirestoreOutboxStore>());

		return services;
	}
}
