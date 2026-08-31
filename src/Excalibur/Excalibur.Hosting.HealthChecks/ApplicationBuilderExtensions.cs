// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Serialization;

using Excalibur.Hosting.HealthChecks;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Provides extension methods for configuring health checks in an ASP.NET Core application.
/// </summary>
public static class ExcaliburHealthChecksApplicationExtensions
{
	private static DispatchJsonSerializer? _serializer;

	/// <summary>
	/// Gets the serializer used to write health-report responses.
	/// </summary>
	/// <remarks>
	/// Built on first access rather than in a field initializer, because it reads options that require
	/// dynamic code and a class constructor cannot declare that requirement.
	/// </remarks>
	private static DispatchJsonSerializer Serializer
	{
		[RequiresDynamicCode(
			"Reads the health-check options, which derive from options built at run time.")]
		get
		{
			var existing = Volatile.Read(ref _serializer);
			if (existing is not null)
			{
				return existing;
			}

			var created = new DispatchJsonSerializer(ConfigureHealthReportOptions);
			return Interlocked.CompareExchange(ref _serializer, created, null) ?? created;
		}
	}

	[RequiresDynamicCode(
		"Reads the health-check options, which derive from options built at run time.")]
	private static void ConfigureHealthReportOptions(JsonSerializerOptions options)
	{
		var healthOptions = HealthCheckJsonSerializerOptions.Default;
		options.PropertyNamingPolicy = healthOptions.PropertyNamingPolicy;
		options.DefaultIgnoreCondition = healthOptions.DefaultIgnoreCondition;
		options.WriteIndented = healthOptions.WriteIndented;

		// Add health check converters
		options.Converters.Add(new HealthReportEntryJsonConverter());
		options.Converters.Add(new HealthReportJsonConverter());
	}

	/// <summary>
	/// Configures the application to use Excalibur health checks, exposing readiness and liveness endpoints.
	/// </summary>
	/// <param name="app"> The <see cref="IApplicationBuilder" /> instance. </param>
	/// <returns> The <see cref="IApplicationBuilder" /> instance for chaining further configurations. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="app" /> is null. </exception>
	[RequiresUnreferencedCode("Health check JSON serialization uses DispatchJsonSerializer which may require unreferenced code.")]
	[RequiresDynamicCode("Health check JSON serialization uses DispatchJsonSerializer which requires dynamic code generation.")]
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
					report, report.GetType()).ConfigureAwait(false));

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

		return app;
	}
}
