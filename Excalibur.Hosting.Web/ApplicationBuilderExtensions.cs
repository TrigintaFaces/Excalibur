using Excalibur.Core;
using Excalibur.Core.Concurrency;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace Excalibur.Hosting.Web;

/// <summary>
///     Provides extension methods for configuring middleware in an <see cref="IApplicationBuilder" />.
/// </summary>
public static class ApplicationBuilderExtensions
{
	/// <summary>
	///     Configures the Excalibur web host middleware pipeline.
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
	///     Adds middleware to populate the <see cref="ITenantId" /> service with the current tenant ID.
	/// </summary>
	/// <param name="app"> The application builder to configure. </param>
	/// <returns> The configured <see cref="IApplicationBuilder" />. </returns>
	private static IApplicationBuilder UseTenantIdMiddleware(this IApplicationBuilder app) => app
		.Use((httpContext, next) =>
		{
			httpContext.RequestServices
				.GetRequiredService<ITenantId>()
				.Value = httpContext.TenantId();

			return next();
		});

	/// <summary>
	///     Adds middleware to populate the <see cref="ICorrelationId" /> service with the current correlation ID.
	/// </summary>
	/// <param name="app"> The application builder to configure. </param>
	/// <returns> The configured <see cref="IApplicationBuilder" />. </returns>
	private static IApplicationBuilder UseCorrelationIdMiddleware(this IApplicationBuilder app) => app
		.Use((httpContext, next) =>
		{
			httpContext.RequestServices
				.GetRequiredService<ICorrelationId>()
				.Value = httpContext.CorrelationId();

			return next();
		});

	/// <summary>
	///     Adds middleware to manage incoming and outgoing ETags for the current request.
	/// </summary>
	/// <param name="app"> The application builder to configure. </param>
	/// <returns> The configured <see cref="IApplicationBuilder" />. </returns>
	private static IApplicationBuilder UseETagMiddleware(this IApplicationBuilder app) => app
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
	///     Adds middleware to populate the <see cref="IClientAddress" /> service with the client's remote IP address.
	/// </summary>
	/// <param name="app"> The application builder to configure. </param>
	/// <returns> The configured <see cref="IApplicationBuilder" />. </returns>
	private static IApplicationBuilder UseClientAddressMiddleware(this IApplicationBuilder app) => app
		.Use((httpContext, next) =>
		{
			httpContext.RequestServices
				.GetRequiredService<IClientAddress>()
				.Value = httpContext.RemoteIpAddress();

			return next();
		});
}
