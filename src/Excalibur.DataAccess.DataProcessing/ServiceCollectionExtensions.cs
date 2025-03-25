using System.Reflection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.DataAccess.DataProcessing;

/// <summary>
///     Extension methods for registering data processing services with the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	///     Adds the required services and configurations for data processing to the dependency injection container.
	/// </summary>
	/// <typeparam name="TDataProcessorDb"> The database type implementing <see cref="IDb" /> for data processor persistence. </typeparam>
	/// <typeparam name="TDataToProcessDb"> The database type implementing <see cref="IDb" /> for data to process persistence. </typeparam>
	/// <param name="services"> The <see cref="IServiceCollection" /> to add the services to. </param>
	/// <param name="configuration"> The application configuration containing the required settings. </param>
	/// <param name="configurationSection"> The section of the configuration containing the data processing settings. </param>
	/// <param name="handlerAssemblies"> The assemblies to scan for <see cref="IDataProcessor" /> implementations. </param>
	/// <returns> The updated <see cref="IServiceCollection" /> instance. </returns>
	/// <exception cref="ArgumentNullException">
	///     Thrown if <paramref name="configuration" /> or <paramref name="handlerAssemblies" /> is <c> null </c>.
	/// </exception>
	public static IServiceCollection AddDataProcessing<TDataProcessorDb, TDataToProcessDb>(
		this IServiceCollection services,
		IConfiguration configuration,
		string configurationSection,
		params Assembly[] handlerAssemblies)
		where TDataProcessorDb : class, IDb
		where TDataToProcessDb : class, IDb
	{
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentException.ThrowIfNullOrWhiteSpace(configurationSection);
		ArgumentNullException.ThrowIfNull(handlerAssemblies);

		_ = services.AddScoped<IDataProcessorDb, DataProcessorDb>(sp => new DataProcessorDb(sp.GetRequiredService<TDataProcessorDb>()));
		_ = services.AddScoped<IDataToProcessDb, DataToProcessDb>(sp => new DataToProcessDb(sp.GetRequiredService<TDataToProcessDb>()));

		foreach (var processor in DataProcessorDiscovery.DiscoverProcessors(handlerAssemblies))
		{
			_ = services.AddScoped(typeof(IDataProcessor), processor);
		}

		foreach (var (interfaceType, implementationType) in RecordHandlerDiscovery.DiscoverHandlers(handlerAssemblies))
		{
			_ = services.AddScoped(interfaceType, implementationType);
		}

		_ = services.Configure<DataProcessingConfiguration>(configuration.GetSection(configurationSection));
		_ = services.AddScoped<IDataProcessorRegistry, DataProcessorRegistry>();
		_ = services.AddScoped<IDataOrchestrationManager, DataOrchestrationManager>();

		return services;
	}
}
