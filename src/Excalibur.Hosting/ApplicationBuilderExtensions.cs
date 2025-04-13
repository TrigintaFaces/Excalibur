using System.Net.Mime;
using System.Text;
using System.Text.Json;

using HealthChecks.UI.Configuration;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Excalibur.Hosting;

/// <summary>
///     Provides extension methods for configuring health checks in an ASP.NET Core application.
/// </summary>
public static class ApplicationBuilderExtensions
{
	/// <summary>
	///     Configures the application to use Excalibur health checks, including readiness, liveness, and UI endpoints.
	/// </summary>
	/// <param name="app"> The <see cref="IApplicationBuilder" /> instance. </param>
	/// <returns> The <see cref="IApplicationBuilder" /> instance for chaining further configurations. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="app" /> is null. </exception>
	public static IApplicationBuilder UseExcaliburHealthChecks(this IApplicationBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app, nameof(app));

		_ = app.UseHealthChecks("/.well-known/ready", new HealthCheckOptions
		{
			Predicate = _ => true,
			ResponseWriter = async (httpContext, report) =>
			{
				httpContext.Response.ContentType = MediaTypeNames.Application.Json;

				var response = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
					report,
					HealthChecks.HealthCheckJsonSerializerSettings.Default));

				await httpContext.Response.Body.WriteAsync(response).ConfigureAwait(false);
			}
		});

		_ = app.UseHealthChecks("/.well-known/live", new HealthCheckOptions
		{
			Predicate = _ => true,
			ResponseWriter = async (httpContext, _) =>
			{
				var response = "pong"u8.ToArray();

				httpContext.Response.ContentType = MediaTypeNames.Text.Plain;
				await httpContext.Response.Body.WriteAsync(response).ConfigureAwait(false);
			}
		});

		_ = app.UseHealthChecksUI(delegate (Options options)
		{
			options.UIPath = "/healthcheck-ui";

			var customCssPath = Path.Combine(AppContext.BaseDirectory, "HealthCheck", "Custom.css");
			if (File.Exists(customCssPath))
			{
				_ = options.AddCustomStylesheet(customCssPath);
			}
		});

		return app;
	}
}
