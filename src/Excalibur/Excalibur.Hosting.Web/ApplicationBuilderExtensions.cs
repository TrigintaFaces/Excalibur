// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain;
using Excalibur.Domain.Concurrency;
using Excalibur.Hosting.Web;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Provides extension methods for configuring middleware in an <see cref="IApplicationBuilder" />.
/// </summary>
public static class ExcaliburWebHostApplicationExtensions
{
	/// <summary>
	/// Configures the Excalibur web host middleware pipeline.
	/// </summary>
	/// <param name="app"> The application builder to configure. </param>
	/// <returns> The configured <see cref="IApplicationBuilder" />. </returns>
	public static IApplicationBuilder UseExcaliburWebHost(this IApplicationBuilder app)
	{
		_ = app.UseExceptionHandler();
		_ = app.UseTenantIdMiddleware();
		_ = app.UseCorrelationIdMiddleware();
		_ = app.UseETagMiddleware();
		_ = app.UseClientAddressMiddleware();

		return app;
	}

	/// <summary>
	/// Adds middleware to populate the <see cref="ITenantId" /> service with the current tenant ID.
	/// </summary>
	/// <param name="app"> The application builder to configure. </param>
	/// <returns> The configured <see cref="IApplicationBuilder" />. </returns>
	public static IApplicationBuilder UseTenantIdMiddleware(this IApplicationBuilder app) =>
		app
			.Use(static (httpContext, next) =>
			{
				var tenantId = httpContext.TenantId();
				if (tenantId != null)
				{
					httpContext.RequestServices
						.GetRequiredService<ITenantId>()
						.Value = tenantId.Value.ToString();
				}

				return next();
			});

	/// <summary>
	/// Adds middleware to populate the <see cref="ICorrelationId" /> service with the current correlation ID.
	/// </summary>
	/// <param name="app"> The application builder to configure. </param>
	/// <returns> The configured <see cref="IApplicationBuilder" />. </returns>
	public static IApplicationBuilder UseCorrelationIdMiddleware(this IApplicationBuilder app) =>
		app
			.Use(static (httpContext, next) =>
			{
				httpContext.RequestServices
					.GetRequiredService<ICorrelationId>()
					.Value = httpContext.CorrelationId();

				return next();
			});

	/// <summary>
	/// Adds middleware to manage incoming and outgoing ETags for the current request.
	/// </summary>
	/// <param name="app"> The application builder to configure. </param>
	/// <returns> The configured <see cref="IApplicationBuilder" />. </returns>
	public static IApplicationBuilder UseETagMiddleware(this IApplicationBuilder app) =>
		app
			.Use(async (httpContext, next) =>
			{
				httpContext.RequestServices
					.GetRequiredService<IETag>()
					.IncomingValue = httpContext.ETag();

				httpContext.Response.OnStarting(() =>
				{
					var etag = httpContext.RequestServices
						.GetRequiredService<IETag>()
						.OutgoingValue;

					if (!string.IsNullOrEmpty(etag))
					{
						httpContext.Response.Headers.Append(HeaderNames.ETag, etag.Split(','));
					}

					return Task.CompletedTask;
				});

				await next().ConfigureAwait(false);
			});

	/// <summary>
	/// Adds middleware to populate the <see cref="IClientAddress" /> service with the client's remote IP address.
	/// </summary>
	/// <param name="app"> The application builder to configure. </param>
	/// <returns> The configured <see cref="IApplicationBuilder" />. </returns>
	public static IApplicationBuilder UseClientAddressMiddleware(this IApplicationBuilder app) =>
		app
			.Use(static (httpContext, next) =>
			{
				httpContext.RequestServices
					.GetRequiredService<IClientAddress>()
					.Value = httpContext.RemoteIpAddress()?.ToString();

				return next();
			});
}
