// Copyright (c) Nexus Dynamics. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Excalibur.Dispatch.CloudNative.Serverless.Google.CloudRun;
using Excalibur.Dispatch.CloudNative.Serverless.Google.Scaling;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace examples.CloudNative.Serverless.GoogleCloudRun.Autoscaling
 /// <summary>
 /// Example of a Cloud Run service with autoscaling configured.
 /// </summary>
 public class AutoscalingExample {
 public static async Task Main(string[] args)
 {
 var builder = WebApplication.CreateBuilder(args);

 // Configure Cloud Run services
 builder.Services.AddCloudRun();

 // Configure autoscaling with custom settings
 builder.Services.AddCloudRunAutoscaling(options =>
 {
 options.MinInstances = 2;
 options.MaxInstances = 50;
 options.MaxConcurrency = 100;
 options.TargetCpuUtilization = 70;
 options.TargetMemoryUtilization = 80;
 options.TargetRequestLatencyMs = 300;
 options.ScaleUpDelay = TimeSpan.FromSeconds(15);
 options.ScaleDownDelay = TimeSpan.FromSeconds(120);
 options.EnableStartupCpuBoost = true;
 options.EnablePredictiveScaling = true;
 });

 // Add custom metrics for scaling decisions
 builder.Services.Configure<CloudRunAutoscalingConfiguration>(config =>
 {
 config.CustomMetrics.Add("QueueDepth", new ScalingMetric
 {
 Name = "message_queue_depth",
 TargetValue = 100,
 Type = MetricType.Gauge,
 Weight = 1.2
 });

 config.CustomMetrics.Add("DatabaseConnections", new ScalingMetric
 {
 Name = "db_connection_pool_usage",
 TargetValue = 70,
 Type = MetricType.Gauge,
 Weight = 0.8
 });
 });

 // Add scheduled scaling for known traffic patterns
 builder.Services.AddScheduledScaling(schedule =>
 {
 // Scale up before business hours
 schedule.AddDailySchedule(
 time: TimeSpan.FromHours(7.5),
 instances: 10);

 // Scale down after business hours
 schedule.AddDailySchedule(
 time: TimeSpan.FromHours(19),
 instances: 3);

 // Weekend scaling
 schedule.AddWeeklySchedule(
 DayOfWeek.Saturday,
 time: TimeSpan.Zero,
 instances: 2);

 schedule.AddWeeklySchedule(
 DayOfWeek.Sunday,
 time: TimeSpan.Zero,
 instances: 2);
 });

 // Configure services
 builder.Services.AddControllers();
 builder.Services.AddHealthChecks();

 var app = builder.Build();

 // Configure middleware
 app.UseCloudRunMiddleware();
 app.UseRouting();

 // Health check endpoints
 app.MapHealthChecks("/_health");
 app.MapHealthChecks("/_ready", new()
 {
 Predicate = check => check.Tags.Contains("ready")
 });

 // API endpoints
 app.MapControllers();

 // Custom endpoint to view autoscaling metrics
 app.MapGet("/_autoscaling/metrics", async (CloudRunAutoscalingManager manager) =>
 {
 var metrics = await manager.GetMetricsAsync();
 return Results.Ok(metrics);
 });

 // Custom endpoint to manually trigger scaling evaluation
 app.MapPost("/_autoscaling/evaluate", async (CloudRunAutoscalingManager manager) =>
 {
 var decision = await manager.EvaluateScalingAsync();
 return Results.Ok(decision);
 });

 // Custom endpoint to manually set instance count
 app.MapPost("/_autoscaling/instances/{count:int}",
 async (int count, CloudRunAutoscalingManager manager) =>
 {
 try
 {
 await manager.SetInstanceCountAsync(count, default);
 return Results.Ok(new { success = true, instances = count });
 }
 catch (ArgumentOutOfRangeException ex)
 {
 return Results.BadRequest(new { error = ex.Message });
 }
 });

 await app.RunAsync();
 }
 }

 /// <summary>
 /// Example controller that generates variable load.
 /// </summary>
 [ApiController]
 [Route("api/[controller]")]
 public class WorkloadController : ControllerBase
 {
 private readonly ILogger<WorkloadController> _logger;
 private readonly CloudRunMetricsCollector _metricsCollector;
 private static readonly Random _random = new();

 public WorkloadController(ILogger<WorkloadController> logger,
 IMetricsCollector metricsCollector)
 {
 _logger = logger;
 _metricsCollector = (CloudRunMetricsCollector)metricsCollector;
 }

 /// <summary>
 /// Simulates CPU-intensive workload.
 /// </summary>
 [HttpPost("cpu-intensive")]
 public async Task<IActionResult> ProcessCpuIntensive([FromBody] WorkloadRequest request)
 {
 _metricsCollector.IncrementActiveRequests();
 var sw = Stopwatch.StartNew();

 try
 {
 // Simulate CPU work
 var result = 0.0;
 for (int i = 0; i < request.Iterations; i++)
 {
 result += Math.Sqrt(i) * Math.Sin(i);
 }

 await Task.Delay(request.DelayMs);

 _metricsCollector.RecordRequest(true, sw.ElapsedMilliseconds);

 return Ok(new
 {
 result,
 processingTime = sw.ElapsedMilliseconds,
 instanceId = Environment.GetEnvironmentVariable("K_REVISION")
 });
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Error processing CPU-intensive request");
 _metricsCollector.RecordRequest(false, sw.ElapsedMilliseconds);
 throw;
 }
 finally
 {
 _metricsCollector.DecrementActiveRequests();
 }
 }

 /// <summary>
 /// Simulates memory-intensive workload.
 /// </summary>
 [HttpPost("memory-intensive")]
 public async Task<IActionResult> ProcessMemoryIntensive([FromBody] WorkloadRequest request)
 {
 _metricsCollector.IncrementActiveRequests();
 var sw = Stopwatch.StartNew();

 try
 {
 // Allocate memory
 var data = new byte[request.MemoryMb * 1024 * 1024];
 _random.NextBytes(data);

 // Process data
 var sum = 0L;
 for (int i = 0; i < data.Length; i += 1024)
 {
 sum += data[i];
 }

 await Task.Delay(request.DelayMs);

 _metricsCollector.RecordRequest(true, sw.ElapsedMilliseconds);

 return Ok(new
 {
 checksum = sum,
 bytesProcessed = data.Length,
 processingTime = sw.ElapsedMilliseconds
 });
 }
 finally
 {
 _metricsCollector.DecrementActiveRequests();
 }
 }

 /// <summary>
 /// Simulates variable latency workload.
 /// </summary>
 [HttpGet("variable-latency")]
 public async Task<IActionResult> ProcessVariableLatency()
 {
 _metricsCollector.IncrementActiveRequests();
 var sw = Stopwatch.StartNew();

 try
 {
 // Random latency between 50ms and 500ms
 var latency = 50 + _random.Next(450);
 await Task.Delay(latency);

 _metricsCollector.RecordRequest(true, sw.ElapsedMilliseconds);

 return Ok(new
 {
 latency,
 timestamp = DateTime.UtcNow
 });
 }
 finally
 {
 _metricsCollector.DecrementActiveRequests();
 }
 }
 }

 /// <summary>
 /// Workload request model.
 /// </summary>
 public class WorkloadRequest {
 public int Iterations { get; set; } = 1000000;
 public int MemoryMb { get; set; } = 10;
 public int DelayMs { get; set; } = 100;
 }
}
