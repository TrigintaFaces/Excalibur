// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Amazon.DynamoDBv2;

using Excalibur.Dispatch;
using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.DynamoDb;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring DynamoDB provider on <see cref="IInboxBuilder"/>.
/// </summary>
public static class InboxBuilderDynamoDbExtensions
{
	/// <summary>
	/// Configures the inbox to use Amazon DynamoDB storage.
	/// </summary>
	/// <param name="builder">The inbox builder.</param>
	/// <param name="configure">Configuration action for the DynamoDB inbox builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburInbox(inbox =&gt;
	/// {
	///     inbox.UseDynamoDb(dynamo =&gt;
	///     {
	///         dynamo.ServiceUrl("http://localhost:8000")
	///               .TableName("inbox_messages");
	///     });
	/// });
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IInboxBuilder UseDynamoDb(
		this IInboxBuilder builder,
		Action<IDynamoDBInboxBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var dynamoBuilder = new DynamoDBInboxBuilder();
		configure(dynamoBuilder);

		var hasBuilderClient = dynamoBuilder.ClientInstance is not null
			|| dynamoBuilder.ClientFactoryFunc is not null;

		RegisterOptionsAndServices(builder, dynamoBuilder, hasBuilderClient);

		return builder;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IInboxBuilder builder,
		DynamoDBInboxBuilder dynamoBuilder,
		bool hasBuilderClient)
	{
		// Register store-specific options from builder state
		_ = builder.Services.Configure<DynamoDbInboxOptions>(opt =>
		{
			if (dynamoBuilder.TableNameValue is not null)
			{
				opt.TableName = dynamoBuilder.TableNameValue;
			}

			if (dynamoBuilder.ServiceUrlValue is not null)
			{
				opt.Connection.ServiceUrl = dynamoBuilder.ServiceUrlValue;
			}

			if (dynamoBuilder.RegionValue is not null)
			{
				opt.Connection.Region = dynamoBuilder.RegionValue.SystemName;
			}
		});

		// Register BindConfiguration if set
		if (dynamoBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<DynamoDbInboxOptions>()
				.BindConfiguration(dynamoBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		// Register ValidateOnStart
		_ = builder.Services.AddDefaultTenantContext();
		builder.Services.AddOptions<DynamoDbInboxOptions>().ValidateOnStart();

		// Register validator
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DynamoDbInboxOptions>, DynamoDbInboxOptionsValidator>());

		// Register IAmazonDynamoDB based on connection path
		if (hasBuilderClient)
		{
			RegisterBuilderManagedClient(builder.Services, dynamoBuilder);
		}
		else if (dynamoBuilder.ServiceUrlValue is not null)
		{
			var serviceUrl = dynamoBuilder.ServiceUrlValue;
			builder.Services.TryAddSingleton<IAmazonDynamoDB>(_ =>
				new AmazonDynamoDBClient(new AmazonDynamoDBConfig { ServiceURL = serviceUrl }));
		}
		else if (dynamoBuilder.RegionValue is not null)
		{
			var region = dynamoBuilder.RegionValue;
			builder.Services.TryAddSingleton<IAmazonDynamoDB>(_ =>
				new AmazonDynamoDBClient(region));
		}

		// AddTenantAwareStore builds the store injecting ITenantContext (so the dedup sort key scopes per
		// tenant, since this store's constructor declares one) AND emits the ITenantScopingCapability<IInboxStore>
		// marker inseparably from that wiring — an unwired provider cannot carry a truthful marker.
		// Registering the store on its own would leave the capability advertisable while the key stayed
		// (handler_type, message_id).
		_ = builder.Services.AddDefaultTenantContext();
		builder.Services.AddTenantAwareStore<IInboxStore, DynamoDbInboxStore>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<DynamoDbInboxOptions>>();
			var logger = sp.GetRequiredService<ILogger<DynamoDbInboxStore>>();
			var tenantContext = sp.GetRequiredService<ITenantContext>();

			// A consumer-supplied or builder-registered client must still win: constructing the
			// options-only overload here would silently discard it and have the store build its own.
			var client = sp.GetService<IAmazonDynamoDB>();

			return client is null
				? new DynamoDbInboxStore(options, logger, tenantContext)
				: new DynamoDbInboxStore(client, options, logger, tenantContext);
		});
		_ = builder.Services.AddDefaultTenantContext();
		builder.Services.AddKeyedSingleton<IInboxStore>("dynamodb", (sp, _) => sp.GetRequiredService<DynamoDbInboxStore>());
		builder.Services.TryAddKeyedSingleton<IInboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IInboxStore>("dynamodb"));
	}

	private static void RegisterBuilderManagedClient(
		IServiceCollection services,
		DynamoDBInboxBuilder dynamoBuilder)
	{
		if (dynamoBuilder.ClientInstance is not null)
		{
			var client = dynamoBuilder.ClientInstance;
			services.TryAddSingleton(client);
		}
		else if (dynamoBuilder.ClientFactoryFunc is not null)
		{
			var factory = dynamoBuilder.ClientFactoryFunc;
			services.TryAddSingleton(factory);
		}
	}
}
