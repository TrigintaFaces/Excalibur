// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Firestore.Outbox;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;

using Google.Cloud.Firestore;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Firestore outbox store.
/// </summary>
public static class FirestoreOutboxExtensions
{
	/// <summary>
	/// Adds Firestore outbox store to the service collection.
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

		_ = services.Configure(configure);
		services.TryAddSingleton<FirestoreOutboxStore>();
		services.TryAddSingleton<IOutboxStore>(sp => sp.GetRequiredService<FirestoreOutboxStore>());

		return services;
	}

	/// <summary>
	/// Adds Firestore outbox store to the service collection with project ID.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="projectId">The Google Cloud project ID.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddFirestoreOutboxStore(
		this IServiceCollection services,
		string projectId)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

		return services.AddFirestoreOutboxStore(options =>
		{
			options.ProjectId = projectId;
		});
	}

	/// <summary>
	/// Adds Firestore outbox store to the service collection with an existing Firestore database.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="dbProvider">A factory function that provides the Firestore database.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddFirestoreOutboxStore(
		this IServiceCollection services,
		Func<IServiceProvider, FirestoreDb> dbProvider,
		Action<FirestoreOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(dbProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		services.TryAddSingleton(sp =>
		{
			var db = dbProvider(sp);
			var options = sp.GetRequiredService<IOptions<FirestoreOutboxOptions>>();
			var logger = sp.GetRequiredService<ILogger<FirestoreOutboxStore>>();
			return new FirestoreOutboxStore(db, options, logger);
		});
		services.TryAddSingleton<IOutboxStore>(sp => sp.GetRequiredService<FirestoreOutboxStore>());

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use Firestore outbox store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseFirestoreOutboxStore(
		this IDispatchBuilder builder,
		Action<FirestoreOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddFirestoreOutboxStore(configure);

		return builder;
	}

	/// <summary>
	/// Configures the dispatch builder to use Firestore outbox store with project ID.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="projectId">The Google Cloud project ID.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseFirestoreOutboxStore(
		this IDispatchBuilder builder,
		string projectId)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

		return builder.UseFirestoreOutboxStore(options =>
		{
			options.ProjectId = projectId;
		});
	}

	/// <summary>
	/// Configures the dispatch builder to use Firestore outbox store with an existing database.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="dbProvider">A factory function that provides the Firestore database.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseFirestoreOutboxStore(
		this IDispatchBuilder builder,
		Func<IServiceProvider, FirestoreDb> dbProvider,
		Action<FirestoreOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(dbProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddFirestoreOutboxStore(dbProvider, configure);

		return builder;
	}
}
