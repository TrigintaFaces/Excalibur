// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Alba;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Excalibur.Dispatch.Tests.Functional.Infrastructure.TestBaseClasses.Host;

/// <summary>
///     Extension methods to bridge Alba API changes.
/// </summary>
public static class AlbaExtensions
{
	/// <summary>
	///     Starts Alba from a WebApplicationBuilder.
	/// </summary>
	public static async Task<IAlbaHost> StartAlbaAsync(this WebApplicationBuilder builder, Action<WebApplication>? configure = null)
	{
		// Create a new host builder with the services from WebApplicationBuilder
		var hostBuilder = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
			.ConfigureServices(services =>
			{
				// Copy all services from the WebApplicationBuilder
				foreach (var service in builder.Services)
				{
					services.Add(service);
				}
			})
			.ConfigureWebHostDefaults(webBuilder =>
			{
				_ = webBuilder.UseKestrel();
				_ = webBuilder.Configure(app =>
				{
					// Basic middleware setup
					_ = app.UseRouting();
					_ = app.UseEndpoints(endpoints => _ = endpoints.MapControllers());
				});
			});

		// Create Alba host from the host builder
		return await AlbaHost.For(hostBuilder).ConfigureAwait(false);
	}

	/// <summary>
	///     Gets the server from an IAlbaHost.
	/// </summary>
	public static IHost Server(this IAlbaHost host) => host;
}
