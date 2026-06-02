// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.MongoDB;
using Excalibur.Data.Persistence;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring MongoDB persistence services.
/// </summary>
public static class MongoDbServiceCollectionExtensions
{
	private const string BuilderManagedConnectionSentinel = "mongodb://builder-managed-client";

	/// <summary>
	/// Adds MongoDB persistence services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure MongoDB data settings via the fluent builder.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburMongoDb(mongo =&gt;
	/// {
	///     mongo.ConnectionString("mongodb://localhost:27017")
	///          .DatabaseName("myapp");
	/// });
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IServiceCollection AddExcaliburMongoDb(
		this IServiceCollection services,
		Action<IMongoDBDataBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new MongoDbProviderOptions();
		var mongoBuilder = new MongoDBDataBuilder(options);
		configure(mongoBuilder);

		var hasBuilderConnection = mongoBuilder.ClientInstance is not null
			|| mongoBuilder.ClientFactoryFunc is not null;

		if (hasBuilderConnection)
		{
			options.ConnectionString = BuilderManagedConnectionSentinel;
		}

		RegisterOptionsAndServices(services, mongoBuilder, options, hasBuilderConnection);

		return services;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IServiceCollection services,
		MongoDBDataBuilder mongoBuilder,
		MongoDbProviderOptions options,
		bool hasBuilderConnection)
	{
		_ = services.Configure<MongoDbProviderOptions>(opt =>
		{
			opt.ConnectionString = options.ConnectionString;
			opt.DatabaseName = options.DatabaseName;
		});

		if (mongoBuilder.BindConfigurationPath is not null)
		{
			services.AddOptions<MongoDbProviderOptions>()
				.BindConfiguration(mongoBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MongoDbProviderOptions>, MongoDbProviderOptionsValidator>());
		services.AddOptions<MongoDbProviderOptions>().ValidateOnStart();

		if (hasBuilderConnection)
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
		}

		services.TryAddSingleton<MongoDbPersistenceProvider>();
		services.AddKeyedSingleton<IPersistenceProvider>("mongodb",
			(sp, _) => sp.GetRequiredService<MongoDbPersistenceProvider>());
		services.TryAddKeyedSingleton<IPersistenceProvider>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IPersistenceProvider>("mongodb"));
	}
}
