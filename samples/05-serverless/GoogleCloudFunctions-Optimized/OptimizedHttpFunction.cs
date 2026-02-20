// Copyright (c) Nexus Dynamics. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Functions.Framework;
using Google.Cloud.Functions.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace examples.Excalibur.Dispatch.Examples.Serverless.Google
 /// <summary>
 /// Example of an optimized HTTP function with startup optimization.
 /// </summary>
 [FunctionsStartup(typeof(OptimizedHttpFunctionStartup))]
 public class OptimizedHttpFunction : IHttpFunction
 {
 private readonly ILogger<OptimizedHttpFunction> _logger;
 private readonly StartupMetrics _metrics;
 private static readonly string WarmupResponse = System.Text.Json.JsonSerializer.Serialize(new { status = "warm" });

 public OptimizedHttpFunction(ILogger<OptimizedHttpFunction> logger, StartupMetrics metrics)
 {
 _logger = logger ?? throw new ArgumentNullException(nameof(logger));
 _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
 }

 public async Task HandleAsync(HttpContext context)
 {
 // Record first request if not already done
 if (_metrics.FirstRequestOverheadMs == 0)
 {
 _metrics.RecordFirstRequestTime();
 }

 // Handle warmup requests
 if (context.Request.Path == "/_ah/warmup")
 {
 _logger.LogInformation("Handling warmup request");
 context.Response.StatusCode = 200;
 context.Response.ContentType = "application/json";
 await context.Response.WriteAsync(WarmupResponse).ConfigureAwait(false);
 return;
 }

 // Regular request handling
 var response = new
 {
 message = "Hello from optimized Google Cloud Function!",
 metrics = new
 {
 totalStartupMs = _metrics.TotalStartupTimeMs,
 serviceProviderBuildMs = _metrics.ServiceProviderBuildTimeMs,
 prewarmMs = _metrics.PrewarmTimeMs,
 firstRequestOverheadMs = _metrics.FirstRequestOverheadMs
 },
 timestamp = DateTime.UtcNow
 };

 context.Response.StatusCode = 200;
 context.Response.ContentType = "application/json";
 await context.Response.WriteAsync(
 System.Text.Json.JsonSerializer.Serialize(response)
 ).ConfigureAwait(false);
 }
 }

 /// <summary>
 /// Startup configuration for the optimized HTTP function.
 /// </summary>
 public class OptimizedHttpFunctionStartup : FunctionsStartup
 {
 public override void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
 {
 // Apply startup optimizations
 services.OptimizeStartup();
 services.AddGoogleCloudFunctionsPerformance(context.Configuration);

 // Add function-specific services
 services.AddSingleton<IPrewarmable, PrewarmableDataService>();
 }

 public override void Configure(WebHostBuilderContext context, IApplicationBuilder app)
 {
 // Prewarm services on startup
 var serviceProvider = app.ApplicationServices;
 Task.Run(async () => await StartupOptimization.PrewarmAsync(serviceProvider));
 }
 }

 /// <summary>
 /// Example service that supports prewarming.
 /// </summary>
 public class PrewarmableDataService : IPrewarmable
 {
 private readonly ILogger<PrewarmableDataService> _logger;
 private volatile bool _isInitialized;
 private string[]? _cachedData;

 public PrewarmableDataService(ILogger<PrewarmableDataService> logger)
 {
 _logger = logger ?? throw new ArgumentNullException(nameof(logger));
 }

 public async Task PrewarmAsync(CancellationToken cancellationToken = default)
 {
 if (_isInitialized)
 return;

 _logger.LogInformation("Prewarming data service");

 // Simulate loading data or establishing connections
 await Task.Delay(50, cancellationToken).ConfigureAwait(false);

 _cachedData = new[]
 {
 "Preloaded data 1",
 "Preloaded data 2",
 "Preloaded data 3"
 };

 _isInitialized = true;
 _logger.LogInformation("Data service prewarming completed");
 }

 public string[] GetData()
 {
 return _cachedData ?? Array.Empty<string>();
 }
 }
}
