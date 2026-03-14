// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;

using Serilog;
using Serilog.Core;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for configuring Serilog-based logging in an <see cref="IHostApplicationBuilder" />.
/// </summary>
public static class SerilogHostApplicationBuilderExtensions
{
	/// <summary>
	/// Configures Serilog-based structured logging.
	/// </summary>
	/// <param name="builder"> The <see cref="IHostApplicationBuilder" /> to configure. </param>
	/// <param name="additionalLogSinks"> The additional sinks to configure. </param>
	/// <returns> The updated <see cref="IHostApplicationBuilder" /> instance for further configuration. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="builder" /> is null. </exception>
	public static IHostApplicationBuilder ConfigureExcaliburLogging(
		this IHostApplicationBuilder builder,
		params ILogEventSink[]? additionalLogSinks)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var loggerConfig = new LoggerConfiguration()
			.ReadFrom.Configuration(builder.Configuration)
			.Enrich.FromLogContext()
			.Enrich.WithProperty("ApplicationName", builder.Environment.ApplicationName)
			.Enrich.WithProperty("Environment", builder.Environment.EnvironmentName);

		foreach (var sink in additionalLogSinks ?? [])
		{
			_ = loggerConfig.WriteTo.Sink(sink);
		}

		Log.Logger = loggerConfig.CreateLogger();

		_ = builder.Logging.ClearProviders();
		_ = builder.Logging.AddSerilog(Log.Logger, dispose: true);

		_ = builder.Services.AddSerilog(Log.Logger, dispose: true);

		Log.Information("Serilog logging configured successfully.");

		return builder;
	}
}
