using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Excalibur.Dispatch.CloudNative.Core.Scheduling.Distributed;
using Excalibur.Dispatch.CloudNative.Core.Scheduling.Coordination.Extensions;

namespace examples.DistributedScheduling;

/// <summary>
/// Example demonstrating distributed scheduling coordination across multiple nodes.
/// </summary>
public class DistributedSchedulingExample {
 public static async Task Main(string[] args)
 {
 // Simulate multiple nodes by running with different node IDs
 var nodeId = args.Length > 0 ? args[0] : "node-1";

 var host = Host.CreateDefaultBuilder(args)
 .ConfigureServices((context, services) =>
 {
 // Add distributed scheduling coordination
 services.AddRedisSchedulingCoordination(options =>
 {
 options.ConnectionString = "localhost:6379";
 options.KeyPrefix = "example:scheduling:";
 options.MaxSchedulesPerNode = 50;
 options.HeartbeatInterval = TimeSpan.FromSeconds(10);
 options.LeaderLeaseDuration = TimeSpan.FromMinutes(1);
 });

 // Add the example hosted service
 services.AddHostedService<SchedulingNodeService>();
 services.AddSingleton(new NodeContext { NodeId = nodeId });
 })
 .Build();

 await host.RunAsync();
 }
}

/// <summary>
/// Context for the current node.
/// </summary>
public class NodeContext {
 public string NodeId { get; set; } = string.Empty;
}

/// <summary>
/// Background service that demonstrates distributed scheduling coordination.
/// </summary>
public class SchedulingNodeService : BackgroundService
{
 private readonly IDistributedSchedulingCoordinator _coordinator;
 private readonly ILogger<SchedulingNodeService> _logger;
 private readonly NodeContext _nodeContext;
 private readonly List<string> _ownedSchedules = new();

 public SchedulingNodeService(
 IDistributedSchedulingCoordinator coordinator,
 ILogger<SchedulingNodeService> logger,
 NodeContext nodeContext)
 {
 _coordinator = coordinator;
 _logger = logger;
 _nodeContext = nodeContext;

 // Subscribe to coordination events
 _coordinator.ScheduleOwnershipChanged += OnScheduleOwnershipChanged;
 _coordinator.LeadershipChanged += OnLeadershipChanged;
 _coordinator.ClusterMembershipChanged += OnClusterMembershipChanged;
 }

 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {
 _logger.LogInformation("Starting scheduling node {NodeId}", _nodeContext.NodeId);

 // Try to become leader
 var isLeader = await _coordinator.TryBecomeLeaderAsync(stoppingToken);
 _logger.LogInformation("Leadership attempt result: {IsLeader}", isLeader);

 // Main processing loop
 while (!stoppingToken.IsCancellationRequested)
 {
 try
 {
 // Process owned schedules
 await ProcessOwnedSchedulesAsync(stoppingToken);

 // If leader, perform coordination tasks
 if (await _coordinator.IsLeaderAsync(stoppingToken))
 {
 await PerformLeaderTasksAsync(stoppingToken);
 }

 // Wait before next iteration
 await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Error in scheduling loop");
 await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
 }
 }
 }

 private async Task ProcessOwnedSchedulesAsync(CancellationToken cancellationToken)
 {
 foreach (var scheduleId in _ownedSchedules.ToList())
 {
 // Try to acquire lock for processing
 if (await _coordinator.TryAcquireScheduleLockAsync(scheduleId, TimeSpan.FromMinutes(1), cancellationToken))
 {
 try
 {
 _logger.LogInformation("Processing schedule {ScheduleId}", scheduleId);

 // Simulate schedule processing
 await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

 // Extend lock if needed
 await _coordinator.ExtendScheduleLockAsync(scheduleId, TimeSpan.FromMinutes(1), cancellationToken);
 }
 finally
 {
 // Release lock
 await _coordinator.ReleaseScheduleLockAsync(scheduleId, cancellationToken);
 }
 }
 else
 {
 _logger.LogWarning("Could not acquire lock for schedule {ScheduleId}", scheduleId);
 }
 }
 }

 private async Task PerformLeaderTasksAsync(CancellationToken cancellationToken)
 {
 _logger.LogInformation("Performing leader tasks");

 // Check for orphaned schedules
 var orphaned = await _coordinator.GetOrphanedSchedulesAsync(cancellationToken);
 foreach (var scheduleId in orphaned)
 {
 _logger.LogWarning("Found orphaned schedule {ScheduleId}, reassigning", scheduleId);

 // Find node with lowest load
 var nodeLoads = await _coordinator.GetNodeLoadInfoAsync(cancellationToken);
 var targetNode = nodeLoads
 .OrderBy(n => n.Value.LoadFactor)
 .FirstOrDefault()
 .Key;

 if (!string.IsNullOrEmpty(targetNode))
 {
 // Register new ownership
 await _coordinator.RegisterScheduleOwnershipAsync(new[] { scheduleId }, cancellationToken);
 }
 }

 // Check load balancing
 await CheckLoadBalancingAsync(cancellationToken);
 }

 private async Task CheckLoadBalancingAsync(CancellationToken cancellationToken)
 {
 var nodeLoads = await _coordinator.GetNodeLoadInfoAsync(cancellationToken);
 if (nodeLoads.Count < 2) return;

 var maxLoad = nodeLoads.Values.Max(n => n.LoadFactor);
 var minLoad = nodeLoads.Values.Min(n => n.LoadFactor);

 // If load difference is significant, rebalance
 if (maxLoad - minLoad > 0.3)
 {
 _logger.LogInformation("Load imbalance detected: {MaxLoad} vs {MinLoad}", maxLoad, minLoad);

 var overloadedNode = nodeLoads.First(n => n.Value.LoadFactor == maxLoad).Key;
 var underloadedNode = nodeLoads.First(n => n.Value.LoadFactor == minLoad).Key;

 // Transfer some schedules
 var schedulesToTransfer = _ownedSchedules
 .Take((int)((maxLoad - minLoad) * 10))
 .ToList();

 if (schedulesToTransfer.Any())
 {
 var success = await _coordinator.TransferScheduleOwnershipAsync(
 schedulesToTransfer,
 underloadedNode,
 cancellationToken);

 if (success)
 {
 _logger.LogInformation(
 "Transferred {Count} schedules from {From} to {To}",
 schedulesToTransfer.Count,
 overloadedNode,
 underloadedNode);
 }
 }
 }
 }

 private void OnScheduleOwnershipChanged(object? sender, ScheduleOwnershipChangedEventArgs e)
 {
 _logger.LogInformation(
 "Schedule ownership changed: {ScheduleId} from {Previous} to {New} (Reason: {Reason})",
 e.ScheduleId,
 e.PreviousOwner ?? "none",
 e.NewOwner ?? "none",
 e.Reason);

 // Update local ownership list
 if (e.NewOwner == _nodeContext.NodeId)
 {
 _ownedSchedules.Add(e.ScheduleId);
 }
 else if (e.PreviousOwner == _nodeContext.NodeId)
 {
 _ownedSchedules.Remove(e.ScheduleId);
 }
 }

 private void OnLeadershipChanged(object? sender, LeadershipChangedEventArgs e)
 {
 _logger.LogInformation(
 "Leadership changed from {Previous} to {New}",
 e.PreviousLeader ?? "none",
 e.NewLeader ?? "none");
 }

 private void OnClusterMembershipChanged(object? sender, ClusterMembershipChangedEventArgs e)
 {
 _logger.LogInformation(
 "Cluster membership changed: {NodeId} {ChangeType}",
 e.NodeId,
 e.ChangeType);
 }

 public override async Task StopAsync(CancellationToken cancellationToken)
 {
 _logger.LogInformation("Stopping scheduling node {NodeId}", _nodeContext.NodeId);

 // Renounce leadership if leader
 if (await _coordinator.IsLeaderAsync(cancellationToken))
 {
 await _coordinator.RenounceLeadershipAsync(cancellationToken);
 }

 await base.StopAsync(cancellationToken);
 }
}
