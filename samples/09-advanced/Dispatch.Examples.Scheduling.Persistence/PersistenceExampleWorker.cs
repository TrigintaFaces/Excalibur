// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Excalibur.Dispatch.CloudNative.Scheduling.Abstractions;
using Excalibur.Dispatch.CloudNative.Scheduling.Persistence.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace examples.Excalibur.Dispatch.Examples.Scheduling.Persistence;

/// <summary>
/// Example worker demonstrating schedule persistence features.
/// </summary>
public class PersistenceExampleWorker : BackgroundService
{
 private readonly IMessageScheduler _scheduler;
 private readonly ISchedulePersistenceProvider _persistenceProvider;
 private readonly IScheduleMigrationService _migrationService;
 private readonly IScheduleBackupService _backupService;
 private readonly ILogger<PersistenceExampleWorker> _logger;

 public PersistenceExampleWorker(
 IMessageScheduler scheduler,
 ISchedulePersistenceProvider persistenceProvider,
 IScheduleMigrationService migrationService,
 IScheduleBackupService backupService,
 ILogger<PersistenceExampleWorker> logger)
 {
 _scheduler = scheduler;
 _persistenceProvider = persistenceProvider;
 _migrationService = migrationService;
 _backupService = backupService;
 _logger = logger;
 }

 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {
 // Example 1: Create and persist a schedule
 var scheduleId = await CreatePersistedScheduleAsync();
 _logger.LogInformation("Created persisted schedule: {ScheduleId}", scheduleId);

 // Example 2: Check schedule state
 await CheckScheduleStateAsync(scheduleId);

 // Example 3: Simulate node failure and recovery
 await SimulateNodeFailureAndRecoveryAsync();

 // Example 4: Backup all schedules
 await BackupSchedulesAsync();

 // Example 5: Monitor schedule execution history
 await MonitorExecutionHistoryAsync(scheduleId, stoppingToken);
 }

 private async Task<string> CreatePersistedScheduleAsync()
 {
 var message = new
 {
 Type = "HealthCheck",
 Timestamp = DateTime.UtcNow
 };

 // Schedule a recurring health check every minute
 var scheduleId = await _scheduler.ScheduleRecurringAsync(
 message,
 "0 * * * * *", // Every minute
 new ScheduleOptions
 {
 TimeZone = TimeZoneInfo.Utc,
 Description = "Persisted health check schedule"
 });

 _logger.LogInformation("Created recurring schedule with persistence");
 return scheduleId;
 }

 private async Task CheckScheduleStateAsync(string scheduleId)
 {
 var state = await _persistenceProvider.GetScheduleStateAsync(scheduleId);
 if (state != null)
 {
 _logger.LogInformation(
 "Schedule state - ID: {Id}, Node: {Node}, Status: {Status}, Next: {Next}",
 state.ScheduleId, state.NodeId, state.Status, state.NextExecutionAt);
 }
 }

 private async Task SimulateNodeFailureAndRecoveryAsync()
 {
 _logger.LogInformation("Simulating node failure scenario...");

 // Get orphaned schedules (simulating inactive nodes)
 var orphaned = await _persistenceProvider.GetOrphanedSchedulesAsync(
 new[] { Environment.MachineName });

 _logger.LogInformation("Found {Count} orphaned schedules",
 orphaned.Count());

 // Recovery will happen automatically via ScheduleRecoveryService
 }

 private async Task BackupSchedulesAsync()
 {
 var backupPath = $"schedules-backup-{DateTime.UtcNow:yyyyMMddHHmmss}.zip";
 var result = await _backupService.BackupSchedulesAsync(backupPath);

 if (result.Success)
 {
 _logger.LogInformation(
 "Backed up {Count} schedules to {Path}",
 result.ScheduleCount, result.BackupPath);
 }
 }

 private async Task MonitorExecutionHistoryAsync(string scheduleId, CancellationToken stoppingToken)
 {
 while (!stoppingToken.IsCancellationRequested)
 {
 var history = await _persistenceProvider.GetExecutionHistoryAsync(scheduleId, 10);

 _logger.LogInformation(
 "Schedule {Id} - Total executions: {Total}, Recent: {Recent}",
 scheduleId, history.TotalExecutions, history.Executions.Count);

 await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
 }
 }
}
