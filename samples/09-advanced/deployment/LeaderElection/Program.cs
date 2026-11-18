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
#pragma warning disable CA1506 // Excessive class coupling — sample wires full DI stack
// CA1505 measures the maintainability index of the compiler-synthesized `Program` that holds this
// file's top-level statements. The sample is deliberately one linear narrative a reader follows
// top-to-bottom, so the index is reporting its teaching structure rather than a defect: extracting
// helper methods to raise it would raise the number and make the sample harder to read, which is the
// only thing it is for. Scoped to this file, for the same reason as CA1506 above.
#pragma warning disable CA1505 // Maintainability index — sample is one linear narrative by design

using System.Runtime.ExceptionServices;

using Excalibur.Dispatch.LeaderElection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Build configuration
var builder = new HostApplicationBuilder(args);

// Configure logging for visibility
builder.Services.AddLogging(logging =>
{
	_ = logging.AddConsole();
	_ = logging.SetMinimumLevel(LogLevel.Debug);
});

// ============================================================
// Configure Redis Connection
// ============================================================
// Use one configuration value for the framework-owned connection. Do not log the
// connection string: it may contain credentials or other connection options.
var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? builder.Configuration["Redis:ConnectionString"]
    ?? "localhost:6379";

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

builder.Services.AddExcalibur(excalibur =>
	excalibur.AddLeaderElection(le => le
		.UseRedis(redis => redis
			.ConnectionString(redisConnectionString)
			.LockKey(lockKey))
		.WithOptions(options =>
		{
			options.LeaseDuration = leaseDuration;
			options.RenewInterval = renewInterval;
			options.GracePeriod = gracePeriod;
			options.InstanceId = $"{Environment.MachineName}-{Environment.ProcessId}";
		})));

// ============================================================
// Build and start the host
// ============================================================
var host = builder.Build();

// Ownership begins immediately after Build, including dependency resolution and startup logging.
await RunAndShutdownAsync(host, RunDemoAsync, StopHostAsync).ConfigureAwait(false);

async Task RunDemoAsync(ILeaderElection leaderElection, ILogger<Program> logger)
{
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
		logger.LogInformation("  Acquisition fence: {FencingToken}", args.FencingToken);
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
	logger.LogInformation("(Renewal requests use a {RenewInterval} request-start cadence)", renewInterval);
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

	// Pin one complete acquisition snapshot. Never replace its token mid-batch with
	// the token from a newer tenure: that would let old work impersonate the new owner.
	if (leaderElection.CurrentLeadership is { } leadership && leadership.FencingToken is { } fencingToken)
	{
		logger.LogInformation("Simulating work under acquisition fence {FencingToken}", fencingToken);
		logger.LogInformation("No protected writes are performed by this simulation.");
		for (var i = 1; i <= 3; i++)
		{
			if (leaderElection.CurrentLeadership != leadership)
			{
				logger.LogWarning("The pinned tenure ended. Abandoning its remaining simulated batches.");
				break;
			}

			// For real work, pass fencingToken to the protected resource. The resource must
			// atomically check/advance its high-water mark AND apply the write in the same
			// operation/transaction. ValidateTokenAsync followed by a separate write races.
			logger.LogInformation("  Simulating batch {Batch}/3 with pinned fence {FencingToken}", i, fencingToken);
			await Task.Delay(1000).ConfigureAwait(false);
			if (leaderElection.CurrentLeadership != leadership)
			{
				logger.LogWarning("The pinned tenure ended during the simulated batch.");
				break;
			}
			logger.LogInformation("  Simulated batch {Batch} completed with fence {FencingToken}", i, fencingToken);
		}
	}
	else
	{
		logger.LogInformation("This instance has no fenced leadership tenure; it will not perform leader work.");
		logger.LogInformation("Current leader: {CurrentLeader}", leaderElection.CurrentLeaderId ?? "(unknown)");
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
	logger.LogInformation("Instances contend for the same Redis key; protect real writes with the pinned fence.");
	logger.LogInformation("Stop the leader instance and watch another take over.");
	logger.LogInformation("");

	// ============================================================
	// Configuration Reference
	// ============================================================
	logger.LogInformation("=== Configuration Reference ===");
	logger.LogInformation("");
	logger.LogInformation("| Option | Default | Description |");
	logger.LogInformation("|--------|---------|-------------|");
	logger.LogInformation("| LeaseDuration | 15s | Redis key TTL, independent of the local proof deadline |");
	logger.LogInformation("| RenewInterval | 5s | Renewal request-start cadence |");
	logger.LogInformation("| GracePeriod | 5s | Added to RenewInterval for the local ownership-proof budget |");
	logger.LogInformation("| InstanceId | Unset | Configured identity, then container environment, then generated GUID |");
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
	logger.LogInformation("| Redis | Shared lock coordination | Redis cluster |");
	logger.LogInformation("| SQL Server | Existing SQL infrastructure | SQL Server |");
	logger.LogInformation("| Kubernetes | K8s-native deployments | Kubernetes API |");
	logger.LogInformation("| Consul | Service mesh integration | Consul cluster |");
	logger.LogInformation("");

 }

async Task StopHostAsync(ILogger<Program>? logger, bool allowInteractiveWait)
{
	if (!allowInteractiveWait || builder.Configuration.GetValue<bool>("Once"))
	{
		logger?.LogInformation("Sample completed. Stopping host.");
		await host.StopAsync().ConfigureAwait(false);
	}
	else
	{
		logger?.LogInformation("Sample completed. Press Ctrl+C to exit...");
		await host.WaitForShutdownAsync().ConfigureAwait(false);
	}

}

// One application lifetime owner preserves failures through final host teardown.
static async Task RunAndShutdownAsync(IHost application,
    Func<ILeaderElection, ILogger<Program>, Task> body,
    Func<ILogger<Program>?, bool, Task> stopHost)
{
    var failures = new List<Exception>();
    ILeaderElection? election = null;
    ILogger<Program>? logger = null;
    try
    {
        logger = application.Services.GetRequiredService<ILogger<Program>>();
        election = application.Services.GetRequiredService<ILeaderElection>();
        await body(election, logger).ConfigureAwait(false);
    }
    catch (Exception failure) { Record(failure); }

    if (election is not null)
    {
        var resources = DisposeResourcesAsync(election);
        var notifications = election.NotificationsCompleted;
        try { await Task.WhenAll(resources, notifications).ConfigureAwait(false); }
        catch (Exception)
        {
            foreach (var task in new[] { resources, notifications })
            {
                if (task.Exception is { } error) { Record(error); }
                else if (task.IsCanceled)
                {
                    // Re-await preserves the actual cancellation exception and its original token.
                    try { await task.ConfigureAwait(false); }
                    catch (Exception failure) { Record(failure); }
                }
            }
        }
    }

    // Never hide an already-known failure behind the interactive Ctrl+C wait.
    // Loggers and instruments remain alive until all admitted election callbacks have settled.
    try { await stopHost(logger, failures.Count == 0).ConfigureAwait(false); }
    catch (Exception failure) { Record(failure); }
    try
    {
        if (application is IAsyncDisposable asynchronous) { await asynchronous.DisposeAsync().ConfigureAwait(false); }
        else { application.Dispose(); }
    }
    catch (Exception failure) { Record(failure); }

    if (failures.Count == 1) { ExceptionDispatchInfo.Capture(failures[0]).Throw(); }
    if (failures.Count > 1) { throw new AggregateException("Leader-election demo and shutdown failures.", failures); }

    void Record(Exception failure)
    {
        if (failure is AggregateException aggregate && aggregate.InnerExceptions is { Count: > 0 } innerFailures)
        {
            foreach (var inner in innerFailures) { Record(inner); }
        }
        else if (!failures.Any(previous => ReferenceEquals(previous, failure)))
        {
            // An empty AggregateException is itself a real failure, not an empty successful outcome.
            failures.Add(failure);
        }
    }

    // Normalize synchronous DisposeAsync throws without skipping admission of the terminal join.
    static async Task DisposeResourcesAsync(ILeaderElection target) =>
        await target.DisposeAsync().ConfigureAwait(false);
}

#pragma warning restore CA1303
