// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Provides extension methods for configuring application-level middleware for job hosting in an <see cref="IApplicationBuilder" />.
/// </summary>
public static class ExcaliburJobHostApplicationExtensions
{
	/// <summary>
	/// Configures the application to use Excalibur's job host features, including health checks and monitoring endpoints.
	/// </summary>
	/// <param name="app"> The <see cref="IApplicationBuilder" /> instance to configure. </param>
	/// <returns> The configured <see cref="IApplicationBuilder" /> instance. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="app" /> is null. </exception>
	/// <remarks>
	/// This method sets up monitoring and health check endpoints for job applications. It's particularly useful for ASP.NET Core
	/// applications that also run background jobs.
	/// </remarks>
	public static IApplicationBuilder UseExcaliburJobHost(this IApplicationBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);

		return Excalibur.Jobs.Quartz.ApplicationBuilderExtensions.UseExcaliburJobHost(app);
	}
}
