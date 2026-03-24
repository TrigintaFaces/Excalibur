// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Inbox.Firestore;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;

using Google.Cloud.Firestore;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Firestore inbox store.
/// </summary>
public static class FirestoreInboxExtensions
{
	/// <summary>
	/// Adds Firestore inbox store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddFirestoreInboxStore(
		this IServiceCollection services,
		Action<FirestoreInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		services.TryAddSingleton<FirestoreInboxStore>();
		services.AddKeyedSingleton<IInboxStore>("firestore", (sp, _) => sp.GetRequiredService<FirestoreInboxStore>());
		services.TryAddKeyedSingleton<IInboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IInboxStore>("firestore"));

		return services;
	}

	/// <summary>
	/// Adds Firestore inbox store to the service collection with an existing Firestore database.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="dbProvider">A factory function that provides the Firestore database.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddFirestoreInboxStore(
		this IServiceCollection services,
		Func<IServiceProvider, FirestoreDb> dbProvider,
		Action<FirestoreInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(dbProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		services.TryAddSingleton(sp =>
		{
			var db = dbProvider(sp);
			var options = sp.GetRequiredService<IOptions<FirestoreInboxOptions>>();
			var logger = sp.GetRequiredService<ILogger<FirestoreInboxStore>>();
			return new FirestoreInboxStore(db, options, logger);
		});
		services.AddKeyedSingleton<IInboxStore>("firestore", (sp, _) => sp.GetRequiredService<FirestoreInboxStore>());
		services.TryAddKeyedSingleton<IInboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IInboxStore>("firestore"));

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use Firestore inbox store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseFirestoreInboxStore(
		this IDispatchBuilder builder,
		Action<FirestoreInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddFirestoreInboxStore(configure);

		return builder;
	}

	/// <summary>
	/// Configures the dispatch builder to use Firestore inbox store with an existing database.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="dbProvider">A factory function that provides the Firestore database.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseFirestoreInboxStore(
		this IDispatchBuilder builder,
		Func<IServiceProvider, FirestoreDb> dbProvider,
		Action<FirestoreInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(dbProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddFirestoreInboxStore(dbProvider, configure);

		return builder;
	}
}
