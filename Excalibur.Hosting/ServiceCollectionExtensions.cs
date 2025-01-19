using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Hosting;

/// <summary>
///     Provides extension methods for registering health check services in an <see cref="IServiceCollection" />.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	///     Adds Excalibur health checks and UI components to the service collection.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <param name="withHealthChecks"> An optional action to configure additional health checks using an <see cref="IHealthChecksBuilder" />. </param>
	/// <returns> The updated <see cref="IServiceCollection" /> instance for further configuration. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="services" /> is null. </exception>
	public static IServiceCollection AddExcaliburHealthChecks(this IServiceCollection services,
		Action<IHealthChecksBuilder>? withHealthChecks = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var healthChecks = services.AddHealthChecks();

		withHealthChecks?.Invoke(healthChecks);

		_ = services.AddHealthChecksUI(static options =>
		{
			_ = options.SetEvaluationTimeInSeconds(10);
			_ = options.MaximumHistoryEntriesPerEndpoint(60);
			_ = options.SetApiMaxActiveRequests(1);
			_ = options.AddHealthCheckEndpoint("feedback api", "/.well-known/ready");
		});

		return services;
	}
}
