// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Elastic.Clients.Elasticsearch;

using Excalibur.Dispatch;
using Excalibur.Outbox;
using Excalibur.Outbox.ElasticSearch;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Elasticsearch provider on <see cref="IOutboxBuilder"/>.
/// </summary>
public static class OutboxBuilderElasticsearchExtensions
{
	/// <summary>
	/// Configures the outbox to use Elasticsearch storage via the fluent builder.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="configure">Configuration action for the Elasticsearch outbox builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddOutbox(outbox =&gt;
	/// {
	///     outbox.UseElasticSearch(es =&gt;
	///     {
	///         es.NodeUri(new Uri("http://localhost:9200"))
	///           .IndexName("my-outbox");
	///     });
	/// }));
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IOutboxBuilder UseElasticSearch(
		this IOutboxBuilder builder,
		Action<IElasticSearchOutboxBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new ElasticsearchOutboxOptions();
		var esBuilder = new ElasticSearchOutboxBuilder(options);
		configure(esBuilder);

		RegisterOptionsAndServices(builder, esBuilder, options);

		return builder;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IOutboxBuilder builder,
		ElasticSearchOutboxBuilder esBuilder,
		ElasticsearchOutboxOptions options)
	{
		// Register store-specific options from builder state
		_ = builder.Services.Configure<ElasticsearchOutboxOptions>(opt =>
		{
			opt.IndexName = options.IndexName;
			opt.RefreshPolicy = options.RefreshPolicy;
			opt.DefaultBatchSize = options.DefaultBatchSize;
			opt.SentMessageRetentionDays = options.SentMessageRetentionDays;
		});

		// Register BindConfiguration if set
		if (esBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<ElasticsearchOutboxOptions>()
				.BindConfiguration(esBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		// Register ValidateOnStart
		builder.Services.AddOptions<ElasticsearchOutboxOptions>().ValidateOnStart();

		// Register validator
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ElasticsearchOutboxOptions>, ElasticsearchOutboxOptionsValidator>());

		// Register ElasticsearchClient based on connection path
		RegisterClientFromBuilder(builder.Services, esBuilder);

		// Register store services
		builder.Services.TryAddSingleton<ElasticsearchOutboxStore>();
		builder.Services.AddKeyedSingleton<IOutboxStore>("elasticsearch", (sp, _) => sp.GetRequiredService<ElasticsearchOutboxStore>());
		builder.Services.TryAddKeyedSingleton<IOutboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IOutboxStore>("elasticsearch"));
		builder.Services.AddKeyedSingleton<IOutboxStoreAdmin>("elasticsearch", (sp, _) => sp.GetRequiredService<ElasticsearchOutboxStore>());
		builder.Services.TryAddKeyedSingleton<IOutboxStoreAdmin>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IOutboxStoreAdmin>("elasticsearch"));
	}

	private static void RegisterClientFromBuilder(
		IServiceCollection services,
		ElasticSearchOutboxBuilder esBuilder)
	{
		if (esBuilder.ClientInstance is not null)
		{
			var client = esBuilder.ClientInstance;
			services.TryAddSingleton(client);
		}
		else if (esBuilder.ClientFactoryFunc is not null)
		{
			var factory = esBuilder.ClientFactoryFunc;
			services.TryAddSingleton(factory);
		}
		else if (esBuilder.CloudIdValue is not null)
		{
			var cloudId = esBuilder.CloudIdValue;
			services.TryAddSingleton(_ => new ElasticsearchClient(new ElasticsearchClientSettings(new Uri(cloudId))));
		}
		else if (esBuilder.NodeUrisValue is not null)
		{
			var uris = esBuilder.NodeUrisValue;
			services.TryAddSingleton(_ =>
			{
				var pool = new Elastic.Transport.StaticNodePool(uris);
				return new ElasticsearchClient(new ElasticsearchClientSettings(pool));
			});
		}
		else if (esBuilder.NodeUriValue is not null)
		{
			var uri = esBuilder.NodeUriValue;
			services.TryAddSingleton(_ => new ElasticsearchClient(new ElasticsearchClientSettings(uri)));
		}
		// If no connection configured, the store will use whatever ElasticsearchClient
		// is already registered in DI (e.g., from the Data package).
	}
}
