// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Outbox;
using Excalibur.Outbox.MongoDB;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring MongoDB outbox stores on <see cref="IOutboxBuilder"/>.
/// </summary>
public static class OutboxBuilderMongoDbExtensions
{
	private const string BuilderManagedConnectionSentinel = "mongodb://builder-managed-client";

	/// <summary>
	/// Configures the outbox builder to use MongoDB for outbox storage.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="configure">Action to configure MongoDB outbox settings via the fluent builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddOutbox(outbox =&gt;
	/// {
	///     outbox.UseMongoDB(mongo =&gt;
	///     {
	///         mongo.ConnectionString("mongodb://localhost:27017")
	///              .DatabaseName("myapp");
	///     });
	/// }));
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IOutboxBuilder UseMongoDB(
		this IOutboxBuilder builder,
		Action<IMongoDBOutboxBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new MongoDbOutboxOptions();
		var mongoBuilder = new MongoDBOutboxBuilder(options);
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
		MongoDBOutboxBuilder mongoBuilder,
		MongoDbOutboxOptions options,
		bool hasBuilderConnection)
	{
		_ = services.Configure<MongoDbOutboxOptions>(opt =>
		{
			opt.ConnectionString = options.ConnectionString;
			opt.DatabaseName = options.DatabaseName;
			opt.CollectionName = options.CollectionName;
		});

		if (mongoBuilder.BindConfigurationPath is not null)
		{
			services.AddOptions<MongoDbOutboxOptions>()
				.BindConfiguration(mongoBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MongoDbOutboxOptions>, MongoDbOutboxOptionsValidator>());
		services.AddOptions<MongoDbOutboxOptions>().ValidateOnStart();

		if (hasBuilderConnection)
		{
			RegisterClientAndStore(services, mongoBuilder);
		}
		else
		{
			services.TryAddSingleton<MongoDbOutboxStore>();
			services.AddKeyedSingleton<IOutboxStore>("mongodb", (sp, _) => sp.GetRequiredService<MongoDbOutboxStore>());
			services.TryAddKeyedSingleton<IOutboxStore>("default", (sp, _) =>
				sp.GetRequiredKeyedService<IOutboxStore>("mongodb"));
		}
	}

	private static void RegisterClientAndStore(
		IServiceCollection services,
		MongoDBOutboxBuilder mongoBuilder)
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
			var opts = sp.GetRequiredService<IOptions<MongoDbOutboxOptions>>();
			var logger = sp.GetRequiredService<ILogger<MongoDbOutboxStore>>();
			return new MongoDbOutboxStore(client, opts, logger);
		});
		services.AddKeyedSingleton<IOutboxStore>("mongodb", (sp, _) => sp.GetRequiredService<MongoDbOutboxStore>());
		services.TryAddKeyedSingleton<IOutboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IOutboxStore>("mongodb"));
	}
}
