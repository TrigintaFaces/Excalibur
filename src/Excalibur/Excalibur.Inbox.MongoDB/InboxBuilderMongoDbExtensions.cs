// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.MongoDB;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring MongoDB provider on <see cref="IInboxBuilder"/>.
/// </summary>
public static class InboxBuilderMongoDbExtensions
{
	// internal, not private: MongoDbInboxTopologyHealthCheck (same assembly) reports healthy without a
	// connectivity round trip against this sentinel -- a builder-supplied client has no real connection
	// string to probe, and its topology is the consumer's to guarantee.
	internal const string BuilderManagedConnectionSentinel = "mongodb://builder-managed-client";

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
		// Reachability is NOT options validation. Options validation answers whether the configuration is
		// coherent -- no network -- and its failures rightly stop the host. Whether the server is a replica
		// set can only be answered by a round trip, and a server that is merely not accepting connections
		// yet is the normal case under an orchestrator that does not order the database ahead of the app.
		// Deciding it here turned that into a permanent startup failure, so it is a health check feeding a
		// readiness probe that retries instead.
		_ = services.AddHealthChecks().Add(new HealthCheckRegistration(
			"mongodb-inbox-topology",
			sp => new MongoDbInboxTopologyHealthCheck(
				sp.GetRequiredService<IOptionsMonitor<MongoDbInboxOptions>>()),
			failureStatus: HealthStatus.Unhealthy,
			tags: ["ready", "mongodb", "inbox"]));
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
			// AddTenantAwareStore builds the store injecting ITenantContext (so the dedup _id + every keyed
			// read/claim scope per tenant, since this store's constructor declares one) AND emits the
			// ITenantScopingCapability<IInboxStore> marker inseparably from that wiring (an
			// unwired provider can't carry a truthful marker).
			services.AddTenantAwareStore<IInboxStore, MongoDbInboxStore>(sp =>
				new MongoDbInboxStore(
					sp.GetRequiredService<IOptions<MongoDbInboxOptions>>(),
					sp.GetRequiredService<ILogger<MongoDbInboxStore>>(),
					sp.GetRequiredService<ITenantContext>()));
			services.AddKeyedSingleton<IInboxStore>("mongodb", (sp, _) => sp.GetRequiredService<MongoDbInboxStore>());
			services.TryAddKeyedSingleton<IInboxStore>("default", (sp, _) =>
				sp.GetRequiredKeyedService<IInboxStore>("mongodb"));
		}
	}

	private static void RegisterClientAndStore(
		IServiceCollection services,
		MongoDBInboxBuilder mongoBuilder)
	{
		// Self-sufficient rather than order-dependent: this method resolves ITenantContext as a REQUIRED
		// service, so it wires the default itself instead of relying on a sibling registration having run
		// first. TryAdd makes it idempotent, and a consumer's own context still wins.
		_ = services.AddDefaultTenantContext();

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

		// AddTenantAwareStore builds the store injecting ITenantContext (so the dedup _id + keyed reads scope
		// per tenant, since this store's constructor declares one) AND emits the
		// ITenantScopingCapability<IInboxStore> marker inseparably.
		services.AddTenantAwareStore<IInboxStore, MongoDbInboxStore>(sp =>
			new MongoDbInboxStore(
				sp.GetRequiredService<IMongoClient>(),
				sp.GetRequiredService<IOptions<MongoDbInboxOptions>>(),
				sp.GetRequiredService<ILogger<MongoDbInboxStore>>(),
				sp.GetRequiredService<ITenantContext>()));
		services.AddKeyedSingleton<IInboxStore>("mongodb", (sp, _) => sp.GetRequiredService<MongoDbInboxStore>());
		services.TryAddKeyedSingleton<IInboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IInboxStore>("mongodb"));
	}
}
