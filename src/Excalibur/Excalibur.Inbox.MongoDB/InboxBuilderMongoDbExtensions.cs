// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.MongoDB;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring MongoDB provider on <see cref="IInboxBuilder"/>.
/// </summary>
public static class InboxBuilderMongoDbExtensions
{
	private const string BuilderManagedConnectionSentinel = "mongodb://builder-managed-client";

	/// <summary>
	/// Configures the inbox to use MongoDB storage.
	/// </summary>
	/// <param name="builder">The inbox builder.</param>
	/// <param name="configure">Action to configure MongoDB inbox settings via the fluent builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburInbox(inbox =&gt;
	/// {
	///     inbox.UseMongoDB(mongo =&gt;
	///     {
	///         mongo.ConnectionString("mongodb://localhost:27017")
	///              .DatabaseName("myapp");
	///     });
	/// });
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IInboxBuilder UseMongoDB(
		this IInboxBuilder builder,
		Action<IMongoDBInboxBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new MongoDbInboxOptions();
		var mongoBuilder = new MongoDBInboxBuilder(options);
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
		MongoDBInboxBuilder mongoBuilder,
		MongoDbInboxOptions options,
		bool hasBuilderConnection)
	{
		_ = services.Configure<MongoDbInboxOptions>(opt =>
		{
			opt.ConnectionString = options.ConnectionString;
			opt.DatabaseName = options.DatabaseName;
			opt.CollectionName = options.CollectionName;
		});

		if (mongoBuilder.BindConfigurationPath is not null)
		{
			services.AddOptions<MongoDbInboxOptions>()
				.BindConfiguration(mongoBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MongoDbInboxOptions>, MongoDbInboxOptionsValidator>());
		services.AddOptions<MongoDbInboxOptions>().ValidateOnStart();

		if (hasBuilderConnection)
		{
			RegisterClientAndStore(services, mongoBuilder);
		}
		else
		{
			services.TryAddSingleton<MongoDbInboxStore>();
			services.AddKeyedSingleton<IInboxStore>("mongodb", (sp, _) => sp.GetRequiredService<MongoDbInboxStore>());
			services.TryAddKeyedSingleton<IInboxStore>("default", (sp, _) =>
				sp.GetRequiredKeyedService<IInboxStore>("mongodb"));
		}
	}

	private static void RegisterClientAndStore(
		IServiceCollection services,
		MongoDBInboxBuilder mongoBuilder)
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
			var opts = sp.GetRequiredService<IOptions<MongoDbInboxOptions>>();
			var logger = sp.GetRequiredService<ILogger<MongoDbInboxStore>>();
			return new MongoDbInboxStore(client, opts, logger);
		});
		services.AddKeyedSingleton<IInboxStore>("mongodb", (sp, _) => sp.GetRequiredService<MongoDbInboxStore>());
		services.TryAddKeyedSingleton<IInboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IInboxStore>("mongodb"));
	}
}
