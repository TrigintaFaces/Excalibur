using System.Reflection;

using Asp.Versioning;

using Excalibur.Application;
using Excalibur.Core;
using Excalibur.Core.Concurrency;
using Excalibur.Data;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Hosting.Web;

/// <summary>
///     Provides extension methods for configuring Excalibur web services in the application's dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	///     Adds the necessary services for Excalibur web applications, including problem details, exception handling, API versioning, and
	///     core services.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <param name="configuration"> The application's configuration settings. </param>
	/// <param name="assemblies"> The assemblies to scan for application and data services. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException">
	///     Thrown if <paramref name="services" /> or <paramref name="configuration" /> is null.
	/// </exception>
	public static IServiceCollection AddExcaliburWebServices(this IServiceCollection services, IConfiguration configuration,
		params Assembly[] assemblies)
	{
		ArgumentNullException.ThrowIfNull(services, nameof(services));
		ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

		// Add problem details middleware for consistent error responses
		_ = services.AddProblemDetails();

		// Add global exception handling
		_ = services.AddExceptionHandler<GlobalExceptionHandler>();

		// Add Excalibur-specific data and application services
		_ = services.AddExcaliburDataServices(configuration, assemblies);
		_ = services.AddExcaliburApplicationServices(assemblies);

		// Add tenant ID, correlation ID, ETag, and client address services
		_ = services.AddTenantId();
		_ = services.AddCorrelationId();
		_ = services.AddETag();
		_ = services.AddClientAddress();

		// Configure API versioning
		_ = services
			.AddApiVersioning(options =>
			{
				options.AssumeDefaultVersionWhenUnspecified = true;
				options.DefaultApiVersion = new ApiVersion(1, 0);
				options.ReportApiVersions = true;
				options.ApiVersionReader = ApiVersionReader.Combine(
					new UrlSegmentApiVersionReader(),
					new QueryStringApiVersionReader("api-version"),
					new HeaderApiVersionReader("X-Api-Version"));
			})
			.AddApiExplorer(options =>
			{
				// ReSharper disable once StringLiteralTypo
				options.GroupNameFormat = "'v'VVV";
				options.SubstituteApiVersionInUrl = true;
			});

		return services;
	}

	/// <summary>
	///     Adds a scoped tenant ID service to the application's dependency injection container.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	private static IServiceCollection AddTenantId(this IServiceCollection services) =>
		services.AddScoped<ITenantId, TenantId>(_ => new TenantId());

	/// <summary>
	///     Adds a scoped correlation ID service to the application's dependency injection container.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	private static IServiceCollection AddCorrelationId(this IServiceCollection services) =>
		services.AddScoped<ICorrelationId, CorrelationId>(_ => new CorrelationId());

	/// <summary>
	///     Adds a scoped ETag service to the application's dependency injection container.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	private static IServiceCollection AddETag(this IServiceCollection services) => services.AddScoped<IETag, ETag>(_ => new ETag());

	/// <summary>
	///     Adds a scoped client address service to the application's dependency injection container.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	private static IServiceCollection AddClientAddress(this IServiceCollection services) =>
		services.AddScoped<IClientAddress, ClientAddress>(_ => new ClientAddress());
}
