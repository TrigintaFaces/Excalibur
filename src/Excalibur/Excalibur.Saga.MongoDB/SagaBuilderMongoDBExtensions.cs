// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.DependencyInjection;
using Excalibur.Saga.MongoDB;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring MongoDB saga stores on <see cref="ISagaBuilder"/>.
/// </summary>
public static class SagaBuilderMongoDbExtensions
{
	private const string BuilderManagedConnectionSentinel = "mongodb://builder-managed-client";

	/// <summary>
	/// Configures the saga builder to use MongoDB for saga state storage.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configure">Action to configure MongoDB saga settings via the fluent builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddSagas(saga =&gt;
	/// {
	///     saga.UseMongoDB(mongo =&gt;
	///     {
	///         mongo.ConnectionString("mongodb://localhost:27017")
	///              .DatabaseName("myapp")
	///              .CollectionName("sagas");
	///     });
	/// }));
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static ISagaBuilder UseMongoDB(
		this ISagaBuilder builder,
		Action<IMongoDBSagaBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new MongoDbSagaOptions();
		var mongoBuilder = new MongoDBSagaBuilder(options);
		configure(mongoBuilder);

		var hasBuilderConnection = mongoBuilder.ClientInstance is not null
			|| mongoBuilder.ClientFactoryFunc is not null;

		if (hasBuilderConnection)
		{
			options.ConnectionString = BuilderManagedConnectionSentinel;
		}

		RegisterOptionsAndServices(builder.Services, mongoBuilder, options, hasBuilderConnection);

		return builder;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IServiceCollection services,
		MongoDBSagaBuilder mongoBuilder,
		MongoDbSagaOptions options,
		bool hasBuilderConnection)
	{
		_ = services.Configure<MongoDbSagaOptions>(opt =>
		{
			opt.ConnectionString = options.ConnectionString;
			opt.DatabaseName = options.DatabaseName;
			opt.CollectionName = options.CollectionName;
		});

		if (mongoBuilder.BindConfigurationPath is not null)
		{
			services.AddOptions<MongoDbSagaOptions>()
				.BindConfiguration(mongoBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MongoDbSagaOptions>, MongoDbSagaOptionsValidator>());
		services.AddOptions<MongoDbSagaOptions>().ValidateOnStart();

		if (hasBuilderConnection)
		{
			RegisterClientAndStore(services, mongoBuilder);
		}
		else
		{
			services.TryAddSingleton<MongoDbSagaStore>();
			services.AddKeyedSingleton<ISagaStore>("mongodb", (sp, _) => sp.GetRequiredService<MongoDbSagaStore>());
			services.TryAddKeyedSingleton<ISagaStore>("default", (sp, _) =>
				sp.GetRequiredKeyedService<ISagaStore>("mongodb"));
		}
	}

	private static void RegisterClientAndStore(
		IServiceCollection services,
		MongoDBSagaBuilder mongoBuilder)
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

		services.TryAddSingleton(sp =>
		{
			var client = sp.GetRequiredService<IMongoClient>();
			var opts = sp.GetRequiredService<IOptions<MongoDbSagaOptions>>();
			var logger = sp.GetRequiredService<ILogger<MongoDbSagaStore>>();
			var serializer = sp.GetRequiredService<DispatchJsonSerializer>();
			return new MongoDbSagaStore(client, opts, logger, serializer);
		});
		services.AddKeyedSingleton<ISagaStore>("mongodb", (sp, _) => sp.GetRequiredService<MongoDbSagaStore>());
		services.TryAddKeyedSingleton<ISagaStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ISagaStore>("mongodb"));
	}
}
