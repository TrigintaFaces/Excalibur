// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using Excalibur.EventSourcing.MongoDB;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering MongoDB event store services.
/// </summary>
public static class MongoDbEventStoreExtensions
{
	/// <summary>
	/// Adds the MongoDB event store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure event store options.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddMongoDbEventStore(
		this IServiceCollection services,
		Action<MongoDbEventStoreOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		// Configure options
		_ = services.AddOptions<MongoDbEventStoreOptions>()
			.Configure(configureOptions)
			.ValidateOnStart();

		// Register event store
		services.TryAddScoped<IEventStore>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<MongoDbEventStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<MongoDbEventStore>>();
			var internalSerializer = sp.GetService<ISerializer>();
			var payloadSerializer = sp.GetService<IPayloadSerializer>();

			return new MongoDbEventStore(
				options,
				logger,
				internalSerializer,
				payloadSerializer);
		});

		return services;
	}

	/// <summary>
	/// Adds the MongoDB event store to the service collection with an existing client.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="clientFactory">Factory function that provides a MongoDB client.</param>
	/// <param name="configureOptions">Action to configure event store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Use this overload for advanced scenarios like shared client instances,
	/// custom connection pooling, or integration with existing MongoDB infrastructure.
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddMongoDbEventStore(
		this IServiceCollection services,
		Func<IServiceProvider, IMongoClient> clientFactory,
		Action<MongoDbEventStoreOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(clientFactory);
		ArgumentNullException.ThrowIfNull(configureOptions);

		// Configure options
		_ = services.AddOptions<MongoDbEventStoreOptions>()
			.Configure(configureOptions)
			.ValidateOnStart();

		// Register event store with client factory
		services.TryAddScoped<IEventStore>(sp =>
		{
			var client = clientFactory(sp);
			var options = sp.GetRequiredService<IOptions<MongoDbEventStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<MongoDbEventStore>>();
			var internalSerializer = sp.GetService<ISerializer>();
			var payloadSerializer = sp.GetService<IPayloadSerializer>();

			return new MongoDbEventStore(
				client,
				options,
				logger,
				internalSerializer,
				payloadSerializer);
		});

		return services;
	}
}
