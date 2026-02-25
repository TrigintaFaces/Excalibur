// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Excalibur.Dispatch.CloudNative.Caching.Extensions;
using Excalibur.Dispatch.CloudNative.Core.Extensions;
using Excalibur.Dispatch.CloudNative.Patterns.Sagas.Abstractions;
using Excalibur.Dispatch.CloudNative.Patterns.Sagas.Extensions;
using Excalibur.Dispatch.CloudNative.Patterns.Sagas.Models;
using examples.Dispatch.Examples.Patterns.Sagas.OrderProcessing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace examples.Dispatch.Examples.Patterns.Sagas;

/// <summary>
/// Example demonstrating saga orchestration with caching.
/// </summary>
public class Program {
 public static async Task Main(string[] args)
 {
 var host = Host.CreateDefaultBuilder(args)
 .ConfigureServices((context, services) =>
 {
 // Add cloud-native core services
 services.AddCloudNativeCore();

 // Add distributed caching (using Redis)
 services.AddDistributedCaching(options =>
 {
 options.UseRedis(context.Configuration.GetConnectionString("Redis") 
 ?? "localhost:6379");
 });

 // Add saga orchestration with caching
 services.AddSagaOrchestration(options =>
 {
 options.UseInMemoryStore = true; // For demo purposes
 options.EnableCaching = true;
 options.CacheTtl = TimeSpan.FromMinutes(5);
 });

 // Register saga steps
 services.AddSagaStep<ReserveInventoryStep, OrderSagaData>();
 services.AddSagaStep<ProcessPaymentStep, OrderSagaData>();
 services.AddSagaStep<CreateShipmentStep, OrderSagaData>();
 services.AddSagaStep<SendConfirmationStep, OrderSagaData>();

 // Add the example worker
 services.AddHostedService<OrderSagaExampleWorker>();
 })
 .Build();

 await host.RunAsync();
 }
}
