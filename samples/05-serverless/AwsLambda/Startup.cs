// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AwsLambdaSample;

/// <summary>
/// Configures services for AWS Lambda functions.
/// </summary>
public static class Startup
{
	private static readonly Lazy<IServiceProvider> _serviceProvider = new(BuildServiceProvider);

	/// <summary>
	/// Gets the configured service provider.
	/// </summary>
	public static IServiceProvider ServiceProvider => _serviceProvider.Value;

	private static IServiceProvider BuildServiceProvider()
	{
		var services = new ServiceCollection();

		// Configure logging
		_ = services.AddLogging(builder =>
		{
			_ = builder.SetMinimumLevel(LogLevel.Information);
			_ = builder.AddLambdaLogger();
		});

		// Configure Dispatch messaging
		// Handlers are auto-registered with DI by AddHandlersFromAssembly
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(Startup).Assembly);

			// Register JSON serializer for message payloads
			_ = dispatch.AddDispatchSerializer<DispatchJsonSerializer>(version: 0);
		});

		// Configure AWS Lambda Serverless hosting
		_ = services.AddExcaliburAwsLambdaServerless(opts =>
		{
			opts.EnableColdStartOptimization = true;
			opts.EnableDistributedTracing = true;
			opts.EnableStructuredLogging = true;

			// AWS Lambda specific options
			opts.AwsLambda.Runtime = "dotnet8";
			opts.AwsLambda.PackageType = "Zip";
		});

		return services.BuildServiceProvider();
	}
}

/// <summary>
/// Lambda logger extension for Microsoft.Extensions.Logging.
/// </summary>
public static class LambdaLoggerExtensions
{
	/// <summary>
	/// Adds Lambda logger to the logging builder.
	/// </summary>
	/// <param name="builder">The logging builder.</param>
	/// <returns>The logging builder for chaining.</returns>
	public static ILoggingBuilder AddLambdaLogger(this ILoggingBuilder builder)
	{
		// In production, use Amazon.Lambda.Logging.AspNetCore or similar
		// For simplicity, we're using console output which Lambda captures
		return builder.AddConsole();
	}
}
