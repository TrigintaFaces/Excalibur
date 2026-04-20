// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.Abstractions.CloudNative;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.CosmosDb;

/// <summary>
/// Extension methods for configuring Cosmos DB outbox stores on <see cref="IOutboxBuilder"/>.
/// </summary>
public static class OutboxBuilderCosmosDbExtensions
{
	/// <summary>
	/// Configures the outbox builder to use Azure Cosmos DB for outbox storage.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="configure">Configuration action for the CosmosDb outbox builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddOutbox(outbox =&gt;
	/// {
	///     outbox.UseCosmosDb(cosmos =&gt;
	///     {
	///         cosmos.ConnectionString(connectionString)
	///               .DatabaseName("myapp")
	///               .ContainerName("outbox");
	///     });
	/// }));
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IOutboxBuilder UseCosmosDb(
		this IOutboxBuilder builder,
		Action<ICosmosDbOutboxBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new CosmosDbOutboxOptions();
		var cosmosBuilder = new CosmosDbOutboxBuilder(options);
		configure(cosmosBuilder);

		var hasBuilderConnection = cosmosBuilder.ClientInstance is not null
			|| cosmosBuilder.ClientFactoryFunc is not null;

		RegisterOptionsAndServices(builder, cosmosBuilder, options, hasBuilderConnection);

		return builder;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IOutboxBuilder builder,
		CosmosDbOutboxBuilder cosmosBuilder,
		CosmosDbOutboxOptions options,
		bool hasBuilderConnection)
	{
		// Register store-specific options from builder state
		_ = builder.Services.Configure<CosmosDbOutboxOptions>(opt =>
		{
			opt.DatabaseName = options.DatabaseName;
			opt.ContainerName = options.ContainerName;
		});

		// Register BindConfiguration if set
		if (cosmosBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<CosmosDbOutboxOptions>()
				.BindConfiguration(cosmosBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		// Register ValidateOnStart
		builder.Services.AddOptions<CosmosDbOutboxOptions>().ValidateOnStart();

		// Register validator
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<CosmosDbOutboxOptions>, CosmosDbOutboxOptionsValidator>());

		// Register CosmosClient based on connection path
		if (hasBuilderConnection)
		{
			RegisterBuilderManagedClient(builder.Services, cosmosBuilder, options);
		}
		else if (cosmosBuilder.EndpointValue is not null)
		{
			var endpoint = cosmosBuilder.EndpointValue;
			var authKey = cosmosBuilder.AuthKeyValue!;
			builder.Services.TryAddSingleton(_ => new CosmosClient(endpoint, authKey));

			// Map to options so the store can create its own client
			var endpointCapture = endpoint;
			var authKeyCapture = authKey;
			_ = builder.Services.Configure<CosmosDbOutboxOptions>(opt =>
			{
				opt.Connection.AccountEndpoint = endpointCapture;
				opt.Connection.AccountKey = authKeyCapture;
			});
		}
		else if (cosmosBuilder.ConnectionStringValue is not null)
		{
			var connStr = cosmosBuilder.ConnectionStringValue;
			builder.Services.TryAddSingleton(_ => new CosmosClient(connStr));

			// Map to options so the store can create its own client
			_ = builder.Services.Configure<CosmosDbOutboxOptions>(opt =>
			{
				opt.Connection.ConnectionString = connStr;
			});
		}

		// Register store services
		builder.Services.TryAddSingleton<CosmosDbOutboxStore>();
		builder.Services.TryAddSingleton<ICloudNativeOutboxStore>(sp => sp.GetRequiredService<CosmosDbOutboxStore>());
	}

	private static void RegisterBuilderManagedClient(
		IServiceCollection services,
		CosmosDbOutboxBuilder cosmosBuilder,
		CosmosDbOutboxOptions options)
	{
		// Set sentinel so the store's options validation passes
		const string sentinel =
			"AccountEndpoint=https://builder-managed.documents.azure.com:443/;AccountKey=YnVpbGRlci1tYW5hZ2VkLWtleQ==;";

		options.Connection.ConnectionString = sentinel;

		_ = services.Configure<CosmosDbOutboxOptions>(opt =>
		{
			opt.Connection.ConnectionString = sentinel;
		});

		if (cosmosBuilder.ClientInstance is not null)
		{
			var client = cosmosBuilder.ClientInstance;
			services.TryAddSingleton(client);
		}
		else if (cosmosBuilder.ClientFactoryFunc is not null)
		{
			var factory = cosmosBuilder.ClientFactoryFunc;
			services.TryAddSingleton(factory);
		}
	}
}
