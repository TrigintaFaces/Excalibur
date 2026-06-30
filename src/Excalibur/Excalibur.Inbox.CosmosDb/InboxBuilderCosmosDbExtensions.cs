// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Inbox.DependencyInjection;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Excalibur.Inbox.CosmosDb;

/// <summary>
/// Extension methods for configuring CosmosDB provider on <see cref="IInboxBuilder"/>.
/// </summary>
public static class InboxBuilderCosmosDbExtensions
{
	/// <summary>
	/// Configures the inbox to use Azure Cosmos DB storage.
	/// </summary>
	/// <param name="builder">The inbox builder.</param>
	/// <param name="configure">Configuration action for the CosmosDb inbox builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburInbox(inbox =&gt;
	/// {
	///     inbox.UseCosmosDb(cosmos =&gt;
	///     {
	///         cosmos.ConnectionString(connectionString)
	///               .DatabaseName("myapp")
	///               .ContainerName("inbox");
	///     });
	/// });
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IInboxBuilder UseCosmosDb(
		this IInboxBuilder builder,
		Action<ICosmosDbInboxBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new CosmosDbInboxOptions();
		var cosmosBuilder = new CosmosDbInboxBuilder(options);
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
		IInboxBuilder builder,
		CosmosDbInboxBuilder cosmosBuilder,
		CosmosDbInboxOptions options,
		bool hasBuilderConnection)
	{
		// Register store-specific options from builder state
		_ = builder.Services.Configure<CosmosDbInboxOptions>(opt =>
		{
			opt.DatabaseName = options.DatabaseName;
			opt.ContainerName = options.ContainerName;
		});

		// Register BindConfiguration if set
		if (cosmosBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<CosmosDbInboxOptions>()
				.BindConfiguration(cosmosBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		// Register ValidateOnStart
		builder.Services.AddOptions<CosmosDbInboxOptions>().ValidateOnStart();

		// Register validator
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<CosmosDbInboxOptions>, CosmosDbInboxOptionsValidator>());

		// Register CosmosClient based on connection path
		if (hasBuilderConnection)
		{
			RegisterBuilderManagedClient(builder.Services, cosmosBuilder, options);
		}
		else if (cosmosBuilder.EndpointValue is not null)
		{
			var endpoint = cosmosBuilder.EndpointValue;
			var authKey = cosmosBuilder.AuthKeyValue!;
			builder.Services.TryAddSingleton(_ => new CosmosClient(endpoint, authKey, new CosmosClientOptions { UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase } }));
		}
		else if (cosmosBuilder.ConnectionStringValue is not null)
		{
			var connStr = cosmosBuilder.ConnectionStringValue;
			builder.Services.TryAddSingleton(_ => new CosmosClient(connStr, new CosmosClientOptions { UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase } }));
		}

		// Register store services
		builder.Services.TryAddSingleton<CosmosDbInboxStore>();
		builder.Services.AddKeyedSingleton<IInboxStore>("cosmosdb", (sp, _) => sp.GetRequiredService<CosmosDbInboxStore>());
		builder.Services.TryAddKeyedSingleton<IInboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IInboxStore>("cosmosdb"));
	}

	private static void RegisterBuilderManagedClient(
		IServiceCollection services,
		CosmosDbInboxBuilder cosmosBuilder,
		CosmosDbInboxOptions options)
	{
		// Set sentinel so the store's options validation passes
		options.Client.ConnectionString =
			"AccountEndpoint=https://builder-managed.documents.azure.com:443/;AccountKey=YnVpbGRlci1tYW5hZ2VkLWtleQ==;";

		_ = services.Configure<CosmosDbInboxOptions>(opt =>
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
