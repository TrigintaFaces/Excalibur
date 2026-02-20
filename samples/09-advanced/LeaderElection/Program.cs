// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

// Leader Election Sample
// ======================
// This sample demonstrates distributed leader election with Redis:
// - Redis leader election configuration
// - Single-leader election lifecycle
// - Leadership change callbacks
// - Graceful leadership release on shutdown
// - Leadership renewal (auto-renewal via RenewInterval)
//
// Prerequisites:
// 1. Start Redis: docker-compose up -d
// 2. Run the sample: dotnet run

#pragma warning disable CA1303 // Sample code uses literal strings

using Excalibur.Dispatch.LeaderElection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using StackExchange.Redis;

// Build configuration
var builder = new HostApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// Configure logging for visibility
builder.Services.AddLogging(logging =>
{
	_ = logging.AddConsole();
	_ = logging.SetMinimumLevel(LogLevel.Debug);
});

// ============================================================
// Configure Redis Connection
// ============================================================
var redisConnectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
	var logger = sp.GetRequiredService<ILogger<Program>>();
	logger.LogInformation("Connecting to Redis at {Endpoint}...", redisConnectionString);

	try
	{
		var connection = ConnectionMultiplexer.Connect(redisConnectionString);
		logger.LogInformation("Connected to Redis successfully");
		return connection;
	}
	catch (RedisConnectionException ex)
	{
		logger.LogError(ex, "Failed to connect to Redis. Ensure Redis is running (docker-compose up -d)");
		throw;
	}
});

// ============================================================
// Configure Leader Election
// ============================================================
var lockKey = builder.Configuration["LeaderElection:LockKey"] ?? "myapp:leader";
var leaseDuration = TimeSpan.FromSeconds(
	builder.Configuration.GetValue("LeaderElection:LeaseDurationSeconds", 30));
var renewInterval = TimeSpan.FromSeconds(
	builder.Configuration.GetValue("LeaderElection:RenewIntervalSeconds", 10));
var gracePeriod = TimeSpan.FromSeconds(
	builder.Configuration.GetValue("LeaderElection:GracePeriodSeconds", 15));

builder.Services.AddRedisLeaderElection(lockKey, options =>
{
	options.LeaseDuration = leaseDuration;
	options.RenewInterval = renewInterval;
	options.GracePeriod = gracePeriod;
	options.InstanceId = $"Instance-{Environment.ProcessId}";
});

// ============================================================
// Build and start the host
// ============================================================
using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var leaderElection = host.Services.GetRequiredService<ILeaderElection>();

logger.LogInformation("=================================================");
logger.LogInformation("  Leader Election Sample");
logger.LogInformation("=================================================");
logger.LogInformation("");

await host.StartAsync().ConfigureAwait(false);

// ============================================================
// Demo 1: Subscribe to Leadership Events
// ============================================================
logger.LogInformation("=== Demo 1: Subscribe to Leadership Events ===");
logger.LogInformation("");

logger.LogInformation("Candidate ID: {CandidateId}", leaderElection.CandidateId);
logger.LogInformation("Lock Key: {LockKey}", lockKey);
logger.LogInformation("Lease Duration: {LeaseDuration}", leaseDuration);
logger.LogInformation("Renew Interval: {RenewInterval}", renewInterval);
logger.LogInformation("Grace Period: {GracePeriod}", gracePeriod);
logger.LogInformation("");

// Subscribe to leadership events
leaderElection.BecameLeader += (sender, args) =>
{
	logger.LogInformation("");
	logger.LogInformation("*** LEADERSHIP ACQUIRED ***");
	logger.LogInformation("  Candidate: {CandidateId}", args.CandidateId);
	logger.LogInformation("  Resource: {ResourceName}", args.ResourceName);
	logger.LogInformation("  This instance is now the leader!");
	logger.LogInformation("");
};

leaderElection.LostLeadership += (sender, args) =>
{
	logger.LogInformation("");
	logger.LogWarning("*** LEADERSHIP LOST ***");
	logger.LogWarning("  Candidate: {CandidateId}", args.CandidateId);
	logger.LogWarning("  Resource: {ResourceName}", args.ResourceName);
	logger.LogWarning("  This instance is no longer the leader.");
	logger.LogInformation("");
};

leaderElection.LeaderChanged += (sender, args) =>
{
	logger.LogInformation("");
	logger.LogInformation("*** LEADER CHANGED ***");
	logger.LogInformation("  Previous Leader: {PreviousLeader}", args.PreviousLeaderId ?? "(none)");
	logger.LogInformation("  New Leader: {NewLeader}", args.NewLeaderId ?? "(none)");
	logger.LogInformation("  Resource: {ResourceName}", args.ResourceName);
	logger.LogInformation("");
};

logger.LogInformation("Event handlers registered for:");
logger.LogInformation("  - BecameLeader");
logger.LogInformation("  - LostLeadership");
logger.LogInformation("  - LeaderChanged");

// ============================================================
// Demo 2: Start Leader Election
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Demo 2: Start Leader Election ===");
logger.LogInformation("");

logger.LogInformation("Starting leader election...");
await leaderElection.StartAsync(CancellationToken.None).ConfigureAwait(false);

// Wait briefly for election result
await Task.Delay(1000).ConfigureAwait(false);

logger.LogInformation("");
logger.LogInformation("Initial election result:");
logger.LogInformation("  Is Leader: {IsLeader}", leaderElection.IsLeader);
logger.LogInformation("  Current Leader: {CurrentLeader}", leaderElection.CurrentLeaderId ?? "(unknown)");

// ============================================================
// Demo 3: Monitor Leadership Status
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Demo 3: Monitor Leadership Status ===");
logger.LogInformation("");

logger.LogInformation("Monitoring leadership status for 10 seconds...");
logger.LogInformation("(The lease will be automatically renewed every {RenewInterval})", renewInterval);
logger.LogInformation("");

for (var i = 1; i <= 5; i++)
{
	await Task.Delay(2000).ConfigureAwait(false);
	logger.LogInformation("Status check {Check}/5:", i);
	logger.LogInformation("  Is Leader: {IsLeader}", leaderElection.IsLeader);
	logger.LogInformation("  Current Leader: {CurrentLeader}", leaderElection.CurrentLeaderId ?? "(unknown)");
}

// ============================================================
// Demo 4: Demonstrate Leader Work Pattern
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Demo 4: Leader Work Pattern ===");
logger.LogInformation("");

if (leaderElection.IsLeader)
{
	logger.LogInformation("This instance is the leader. Performing leader-only work...");
	logger.LogInformation("");

	// Simulate leader-only work
	for (var i = 1; i <= 3; i++)
	{
		if (!leaderElection.IsLeader)
		{
			logger.LogWarning("Lost leadership during work execution. Stopping...");
			break;
		}

		logger.LogInformation("  Processing batch {Batch}/3...", i);
		await Task.Delay(1000).ConfigureAwait(false);
		logger.LogInformation("  Batch {Batch} completed.", i);
	}

	logger.LogInformation("");
	logger.LogInformation("Leader work pattern demonstration complete.");
}
else
{
	logger.LogInformation("This instance is NOT the leader.");
	logger.LogInformation("Current leader is: {CurrentLeader}", leaderElection.CurrentLeaderId ?? "(unknown)");
	logger.LogInformation("");
	logger.LogInformation("In a real application, this instance would:");
	logger.LogInformation("  - Wait for leadership opportunity");
	logger.LogInformation("  - Handle follower responsibilities");
	logger.LogInformation("  - Be ready to take over if the leader fails");
}

// ============================================================
// Demo 5: Graceful Shutdown
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Demo 5: Graceful Shutdown ===");
logger.LogInformation("");

logger.LogInformation("Demonstrating graceful leadership release...");
logger.LogInformation("Stopping leader election...");

await leaderElection.StopAsync(CancellationToken.None).ConfigureAwait(false);

logger.LogInformation("");
logger.LogInformation("After stop:");
logger.LogInformation("  Is Leader: {IsLeader}", leaderElection.IsLeader);
logger.LogInformation("  Current Leader: {CurrentLeader}", leaderElection.CurrentLeaderId ?? "(none)");

// ============================================================
// Multi-Instance Simulation Info
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Multi-Instance Simulation ===");
logger.LogInformation("");
logger.LogInformation("To see leader election in action with multiple instances:");
logger.LogInformation("");
logger.LogInformation("  Terminal 1: dotnet run");
logger.LogInformation("  Terminal 2: dotnet run");
logger.LogInformation("  Terminal 3: dotnet run");
logger.LogInformation("");
logger.LogInformation("Only one instance will become the leader.");
logger.LogInformation("Stop the leader instance and watch another take over.");
logger.LogInformation("");

// ============================================================
// Configuration Reference
// ============================================================
logger.LogInformation("=== Configuration Reference ===");
logger.LogInformation("");
logger.LogInformation("| Option | Default | Description |");
logger.LogInformation("|--------|---------|-------------|");
logger.LogInformation("| LeaseDuration | 15s | Time before the lock expires |");
logger.LogInformation("| RenewInterval | 5s | How often to renew the lease |");
logger.LogInformation("| GracePeriod | 5s | Time before declaring leader dead |");
logger.LogInformation("| InstanceId | MachineName | Unique identifier for this candidate |");
logger.LogInformation("");
logger.LogInformation("Redis Commands to Inspect State:");
logger.LogInformation("  docker exec -it leader-election-redis redis-cli");
logger.LogInformation("  > KEYS *leader*");
logger.LogInformation("  > GET myapp:leader");
logger.LogInformation("  > TTL myapp:leader");
logger.LogInformation("");
logger.LogInformation("Provider Comparison:");
logger.LogInformation("| Provider | Best For | Infrastructure |");
logger.LogInformation("|----------|----------|----------------|");
logger.LogInformation("| Redis | High availability, fast failover | Redis cluster |");
logger.LogInformation("| SQL Server | Existing SQL infrastructure | SQL Server |");
logger.LogInformation("| Kubernetes | K8s-native deployments | Kubernetes API |");
logger.LogInformation("| Consul | Service mesh integration | Consul cluster |");
logger.LogInformation("");

logger.LogInformation("Sample completed. Press Ctrl+C to exit...");

await host.WaitForShutdownAsync().ConfigureAwait(false);

#pragma warning restore CA1303
