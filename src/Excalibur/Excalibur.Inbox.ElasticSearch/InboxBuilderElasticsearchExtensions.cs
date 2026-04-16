// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Elastic.Clients.Elasticsearch;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.ElasticSearch;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Elasticsearch provider on <see cref="IInboxBuilder"/>.
/// </summary>
public static class InboxBuilderElasticsearchExtensions
{
	/// <summary>
	/// Configures the inbox to use Elasticsearch storage via the fluent builder.
	/// </summary>
	/// <param name="builder">The inbox builder.</param>
	/// <param name="configure">Configuration action for the Elasticsearch inbox builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburInbox(inbox =&gt;
	/// {
	///     inbox.UseElasticSearch(es =&gt;
	///     {
	///         es.NodeUri(new Uri("http://localhost:9200"))
	///           .IndexName("my-inbox");
	///     });
	/// });
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IInboxBuilder UseElasticSearch(
		this IInboxBuilder builder,
		Action<IElasticSearchInboxBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new ElasticsearchInboxOptions();
		var esBuilder = new ElasticSearchInboxBuilder(options);
		configure(esBuilder);

		RegisterOptionsAndServices(builder, esBuilder, options);

		return builder;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IInboxBuilder builder,
		ElasticSearchInboxBuilder esBuilder,
		ElasticsearchInboxOptions options)
	{
		// Register store-specific options from builder state
		_ = builder.Services.Configure<ElasticsearchInboxOptions>(opt =>
		{
			opt.IndexName = options.IndexName;
			opt.RefreshPolicy = options.RefreshPolicy;
			opt.RetentionDays = options.RetentionDays;
		});

		// Register BindConfiguration if set
		if (esBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<ElasticsearchInboxOptions>()
				.BindConfiguration(esBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		// Register ValidateOnStart
		builder.Services.AddOptions<ElasticsearchInboxOptions>().ValidateOnStart();

		// Register validator
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ElasticsearchInboxOptions>, ElasticsearchInboxOptionsValidator>());

		// Register ElasticsearchClient based on connection path
		RegisterClientFromBuilder(builder.Services, esBuilder);

		// Register store services
		builder.Services.TryAddSingleton<ElasticsearchInboxStore>();
		builder.Services.AddKeyedSingleton<IInboxStore>("elasticsearch", (sp, _) => sp.GetRequiredService<ElasticsearchInboxStore>());
		builder.Services.TryAddKeyedSingleton<IInboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IInboxStore>("elasticsearch"));
	}

	private static void RegisterClientFromBuilder(
		IServiceCollection services,
		ElasticSearchInboxBuilder esBuilder)
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
