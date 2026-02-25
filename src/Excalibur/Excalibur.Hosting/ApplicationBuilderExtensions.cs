// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Net.Mime;
using System.Text;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization;

using Excalibur.Hosting.HealthChecks;

using HealthChecks.UI.Configuration;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Provides extension methods for configuring health checks in an ASP.NET Core application.
/// </summary>
public static class ExcaliburHealthChecksApplicationExtensions
{
	private static readonly IJsonSerializer Serializer =
		new DispatchJsonSerializer(static options =>
		{
			var healthOptions = HealthCheckJsonSerializerOptions.Default;
			options.PropertyNamingPolicy = healthOptions.PropertyNamingPolicy;
			options.DefaultIgnoreCondition = healthOptions.DefaultIgnoreCondition;
			options.WriteIndented = healthOptions.WriteIndented;

			// Add health check converters
			options.Converters.Add(new HealthReportEntryJsonConverter());
			options.Converters.Add(new HealthReportJsonConverter());
		});

	/// <summary>
	/// Configures the application to use Excalibur health checks, including readiness, liveness, and UI endpoints.
	/// </summary>
	/// <param name="app"> The <see cref="IApplicationBuilder" /> instance. </param>
	/// <returns> The <see cref="IApplicationBuilder" /> instance for chaining further configurations. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="app" /> is null. </exception>
	public static IApplicationBuilder UseExcaliburHealthChecks(this IApplicationBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);

		_ = app.UseHealthChecks("/.well-known/ready", new HealthCheckOptions
		{
			Predicate = static _ => true,
			ResponseWriter = static async (httpContext, report) =>
			{
				httpContext.Response.ContentType = MediaTypeNames.Application.Json;

				var response = Encoding.UTF8.GetBytes(await Serializer.SerializeAsync(
					report).ConfigureAwait(false));

				await httpContext.Response.Body.WriteAsync(response).ConfigureAwait(false);
			},
		});

		_ = app.UseHealthChecks("/.well-known/live", new HealthCheckOptions
		{
			Predicate = static _ => true,
			ResponseWriter = static async (httpContext, _) =>
			{
				var response = "pong"u8.ToArray();

				httpContext.Response.ContentType = MediaTypeNames.Text.Plain;
				await httpContext.Response.Body.WriteAsync(response).ConfigureAwait(false);
			},
		});

		_ = app.UseHealthChecksUI((Options options) =>
		{
			options.UIPath = "/health-check-ui";

			var customCssPath = Path.Combine(AppContext.BaseDirectory, "HealthCheck", "Custom.css");
			if (File.Exists(customCssPath))
			{
				_ = options.AddCustomStylesheet(customCssPath);
			}
		});

		return app;
	}
}
