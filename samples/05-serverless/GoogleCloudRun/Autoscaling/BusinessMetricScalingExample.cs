// Copyright (c) Nexus Dynamics. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Excalibur.Dispatch.CloudNative.Serverless.Google.Scaling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace examples.CloudNative.Serverless.GoogleCloudRun.Autoscaling
 /// <summary>
 /// Example of custom scaling based on business metrics.
 /// </summary>
 public class BusinessMetricScalingExample {
 public static async Task Main(string[] args)
 {
 var builder = Host.CreateDefaultBuilder(args)
 .ConfigureServices((context, services) =>
 {
 // Add Cloud Run autoscaling
 services.AddCloudRunAutoscaling(context.Configuration);

 // Replace with custom components
 services.AddCustomMetricsCollector<BusinessMetricsCollector>();
 services.AddCustomScalingEngine<BusinessAwareScalingEngine>();

 // Add business services
 services.AddSingleton<IOrderQueue, OrderQueue>();
 services.AddSingleton<IPaymentProcessor, PaymentProcessor>();
 services.AddHostedService<OrderProcessingService>();
 });

 var host = builder.Build();
 await host.RunAsync();
 }
 }

 /// <summary>
 /// Custom metrics collector that includes business metrics.
 /// </summary>
 public class BusinessMetricsCollector : IMetricsCollector
 {
 private readonly ILogger<BusinessMetricsCollector> _logger;
 private readonly IOrderQueue _orderQueue;
 private readonly IPaymentProcessor _paymentProcessor;
 private readonly CloudRunMetricsCollector _baseCollector;
 private MetricsSnapshot _currentSnapshot = new();

 public BusinessMetricsCollector(ILogger<BusinessMetricsCollector> logger,
 IOrderQueue orderQueue,
 IPaymentProcessor paymentProcessor,
 IInstanceManager instanceManager)
 {
 _logger = logger;
 _orderQueue = orderQueue;
 _paymentProcessor = paymentProcessor;
 _baseCollector = new CloudRunMetricsCollector(
 logger.CreateLogger<CloudRunMetricsCollector>(),
 instanceManager);
 }

 public async Task CollectAsync(CancellationToken cancellationToken = default)
 {
 // Collect base metrics
 await _baseCollector.CollectAsync(cancellationToken).ConfigureAwait(false);
 var baseMetrics = await _baseCollector.GetCurrentMetricsAsync(cancellationToken).ConfigureAwait(false);

 // Copy base metrics
 _currentSnapshot = new MetricsSnapshot
 {
 Timestamp = baseMetrics.Timestamp,
 CpuUtilization = baseMetrics.CpuUtilization,
 MemoryUtilization = baseMetrics.MemoryUtilization,
 ConcurrentRequests = baseMetrics.ConcurrentRequests,
 RequestRate = baseMetrics.RequestRate,
 RequestLatencyAvg = baseMetrics.RequestLatencyAvg,
 RequestLatencyP95 = baseMetrics.RequestLatencyP95,
 RequestLatencyP99 = baseMetrics.RequestLatencyP99,
 ErrorRate = baseMetrics.ErrorRate,
 ActiveInstances = baseMetrics.ActiveInstances,
 PendingRequests = baseMetrics.PendingRequests
 };

 // Add business metrics
 _currentSnapshot.CustomMetrics["OrderQueueDepth"] = await _orderQueue.GetQueueDepthAsync().ConfigureAwait(false);
 _currentSnapshot.CustomMetrics["OrderProcessingRate"] = await _orderQueue.GetProcessingRateAsync().ConfigureAwait(false);
 _currentSnapshot.CustomMetrics["AverageOrderValue"] = await _orderQueue.GetAverageOrderValueAsync().ConfigureAwait(false);
 _currentSnapshot.CustomMetrics["PaymentQueueDepth"] = await _paymentProcessor.GetQueueDepthAsync().ConfigureAwait(false);
 _currentSnapshot.CustomMetrics["PaymentSuccessRate"] = await _paymentProcessor.GetSuccessRateAsync().ConfigureAwait(false);
 _currentSnapshot.CustomMetrics["RevenuePerMinute"] = await _paymentProcessor.GetRevenuePerMinuteAsync().ConfigureAwait(false);

 _logger.LogDebug(
 "Collected business metrics: OrderQueue={OrderQueue}, PaymentQueue={PaymentQueue}, Revenue/min=${Revenue}",
 _currentSnapshot.CustomMetrics["OrderQueueDepth"],
 _currentSnapshot.CustomMetrics["PaymentQueueDepth"],
 _currentSnapshot.CustomMetrics["RevenuePerMinute"]);
 }

 public Task<MetricsSnapshot> GetCurrentMetricsAsync(CancellationToken cancellationToken = default)
 {
 return Task.FromResult(_currentSnapshot);
 }
 }

 /// <summary>
 /// Scaling engine that considers business metrics.
 /// </summary>
 public class BusinessAwareScalingEngine : IScalingDecisionEngine
 {
 private readonly ILogger<BusinessAwareScalingEngine> _logger;
 private readonly DefaultScalingDecisionEngine _defaultEngine;

 public BusinessAwareScalingEngine(ILogger<BusinessAwareScalingEngine> logger,
 IOptions<CloudRunAutoscalingConfiguration> configuration)
 {
 _logger = logger;
 _defaultEngine = new DefaultScalingDecisionEngine(
 logger.CreateLogger<DefaultScalingDecisionEngine>(),
 configuration);
 }

 public async Task<ScalingDecision> MakeScalingDecisionAsync(
 MetricsSnapshot metrics,
 int currentInstances,
 ScalingHistory history,
 CancellationToken cancellationToken = default)
 {
 // Get default decision
 var defaultDecision = await _defaultEngine.MakeScalingDecisionAsync(
 metrics, currentInstances, history, cancellationToken).ConfigureAwait(false);

 // Evaluate business metrics
 var businessDecision = EvaluateBusinessMetrics(metrics, currentInstances);

 // Combine decisions
 if (businessDecision.Priority > defaultDecision.Confidence)
 {
 _logger.LogInformation(
 "Business metrics override default scaling: {Reason}",
 businessDecision.Reason);
 return businessDecision;
 }

 return defaultDecision;
 }

 private ScalingDecision EvaluateBusinessMetrics(MetricsSnapshot metrics, int currentInstances)
 {
 var decision = new ScalingDecision
 {
 ShouldScale = false,
 Direction = ScalingDirection.None
 };

 // Check order queue depth
 if (metrics.CustomMetrics.TryGetValue("OrderQueueDepth", out var orderQueue))
 {
 if (orderQueue > 1000)
 {
 decision.ShouldScale = true;
 decision.Direction = ScalingDirection.Up;
 decision.StepSize = Math.Max(2, (int)(orderQueue / 500));
 decision.Confidence = 0.9;
 decision.Priority = 0.95; // High priority
 decision.Reason = $"Order queue critical: {orderQueue} orders pending";
 decision.InfluencingMetrics.Add("OrderQueueDepth");
 return decision;
 }
 }

 // Check revenue impact
 if (metrics.CustomMetrics.TryGetValue("RevenuePerMinute", out var revenue) &&
 metrics.CustomMetrics.TryGetValue("PaymentSuccessRate", out var successRate))
 {
 // If revenue is high and success rate is dropping, scale up
 if (revenue > 10000 && successRate < 95)
 {
 decision.ShouldScale = true;
 decision.Direction = ScalingDirection.Up;
 decision.StepSize = 3;
 decision.Confidence = 0.85;
 decision.Priority = 0.9;
 decision.Reason = $"Revenue at risk: ${revenue}/min with {successRate}% success rate";
 decision.InfluencingMetrics.Add("RevenuePerMinute");
 decision.InfluencingMetrics.Add("PaymentSuccessRate");
 return decision;
 }
 }

 // Check for over-provisioning during low revenue periods
 if (metrics.CustomMetrics.TryGetValue("RevenuePerMinute", out revenue) &&
 revenue < 100 && currentInstances > 5)
 {
 decision.ShouldScale = true;
 decision.Direction = ScalingDirection.Down;
 decision.StepSize = 1;
 decision.Confidence = 0.7;
 decision.Priority = 0.6;
 decision.Reason = $"Low revenue period: ${revenue}/min";
 decision.InfluencingMetrics.Add("RevenuePerMinute");
 }

 return decision;
 }
 }

 /// <summary>
 /// Extended scaling decision with business priority.
 /// </summary>
 public static class ScalingDecisionExtensions {
 public static double Priority { get; set; }
 }

 // Business service interfaces
 public interface IOrderQueue {
 Task<int> GetQueueDepthAsync();
 Task<double> GetProcessingRateAsync();
 Task<double> GetAverageOrderValueAsync();
 }

 public interface IPaymentProcessor {
 Task<int> GetQueueDepthAsync();
 Task<double> GetSuccessRateAsync();
 Task<double> GetRevenuePerMinuteAsync();
 }

 // Mock implementations
 public class OrderQueue : IOrderQueue
 {
 private readonly Random _random = new();

 public Task<int> GetQueueDepthAsync()
 {
 // Simulate varying queue depth
 var hour = DateTime.UtcNow.Hour;
 var baseDepth = hour >= 12 && hour <= 14 ? 800 : 200; // Lunch rush
 return Task.FromResult(baseDepth + _random.Next(200));
 }

 public Task<double> GetProcessingRateAsync()
 {
 return Task.FromResult(50.0 + _random.Next(20));
 }

 public Task<double> GetAverageOrderValueAsync()
 {
 return Task.FromResult(35.0 + _random.Next(15));
 }
 }

 public class PaymentProcessor : IPaymentProcessor
 {
 private readonly Random _random = new();

 public Task<int> GetQueueDepthAsync()
 {
 return Task.FromResult(_random.Next(100));
 }

 public Task<double> GetSuccessRateAsync()
 {
 // Simulate degrading success rate under load
 var queueDepth = _random.Next(100);
 var successRate = queueDepth > 80 ? 92.0 : 98.0;
 return Task.FromResult(successRate);
 }

 public Task<double> GetRevenuePerMinuteAsync()
 {
 var hour = DateTime.UtcNow.Hour;
 var baseRevenue = hour >= 12 && hour <= 14 ? 15000 : 5000;
 return Task.FromResult((double)(baseRevenue + _random.Next(2000)));
 }
 }

 public class OrderProcessingService : BackgroundService
 {
 private readonly ILogger<OrderProcessingService> _logger;

 public OrderProcessingService(ILogger<OrderProcessingService> logger)
 {
 _logger = logger;
 }

 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {
 while (!stoppingToken.IsCancellationRequested)
 {
 _logger.LogInformation("Processing orders...");
 await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
 }
 }
 }
}
