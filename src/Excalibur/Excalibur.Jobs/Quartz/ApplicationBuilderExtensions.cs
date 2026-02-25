// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.AspNetCore.Builder;

namespace Excalibur.Jobs.Quartz;

/// <summary>
/// Provides extension methods for configuring application-level middleware in an <see cref="IApplicationBuilder" />.
/// </summary>
public static class ApplicationBuilderExtensions
{
	/// <summary>
	/// Configures the application to use Excalibur's job host features, including OpenTelemetry Prometheus scraping and health checks.
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
