// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Excalibur.Dispatch.CloudNative.Core;
using Excalibur.Dispatch.CloudNative.Scheduling;
using Excalibur.Dispatch.CloudNative.Scheduling.Extensions;
using Excalibur.Dispatch.CloudNative.Scheduling.Persistence.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace examples.Excalibur.Dispatch.Examples.Scheduling.Persistence;

/// <summary>
/// Example demonstrating schedule persistence and recovery.
/// </summary>
public class SchedulePersistenceExample {
 public static async Task Main(string[] args)
 {
 var host = Host.CreateDefaultBuilder(args)
 .ConfigureServices((context, services) =>
 {
 // Add cloud-native dispatch with Redis
 services.AddCloudNativeDispatch(options =>
 {
 options.UseRedis(context.Configuration.GetConnectionString("Redis")
 ?? "localhost:6379");
 });

 // Add distributed scheduling
 services.AddDistributedScheduling(options =>
 {
 options.NodeId = $"node-{Environment.MachineName}";
 });

 // Add schedule persistence with recovery
 services.AddSchedulePersistence(
 SchedulePersistenceProvider.Redis,
 recoveryOptions =>
 {
 recoveryOptions.EnableAutomaticRecovery = true;
 recoveryOptions.RecoveryCheckInterval = TimeSpan.FromSeconds(30);
 recoveryOptions.MaxRecoveryBatchSize = 50;
 });

 // Add the example worker
 services.AddHostedService<PersistenceExampleWorker>();
 })
 .Build();

 await host.RunAsync();
 }
}
