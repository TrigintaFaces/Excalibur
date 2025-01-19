using Microsoft.AspNetCore.Builder;

namespace Excalibur.Hosting.Jobs;

/// <summary>
///     Provides extension methods for configuring application-level middleware in an <see cref="IApplicationBuilder" />.
/// </summary>
public static class ApplicationBuilderExtensions
{
	/// <summary>
	///     Configures the application to use Excalibur's job host features, including OpenTelemetry Prometheus scraping and health checks.
	/// </summary>
	/// <param name="app"> The <see cref="IApplicationBuilder" /> instance to configure. </param>
	/// <returns> The configured <see cref="IApplicationBuilder" /> instance. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="app" /> is null. </exception>
	public static IApplicationBuilder UseExcaliburJobHost(this IApplicationBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);

		// Enable the OpenTelemetry Prometheus scraping endpoint
		_ = app.UseOpenTelemetryPrometheusScrapingEndpoint();

		// Register Excalibur health checks
		_ = app.UseExcaliburHealthChecks();

		return app;
	}
}
