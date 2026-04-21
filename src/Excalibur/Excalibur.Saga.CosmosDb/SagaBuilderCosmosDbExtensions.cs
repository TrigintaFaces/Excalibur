// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Saga.DependencyInjection;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.CosmosDb;

/// <summary>
/// Extension methods for configuring Cosmos DB saga stores on <see cref="ISagaBuilder"/>.
/// </summary>
public static class SagaBuilderCosmosDbExtensions
{
	/// <summary>
	/// Configures the saga builder to use Azure Cosmos DB for saga state storage.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configure">Configuration action for the CosmosDb saga builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddSagas(saga =&gt;
	/// {
	///     saga.UseCosmosDb(cosmos =&gt;
	///     {
	///         cosmos.ConnectionString(connectionString)
	///               .DatabaseName("myapp")
	///               .ContainerName("sagas");
	///     });
	/// }));
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static ISagaBuilder UseCosmosDb(
		this ISagaBuilder builder,
		Action<ICosmosDbSagaBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new CosmosDbSagaOptions();
		var cosmosBuilder = new CosmosDbSagaBuilder(options);
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
		ISagaBuilder builder,
		CosmosDbSagaBuilder cosmosBuilder,
		CosmosDbSagaOptions options,
		bool hasBuilderConnection)
	{
		// Register store-specific options from builder state
		_ = builder.Services.Configure<CosmosDbSagaOptions>(opt =>
		{
			opt.DatabaseName = options.DatabaseName;
			opt.ContainerName = options.ContainerName;
		});

		// Register BindConfiguration if set
		if (cosmosBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<CosmosDbSagaOptions>()
				.BindConfiguration(cosmosBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		// Register ValidateOnStart
		builder.Services.AddOptions<CosmosDbSagaOptions>().ValidateOnStart();

		// Register validator
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<CosmosDbSagaOptions>, CosmosDbSagaOptionsValidator>());

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
		}
		else if (cosmosBuilder.ConnectionStringValue is not null)
		{
			var connStr = cosmosBuilder.ConnectionStringValue;
			builder.Services.TryAddSingleton(_ => new CosmosClient(connStr));
		}

		// Register store services
		builder.Services.TryAddSingleton<CosmosDbSagaStore>();
		builder.Services.AddKeyedSingleton<ISagaStore>("cosmosdb", (sp, _) => sp.GetRequiredService<CosmosDbSagaStore>());
		builder.Services.TryAddKeyedSingleton<ISagaStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ISagaStore>("cosmosdb"));
	}

	private static void RegisterBuilderManagedClient(
		IServiceCollection services,
		CosmosDbSagaBuilder cosmosBuilder,
		CosmosDbSagaOptions options)
	{
		// Set sentinel so the store's options validation passes
		options.Client.ConnectionString =
			"AccountEndpoint=https://builder-managed.documents.azure.com:443/;AccountKey=YnVpbGRlci1tYW5hZ2VkLWtleQ==;";

		_ = services.Configure<CosmosDbSagaOptions>(opt =>
		{
			opt.Client.ConnectionString = options.Client.ConnectionString;
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
