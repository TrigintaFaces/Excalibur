// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Azure Functions Serverless Sample
// =================================
// This sample demonstrates how to use Dispatch messaging in Azure Functions
// with HTTP triggers, Queue triggers, and Timer triggers.
//
// Prerequisites:
// 1. Install Azure Functions Core Tools: npm install -g azure-functions-core-tools@4
// 2. (Optional) Install Azurite for local storage emulation: npm install -g azurite
// 3. Run: func start
//
// For Azure deployment:
// 1. Create an Azure Function App
// 2. Deploy using: func azure functionapp publish <app-name>

#pragma warning disable CA1303 // Sample code uses literal strings

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Serialization;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
	.ConfigureFunctionsWebApplication()
	.ConfigureServices(services =>
	{
		// Configure logging
		_ = services.AddLogging(logging =>
		{
			_ = logging.SetMinimumLevel(LogLevel.Information);
		});

		// Configure Application Insights
		_ = services.AddApplicationInsightsTelemetryWorkerService();
		_ = services.ConfigureFunctionsApplicationInsights();

		// ============================================================
		// Configure Dispatch messaging
		// ============================================================
		// Handlers are auto-registered with DI by AddHandlersFromAssembly
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

			// Register JSON serializer for message payloads
			_ = dispatch.AddDispatchSerializer<DispatchJsonSerializer>(version: 0);
		});

		// ============================================================
		// Configure Azure Functions Serverless hosting
		// ============================================================
		_ = services.AddExcaliburAzureFunctionsServerless(opts =>
		{
			opts.EnableColdStartOptimization = true;
			opts.EnableDistributedTracing = true;
			opts.EnableStructuredLogging = true;

			// Azure Functions specific options
			opts.AzureFunctions.HostingPlan = "Consumption";
			opts.AzureFunctions.RuntimeVersion = "~4";
		});
	})
	.Build();

await host.RunAsync().ConfigureAwait(false);

#pragma warning restore CA1303
