// Copyright (c) Nexus Dynamics. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Excalibur.Dispatch.CloudNative.Serverless.Google;
using Excalibur.Dispatch.CloudNative.Serverless.Google.Framework;
using Excalibur.Dispatch.CloudNative.Serverless.Google.ServiceMesh;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace examples.CloudNative.Serverless.GoogleCloudFunctions.ServiceMesh
 /// <summary>
 /// Example demonstrating basic service mesh setup with Google Cloud Run.
 /// This example shows how to configure a service to participate in a service mesh
 /// with mTLS, service discovery, and basic traffic management.
 /// </summary>
 public class BasicServiceMeshExample {
 public static async Task Main(string[] args)
 {
 var host = Host.CreateDefaultBuilder(args)
 .ConfigureServices((context, services) =>
 {
 // Configure Google Cloud Functions framework
 services.AddGoogleCloudFunctions()
 .AddHttpFunction<OrderServiceFunction>();

 // Configure service mesh integration
 services.AddCloudRunServiceMesh(options =>
 {
 options.ServiceName = "order-service";
 options.Namespace = "production";
 options.Version = "v1";

 // Enable mTLS for secure service-to-service communication
 options.EnableMTLS = true;
 options.MTLSMode = MTLSMode.Strict;

 // Configure service discovery
 options.ServiceDiscovery.EnableAutoRegistration = true;
 options.ServiceDiscovery.HealthCheckPath = "/health";
 options.ServiceDiscovery.HealthCheckInterval = TimeSpan.FromSeconds(30);

 // Configure basic traffic management
 options.Traffic.LoadBalancingPolicy = LoadBalancingPolicy.RoundRobin;
 options.Traffic.ConnectionPooling = new ConnectionPoolingConfiguration
 {
 MaxConnections = 100,
 MaxPendingRequests = 50,
 ConnectionTimeout = TimeSpan.FromSeconds(30)
 };

 // Configure retry policy
 options.RetryPolicy.MaxAttempts = 3;
 options.RetryPolicy.BackoffInterval = TimeSpan.FromMilliseconds(250);
 options.RetryPolicy.BackoffMultiplier = 2.0;

 // Configure circuit breaker
 options.CircuitBreaker.ConsecutiveErrors = 5;
 options.CircuitBreaker.Interval = TimeSpan.FromSeconds(10);
 options.CircuitBreaker.BaseEjectionTime = TimeSpan.FromSeconds(30);
 });
 })
 .Build();

 await host.RunAsync();
 }
 }

 /// <summary>
 /// Example HTTP function that processes orders.
 /// </summary>
 public class OrderServiceFunction : GoogleCloudFunctionBase
 {
 private readonly ILogger<OrderServiceFunction> _logger;

 public OrderServiceFunction(ILogger<OrderServiceFunction> logger)
 {
 _logger = logger;
 }

 public override async Task<GoogleCloudFunctionResult> ExecuteAsync(GoogleCloudFunctionRequest request, GoogleCloudFunctionExecutionContext context)
 {
 _logger.LogInformation("Processing order request");

 try
 {
 // Process the order
 var order = await request.ReadAsJsonAsync<Order>();

 // In a real service mesh scenario, this might call other services
 // The service mesh handles load balancing, retries, and circuit breaking

 var result = new OrderResult
 {
 OrderId = Guid.NewGuid().ToString(),
 Status = "Processed",
 ProcessedAt = DateTime.UtcNow
 };

 return GoogleCloudFunctionResult.Ok(result);
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Error processing order");
 return GoogleCloudFunctionResult.Error("Failed to process order");
 }
 }
 }

 public class Order {
 public string? CustomerId { get; set; }
 public string? ProductId { get; set; }
 public int Quantity { get; set; }
 public decimal Price { get; set; }
 }

 public class OrderResult {
 public string? OrderId { get; set; }
 public string? Status { get; set; }
 public DateTime ProcessedAt { get; set; }
 }
}
