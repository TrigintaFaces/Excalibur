// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
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

		// Fail-closed single-tenant default guarantees a non-null ITenantContext for tenant scoping; the
		// multi-tenancy composition replaces it with the ambient context.
		services.AddDefaultTenantContext();

		if (hasBuilderConnection)
		{
			RegisterClientAndStore(services, mongoBuilder);
		}
		else
		{
			// AddTenantScopedStore builds the store injecting ITenantContext (so the dedup _id + every keyed
			// read/claim scope per tenant) AND emits the ITenantScopingCapability<IInboxStore> marker
			// inseparably from that wiring (S886 rw2ull — an unwired provider can't carry a truthful marker).
			services.AddTenantScopedStore<IInboxStore, MongoDbInboxStore>((sp, tenantContext) =>
				new MongoDbInboxStore(
					sp.GetRequiredService<IOptions<MongoDbInboxOptions>>(),
					sp.GetRequiredService<ILogger<MongoDbInboxStore>>(),
					tenantContext));
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

		// AddTenantScopedStore builds the store injecting ITenantContext (so the dedup _id + keyed reads scope
		// per tenant) AND emits the ITenantScopingCapability<IInboxStore> marker inseparably (S886 rw2ull).
		services.AddTenantScopedStore<IInboxStore, MongoDbInboxStore>((sp, tenantContext) =>
			new MongoDbInboxStore(
				sp.GetRequiredService<IMongoClient>(),
				sp.GetRequiredService<IOptions<MongoDbInboxOptions>>(),
				sp.GetRequiredService<ILogger<MongoDbInboxStore>>(),
				tenantContext));
		services.AddKeyedSingleton<IInboxStore>("mongodb", (sp, _) => sp.GetRequiredService<MongoDbInboxStore>());
		services.TryAddKeyedSingleton<IInboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IInboxStore>("mongodb"));
	}
}
