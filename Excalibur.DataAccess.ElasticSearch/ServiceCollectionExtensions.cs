// Copyright (c) 2025 The Excalibur Project Authors
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in
// the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on
// an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.DataAccess.ElasticSearch;

/// <summary>
///   Provides extension methods for configuring Elasticsearch-related services in the application's dependency
///   injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	///   Registers the Elasticsearch client and related services with the dependency injection container.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to which services will be added. </param>
	/// <param name="configuration">
	///   The application's <see cref="IConfiguration" /> containing the Elasticsearch configuration settings.
	/// </param>
	/// <param name="registry"> A delegate to register additional services related to Elasticsearch. </param>
	/// <param name="configureSettings">
	///   An optional delegate to further configure the <see cref="ElasticsearchClientSettings" /> before creating the client.
	/// </param>
	/// <returns> The updated <see cref="IServiceCollection" /> with Elasticsearch services registered. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="configuration" /> is <c> null </c>. </exception>
	public static IServiceCollection AddElasticsearchServices(
		this IServiceCollection services,
		IConfiguration configuration,
		Action<IServiceCollection> registry,
		Action<ElasticsearchClientSettings>? configureSettings = null)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.Configure<ElasticsearchConfigurationSettings>(configuration.GetSection("ElasticSearch"));

		_ = services.AddSingleton(
			(IServiceProvider sp) =>
			{
				var elasticConfig = sp.GetRequiredService<IOptions<ElasticsearchConfigurationSettings>>().Value;

				Uri[] uris;
				if (elasticConfig.Urls is { Length: > 0 })
				{
					uris = Array.ConvertAll(elasticConfig.Urls, u => new Uri(u));
				}
				else if (!string.IsNullOrWhiteSpace(elasticConfig.Url))
				{
					uris = [new Uri(elasticConfig.Url)];
				}
				else
				{
					throw new InvalidOperationException("No Elasticsearch Urls configured.");
				}

				var pool = new StaticNodePool(uris);
				var settings = new ElasticsearchClientSettings(pool);

				if (!string.IsNullOrWhiteSpace(elasticConfig.CertificateFingerprint))
				{
					_ = settings.CertificateFingerprint(elasticConfig.CertificateFingerprint);
				}

				if (!string.IsNullOrWhiteSpace(elasticConfig.ApiKey))
				{
					_ = settings.Authentication(new ApiKey(elasticConfig.ApiKey));
				}
				else if (!string.IsNullOrWhiteSpace(elasticConfig.Username) && !string.IsNullOrWhiteSpace(elasticConfig.Password))
				{
					_ = settings.Authentication(new BasicAuthentication(elasticConfig.Username, elasticConfig.Password));
				}

				configureSettings?.Invoke(settings);

				return new ElasticsearchClient(settings);
			});

		_ = services.AddScoped<IndexInitializer>();

		registry?.Invoke(services);

		return services;
	}

	/// <summary>
	///   Registers an Elasticsearch repository in the dependency injection container.
	/// </summary>
	/// <typeparam name="TRepositoryInterface"> The interface type of the repository. </typeparam>
	/// <typeparam name="TRepository">
	///   The concrete implementation type of the repository, which also implements <see cref="IInitializeElasticIndex" />.
	/// </typeparam>
	/// <param name="services"> The <see cref="IServiceCollection" /> to which the repository will be added. </param>
	/// <returns> The updated <see cref="IServiceCollection" /> with the repository registered. </returns>
	public static IServiceCollection AddRepository<TRepositoryInterface, TRepository>(this IServiceCollection services)
		where TRepository : class, TRepositoryInterface, IInitializeElasticIndex where TRepositoryInterface : class
	{
		_ = services.AddScoped<TRepositoryInterface, TRepository>();
		_ = services.AddScoped<IInitializeElasticIndex>(sp => (IInitializeElasticIndex)sp.GetRequiredService<TRepositoryInterface>());

		return services;
	}
}
