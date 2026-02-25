// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Excalibur.Dispatch.CloudNative.Core;
using Excalibur.Dispatch.CloudNative.Patterns.Sagas.Extensions;
using Excalibur.Dispatch.CloudNative.Patterns.Sagas.Implementation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace examples.Dispatch.Examples.Patterns.Sagas;

/// <summary>
/// Example demonstrating saga orchestration with caching.
/// </summary>
public class SagaOrchestrationExample {
 public static async Task Main(string[] args)
 {
 var host = Host.CreateDefaultBuilder(args)
 .ConfigureServices((context, services) =>
 {
 // Add cloud-native dispatch with caching
 services.AddCloudNativeDispatch(options =>
 {
 options.UseRedis(context.Configuration.GetConnectionString("Redis") 
 ?? "localhost:6379");
 });

 // Add distributed caching
 services.AddDistributedCaching(options =>
 {
 options.DefaultCacheDuration = TimeSpan.FromMinutes(5);
 });

 // Add saga orchestration with caching
 services.AddSagaOrchestration(options =>
 {
 options.DefaultTimeout = TimeSpan.FromMinutes(10);
 options.EnableStateCleanup = true;
 options.StateRetentionPeriod = TimeSpan.FromDays(3);
 });

 // Register the example worker
 services.AddHostedService<SagaExampleWorker>();
 })
 .Build();

 await host.RunAsync();
 }
}

/// <summary>
/// Order processing data for the saga.
/// </summary>
public class OrderData {
 public string OrderId { get; set; } = string.Empty;
 public string CustomerId { get; set; } = string.Empty;
 public decimal TotalAmount { get; set; }
 public string[] ProductIds { get; set; } = Array.Empty<string>();
 public string PaymentMethod { get; set; } = string.Empty;
}
