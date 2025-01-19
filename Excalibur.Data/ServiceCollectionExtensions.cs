using System.Reflection;
using System.Text.Json;

using Dapper;

using Excalibur.Data.Outbox;
using Excalibur.Data.Outbox.Serialization;
using Excalibur.Data.Serialization;
using Excalibur.Domain.Repositories;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;

namespace Excalibur.Data;

/// <summary>
///     Provides extension methods for configuring Excalibur data services and repositories in an application.
/// </summary>
public static class ServiceCollectionExtensions
{
	private static readonly OutboxMessageTypeHandler OutboxMessageTypeHandler = CreateOutboxMessageTypeHandler();

	/// <summary>
	///     Configures Excalibur data services, including repositories and outbox services.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <param name="configuration"> The application configuration object. </param>
	/// <param name="assemblies"> The assemblies containing repository implementations. </param>
	/// <returns> The updated <see cref="IServiceCollection" />. </returns>
	public static IServiceCollection AddExcaliburDataServices(this IServiceCollection services, IConfiguration configuration,
		params Assembly[] assemblies)
	{
		ConfigureDapper();
		ConfigureJsonSerialization();

		_ = services.AddExcaliburDataOutboxServices(configuration);
		_ = services.AddExcaliburDataRepositories(assemblies);

		return services;
	}

	/// <summary>
	///     Adds repository implementations found in the provided assemblies.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <param name="assemblies"> The assemblies containing repository implementations. </param>
	/// <returns> The updated <see cref="IServiceCollection" />. </returns>
	public static IServiceCollection AddExcaliburDataRepositories(this IServiceCollection services, params Assembly[] assemblies)
	{
		ArgumentNullException.ThrowIfNull(assemblies);

		foreach (var assembly in assemblies)
		{
			_ = services.AddImplementations(assembly, typeof(IAggregateRepository<,>), ServiceLifetime.Scoped);
		}

		return services;
	}

	/// <summary>
	///     Configures the outbox services for Excalibur, including the outbox manager and related settings.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <param name="configuration"> The application configuration object. </param>
	/// <returns> The updated <see cref="IServiceCollection" />. </returns>
	public static IServiceCollection AddExcaliburDataOutboxServices(this IServiceCollection services, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.Configure<OutboxConfiguration>(configuration.GetSection("OutboxConfiguration"));
		_ = services.AddScoped<IOutbox, Outbox.Outbox>();
		_ = services.AddScoped<IOutboxManager, OutboxManager>();

		return services;
	}

	/// <summary>
	///     Configures the mediator-based outbox message dispatcher.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <returns> The updated <see cref="IServiceCollection" />. </returns>
	public static IServiceCollection AddExcaliburMediatorOutboxMessageDispatcher(this IServiceCollection services)
	{
		_ = services.AddScoped<IOutboxMessageDispatcher, MediatorOutboxMessageDispatcher>();

		return services;
	}

	/// <summary>
	///     Configures Dapper with custom type handlers and naming conventions.
	/// </summary>
	private static void ConfigureDapper()
	{
		DefaultTypeMap.MatchNamesWithUnderscores = true;
		SqlMapper.AddTypeHandler(typeof(OutboxMessage), OutboxMessageTypeHandler);
		SqlMapper.AddTypeHandler(typeof(IEnumerable<OutboxMessage>), OutboxMessageTypeHandler);
	}

	/// <summary>
	///     Creates an instance of <see cref="OutboxMessageTypeHandler" /> with custom serialization options.
	/// </summary>
	/// <returns> A configured <see cref="OutboxMessageTypeHandler" /> instance. </returns>
	private static OutboxMessageTypeHandler CreateOutboxMessageTypeHandler()
	{
		var serializerOptions = new JsonSerializerOptions();
		serializerOptions.Converters.Add(new OutboxMessageJsonConverter());

		return new OutboxMessageTypeHandler(serializerOptions);
	}

	/// <summary>
	///     Configures global JSON serialization settings for Newtonsoft.Json.
	/// </summary>
	private static void ConfigureJsonSerialization() => JsonConvert.DefaultSettings = () => ExcaliburNewtonsoftSerializerSettings.Default;
}
