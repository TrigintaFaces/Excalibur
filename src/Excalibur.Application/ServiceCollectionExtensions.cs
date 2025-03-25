using System.Reflection;

using Excalibur.Application.Behaviors;
using Excalibur.Application.Requests;
using Excalibur.Domain;

using FluentValidation;

using MediatR;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Application;

/// <summary>
///     Provides extension methods for configuring Excalibur application services in an <see cref="IServiceCollection" />.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	///     Adds all required application services for an Excalibur application, including activity context, validators, Mediator, and AutoMapper.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="assemblies"> The assemblies containing types to register. </param>
	/// <returns> The updated <see cref="IServiceCollection" /> instance. </returns>
	public static IServiceCollection AddExcaliburApplicationServices(this IServiceCollection services, params Assembly[] assemblies)
	{
		// Register activity context as a scoped service.
		_ = services.AddScoped<IActivityContext, ActivityContext>();

		// Register validators from the provided assemblies, including internal types.
		_ = services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true);

		// Add Mediator services from the provided assemblies.
		_ = services.AddMediatorServices(assemblies);

		// Register AutoMapper profiles from the provided assemblies.
		_ = services.AddAutoMapper(assemblies);

		return services;
	}

	/// <summary>
	///     Configures Mediator services and pipeline behaviors.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="assemblies"> The assemblies containing Mediator handlers and behaviors. </param>
	/// <returns> The updated <see cref="IServiceCollection" /> instance. </returns>
	public static IServiceCollection AddMediatorServices(this IServiceCollection services, params Assembly[] assemblies)
	{
		_ = services
			.AddMediatR(cfg =>
			{
				// Register Mediator services from the specified assemblies.
				foreach (var assembly in assemblies)
				{
					_ = cfg.RegisterServicesFromAssembly(assembly);
				}
			})
			// Add pipeline behaviors for logging, metrics, transactions, and validation.
			.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>))
			.AddTransient(typeof(IPipelineBehavior<,>), typeof(MetricsBehavior<,>))
			.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>))
			.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));

		return services;
	}

	/// <summary>
	///     Registers all concrete implementations of <see cref="IActivity" /> from the specified assemblies as singletons.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="assemblies"> The assemblies containing activity types to register. </param>
	/// <returns> The updated <see cref="IServiceCollection" /> instance. </returns>
	public static IServiceCollection AddActivities(this IServiceCollection services, params Assembly[] assemblies)
	{
		ArgumentNullException.ThrowIfNull(assemblies);

		foreach (var assembly in assemblies)
		{
			assembly
				.GetExportedTypes()
				.Where(type => type is { IsAbstract: false, IsGenericTypeDefinition: false } && typeof(IActivity).IsAssignableFrom(type))
				.ToList()
				.ForEach(a => services.AddSingleton(typeof(IActivity), a));
		}

		return services;
	}
}
