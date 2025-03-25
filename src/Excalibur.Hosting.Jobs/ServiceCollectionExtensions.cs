using System.Net;
using System.Reflection;

using Excalibur.Application;
using Excalibur.Core;
using Excalibur.Core.Concurrency;
using Excalibur.Data;
using Excalibur.Jobs;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Quartz;

namespace Excalibur.Hosting.Jobs;

/// <summary>
///     Provides extension methods for configuring services in an <see cref="IServiceCollection" />.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	///     Adds Excalibur Job Host services to the specified service collection.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="configuration"> The application configuration. </param>
	/// <param name="assemblies"> An array of assemblies to scan for services and jobs. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="services" /> is null. </exception>
	public static IServiceCollection AddExcaliburJobHost(this IServiceCollection services, IConfiguration configuration,
		params Assembly[] assemblies)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddExcaliburDataServices(configuration, assemblies);
		_ = services.AddExcaliburApplicationServices(assemblies);
		_ = services.AddTenantId();
		_ = services.AddCorrelationId();
		_ = services.AddETag();
		_ = services.AddClientAddress();

		return services;
	}

	/// <summary>
	///     Adds Quartz services to the specified service collection and registers the Quartz hosted service.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="withJobs"> An optional action to configure Quartz jobs via <see cref="IServiceCollectionQuartzConfigurator" />. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="services" /> is null. </exception>
	public static IServiceCollection AddQuartzWithJobs(this IServiceCollection services,
		Action<IServiceCollectionQuartzConfigurator>? withJobs)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddQuartz(withJobs);
		_ = services.AddQuartzHostedService(config => config.WaitForJobsToComplete = true);

		return services;
	}

	/// <summary>
	///     Adds a job watcher service for monitoring job configuration changes.
	/// </summary>
	/// <typeparam name="TJob"> The type of the job to monitor. </typeparam>
	/// <typeparam name="TConfig"> The type of the job configuration. </typeparam>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="configurationSection"> The configuration section for the job. </param>
	/// <exception cref="ArgumentNullException">
	///     Thrown if <paramref name="services" /> or <paramref name="configurationSection" /> is null.
	/// </exception>
	public static void AddJobWatcher<TJob, TConfig>(this IServiceCollection services, IConfigurationSection configurationSection)
		where TJob : IConfigurableJob<TConfig>
		where TConfig : class, IJobConfig
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configurationSection);

		_ = services.Configure<TConfig>(configurationSection);

		_ = services.AddSingleton<IHostedService>(sp =>
		{
			var factory = sp.GetRequiredService<IJobConfigHostedWatcherServiceFactory>();
			return factory.CreateAsync<TJob, TConfig>().GetAwaiter().GetResult();
		});
	}

	/// <summary>
	///     Adds tenant ID support to the service collection.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	private static IServiceCollection AddTenantId(this IServiceCollection services) =>
		services.AddScoped<ITenantId, TenantId>(_ => new TenantId("*"));

	/// <summary>
	///     Adds correlation ID support to the service collection.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	private static IServiceCollection AddCorrelationId(this IServiceCollection services) =>
		services.AddScoped<ICorrelationId, CorrelationId>(_ => new CorrelationId());

	/// <summary>
	///     Adds ETag support to the service collection.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	private static IServiceCollection AddETag(this IServiceCollection services) => services.AddScoped<IETag, ETag>(_ => new ETag());

	/// <summary>
	///     Adds client address support to the service collection.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	private static IServiceCollection AddClientAddress(this IServiceCollection services) =>
		services.AddSingleton<IClientAddress, ClientAddress>(_ =>
		{
			try
			{
				var ip = Dns.GetHostAddresses(Dns.GetHostName()).First().ToString();
				return new ClientAddress(ip);
			}
			catch (Exception)
			{
				return new ClientAddress("127.0.0.1");
			}
		});
}
