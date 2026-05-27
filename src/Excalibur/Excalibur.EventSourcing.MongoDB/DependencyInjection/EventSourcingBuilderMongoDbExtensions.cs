// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Serialization;
using Excalibur.EventSourcing.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Excalibur.EventSourcing.MongoDB;

/// <summary>
/// Extension methods for configuring MongoDB event sourcing on <see cref="IEventSourcingBuilder"/>.
/// </summary>
public static class EventSourcingBuilderMongoDbExtensions
{
	/// <summary>
	/// Sentinel connection string used when the builder provides an <see cref="IMongoClient"/> directly.
	/// Passes <see cref="MongoDbEventStoreOptions.Validate"/> without being used for actual connections.
	/// </summary>
	private const string BuilderManagedConnectionSentinel = "mongodb://builder-managed-client";

	/// <summary>
	/// Configures the event sourcing builder to use MongoDB for event storage.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Configuration action for the MongoDB event sourcing builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddEventSourcing(es =&gt;
	/// {
	///     es.UseMongoDB(mongo =&gt;
	///     {
	///         mongo.ConnectionString(configuration.GetConnectionString("MongoDB")!)
	///              .DatabaseName("events");
	///     })
	///     .AddRepository&lt;OrderAggregate, Guid&gt;();
	/// }));
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IEventSourcingBuilder UseMongoDB(
		this IEventSourcingBuilder builder,
		Action<IMongoDBEventSourcingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new MongoDbEventStoreOptions();
		var mongoBuilder = new MongoDBEventSourcingBuilder(options);
		configure(mongoBuilder);

		var hasBuilderConnection = mongoBuilder.ClientInstance is not null
			|| mongoBuilder.ClientFactoryFunc is not null;

		// When builder provides a client, set sentinel so options validation passes.
		if (hasBuilderConnection)
		{
			options.ConnectionString = BuilderManagedConnectionSentinel;
		}

		RegisterOptionsAndServices(builder, mongoBuilder, options, hasBuilderConnection);

		return builder;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IEventSourcingBuilder builder,
		MongoDBEventSourcingBuilder mongoBuilder,
		MongoDbEventStoreOptions options,
		bool hasBuilderConnection)
	{
		// Register options from builder state
		_ = builder.Services.Configure<MongoDbEventStoreOptions>(opt =>
		{
			opt.ConnectionString = options.ConnectionString;
			opt.DatabaseName = options.DatabaseName;
			opt.CollectionName = options.CollectionName;
			opt.CounterCollectionName = options.CounterCollectionName;
		});

		// Register BindConfiguration if set
		if (mongoBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<MongoDbEventStoreOptions>()
				.BindConfiguration(mongoBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		// Register ValidateOnStart + validator
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MongoDbEventStoreOptions>, MongoDbEventStoreOptionsValidator>());
		builder.Services.AddOptions<MongoDbEventStoreOptions>().ValidateOnStart();

		// Register store based on connection path
		if (hasBuilderConnection)
		{
			RegisterClientAndStore(builder.Services, mongoBuilder);
		}
		else
		{
			RegisterStoreFromOptions(builder.Services);
		}
	}

	/// <summary>
	/// Registers <see cref="IMongoClient"/> and the event store using the client-taking constructor.
	/// </summary>
	private static void RegisterClientAndStore(
		IServiceCollection services,
		MongoDBEventSourcingBuilder mongoBuilder)
	{
		if (mongoBuilder.ClientInstance is not null)
		{
			var client = mongoBuilder.ClientInstance;
			services.TryAddSingleton<IMongoClient>(client);
		}
		else if (mongoBuilder.ClientFactoryFunc is not null)
		{
			var factory = mongoBuilder.ClientFactoryFunc;
			services.TryAddSingleton<IMongoClient>(factory);
		}

		services.TryAddScoped<IEventStore>(sp =>
		{
			var client = sp.GetRequiredService<IMongoClient>();
			var opts = sp.GetRequiredService<IOptions<MongoDbEventStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<MongoDbEventStore>>();
			var internalSerializer = sp.GetService<ISerializer>();
			var payloadSerializer = sp.GetService<IPayloadSerializer>();

			return new MongoDbEventStore(
				client,
				opts,
				logger,
				internalSerializer,
				payloadSerializer);
		});
	}

	/// <summary>
	/// Registers the event store using the options-only constructor (creates own client from ConnectionString).
	/// </summary>
	private static void RegisterStoreFromOptions(IServiceCollection services)
	{
		services.TryAddScoped<IEventStore>(sp =>
		{
			var opts = sp.GetRequiredService<IOptions<MongoDbEventStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<MongoDbEventStore>>();
			var internalSerializer = sp.GetService<ISerializer>();
			var payloadSerializer = sp.GetService<IPayloadSerializer>();

			return new MongoDbEventStore(
				opts,
				logger,
				internalSerializer,
				payloadSerializer);
		});
	}
}
