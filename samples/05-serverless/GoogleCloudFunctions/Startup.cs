// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Serialization;

using Google.Cloud.Functions.Hosting;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: FunctionsStartup(typeof(GoogleCloudFunctionsSample.Startup))]

namespace GoogleCloudFunctionsSample;

/// <summary>
/// Configures services for Google Cloud Functions.
/// </summary>
public class Startup : FunctionsStartup
{
	/// <inheritdoc/>
	public override void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
	{
		// Configure logging
		_ = services.AddLogging(builder =>
		{
			_ = builder.SetMinimumLevel(LogLevel.Information);
			_ = builder.AddConsole();
		});

		// Configure Dispatch messaging
		// Handlers are auto-registered with DI by AddHandlersFromAssembly
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(Startup).Assembly);

			// Configure JSON serialization
			_ = dispatch.WithSerialization(config => config.UseSystemTextJson());
		});

		// Configure Google Cloud Functions Serverless hosting
		_ = services.AddExcaliburGoogleCloudFunctionsServerless(opts =>
		{
			opts.EnableColdStartOptimization = true;
			opts.Telemetry.EnableDistributedTracing = true;
			opts.Telemetry.EnableStructuredLogging = true;

			// Google Cloud Functions specific options
			opts.GoogleCloudFunctions.Runtime = "dotnet8";
			opts.GoogleCloudFunctions.IngressSettings = "ALLOW_ALL";
		});
	}
}
