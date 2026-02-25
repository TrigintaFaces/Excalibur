// Copyright (c) Nexus Dynamics. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Excalibur.Dispatch.CloudNative.Serverless.Google;
using Excalibur.Dispatch.CloudNative.Serverless.Google.Framework;
using Excalibur.Dispatch.CloudNative.Serverless.Google.ServiceMesh;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace examples.CloudNative.Serverless.GoogleCloudFunctions.ServiceMesh
 /// <summary>
 /// Example demonstrating traffic splitting and canary deployments with service mesh.
 /// Shows how to gradually roll out new versions of a service using traffic management.
 /// </summary>
 public class TrafficSplittingExample {
 public static async Task Main(string[] args)
 {
 var host = Host.CreateDefaultBuilder(args)
 .ConfigureServices((context, services) =>
 {
 // Configure Google Cloud Functions
 services.AddGoogleCloudFunctions()
 .AddHttpFunction<PaymentServiceFunction>();

 // Configure service mesh with traffic splitting
 services.AddCloudRunServiceMesh(options =>
 {
 options.ServiceName = "payment-service";
 options.Namespace = "production";
 options.Version = Environment.GetEnvironmentVariable("SERVICE_VERSION") ?? "v2";

 options.EnableMTLS = true;
 options.ServiceDiscovery.EnableAutoRegistration = true;
 });

 // Configure traffic management for canary deployment
 services.AddSingleton<IHostedService>(provider =>
 {
 var trafficManager = provider.GetRequiredService<ITrafficManager>();
 var logger = provider.GetRequiredService<ILogger<TrafficSplittingExample>>();

 return new TrafficManagementService(trafficManager, logger);
 });
 })
 .Build();

 await host.RunAsync();
 }
 }

 /// <summary>
 /// Background service that manages traffic splitting configuration.
 /// </summary>
 public class TrafficManagementService : BackgroundService
 {
 private readonly ITrafficManager _trafficManager;
 private readonly ILogger<TrafficManagementService> _logger;

 public TrafficManagementService(
 ITrafficManager trafficManager,
 ILogger<TrafficManagementService> logger)
 {
 _trafficManager = trafficManager;
 _logger = logger;
 }

 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {
 // Wait for service to be ready
 await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

 // Configure canary deployment with gradual traffic shift
 var canaryStages = new[]
 {
 (10, TimeSpan.FromMinutes(5)), // 10% for 5 minutes
 (25, TimeSpan.FromMinutes(10)), // 25% for 10 minutes
 (50, TimeSpan.FromMinutes(15)), // 50% for 15 minutes
 (75, TimeSpan.FromMinutes(10)), // 75% for 10 minutes
 (100, TimeSpan.Zero) // 100% (full rollout)
 };

 foreach (var (percentage, duration) in canaryStages)
 {
 if (stoppingToken.IsCancellationRequested)
 break;

 _logger.LogInformation($"Applying traffic split: {percentage}% to v2");

 // Apply traffic split between v1 and v2
 await _trafficManager.ApplyTrafficSplitAsync(
 "payment-service",
 new List<TrafficSplit>
 {
 new TrafficSplit
 {
 Version = "v1",
 Weight = 100 - percentage,
 Tags = new[] { "stable" }
 },
 new TrafficSplit
 {
 Version = "v2",
 Weight = percentage,
 Tags = new[] { "canary" }
 }
 },
 stoppingToken);

 if (duration > TimeSpan.Zero)
 {
 await Task.Delay(duration, stoppingToken);
 }
 }

 _logger.LogInformation("Canary deployment completed - 100% traffic on v2");
 }
 }

 /// <summary>
 /// Payment service function with version-specific behavior.
 /// </summary>
 public class PaymentServiceFunction : GoogleCloudFunctionBase
 {
 private readonly ILogger<PaymentServiceFunction> _logger;
 private readonly string _version;

 public PaymentServiceFunction(ILogger<PaymentServiceFunction> logger)
 {
 _logger = logger;
 _version = Environment.GetEnvironmentVariable("SERVICE_VERSION") ?? "v1";
 }

 public override async Task<GoogleCloudFunctionResult> ExecuteAsync(GoogleCloudFunctionRequest request, GoogleCloudFunctionExecutionContext context)
 {
 _logger.LogInformation($"Processing payment in version {_version}");

 try
 {
 var payment = await request.ReadAsJsonAsync<PaymentRequest>();

 // Version-specific processing logic
 var result = _version switch
 {
 "v2" => await ProcessPaymentV2(payment), // New implementation
 _ => await ProcessPaymentV1(payment) // Stable implementation
 };

 return GoogleCloudFunctionResult.Ok(result);
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, $"Error processing payment in version {_version}");
 return GoogleCloudFunctionResult.Error("Payment processing failed");
 }
 }

 private async Task<PaymentResult> ProcessPaymentV1(PaymentRequest payment)
 {
 // Simulate v1 processing
 await Task.Delay(100);

 return new PaymentResult
 {
 TransactionId = Guid.NewGuid().ToString(),
 Status = "Completed",
 Version = "v1",
 ProcessingTime = 100
 };
 }

 private async Task<PaymentResult> ProcessPaymentV2(PaymentRequest payment)
 {
 // Simulate v2 processing with improvements
 await Task.Delay(50); // Faster processing

 return new PaymentResult
 {
 TransactionId = Guid.NewGuid().ToString(),
 Status = "Completed",
 Version = "v2",
 ProcessingTime = 50,
 // New feature in v2
 FraudScore = CalculateFraudScore(payment)
 };
 }

 private double CalculateFraudScore(PaymentRequest payment)
 {
 // Simplified fraud scoring for demo
 return payment.Amount > 1000 ? 0.7 : 0.2;
 }
 }

 public class PaymentRequest {
 public string? CustomerId { get; set; }
 public decimal Amount { get; set; }
 public string? Currency { get; set; }
 public string? PaymentMethod { get; set; }
 }

 public class PaymentResult {
 public string? TransactionId { get; set; }
 public string? Status { get; set; }
 public string? Version { get; set; }
 public int ProcessingTime { get; set; }
 public double? FraudScore { get; set; }
 }
}
