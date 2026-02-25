// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Kubernetes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace examples.Excalibur.Patterns.LeaderElection.Examples;

/// <summary>
///     Example demonstrating Kubernetes leader election usage.
/// </summary>
public class KubernetesLeaderElectionExample
{
	/// <summary>
	///     Basic example of using Kubernetes leader election.
	/// </summary>
	public static async Task BasicExample()
	{
		var services = new ServiceCollection();

		// Configure logging
		services.AddLogging(builder => builder.AddConsole());

		// Add Kubernetes leader election
		services.AddKubernetesLeaderElection(options =>
		{
			options.Namespace = "default";
			options.LeaseDurationSeconds = 15;
			options.RenewIntervalMilliseconds = 5000;
			options.RetryIntervalMilliseconds = 2000;
			options.GracePeriodSeconds = 5;
			options.CandidateMetadata = new Dictionary<string, string> { ["version"] = "1.0.0", ["region"] = "us-west-2" };
		});

		var serviceProvider = services.BuildServiceProvider();
		var factory = serviceProvider.GetRequiredService<ILeaderElectionFactory>();
		var logger = serviceProvider.GetService<ILogger<KubernetesLeaderElectionExample>>();

		// Create a leader election instance
		var election = factory.CreateElection("my-app-leader");

		// Subscribe to events
		election.BecameLeader += (sender, args) =>
		{
			logger?.LogInformation("🎉 Became leader for resource: {Resource}", args.ResourceName);
		};

		election.LostLeadership += (sender, args) =>
		{
			logger?.LogWarning("😢 Lost leadership for resource: {Resource}", args.ResourceName);
		};

		election.LeaderChanged += (sender, args) =>
		{
			logger?.LogInformation("Leader changed from {Previous} to {New}",
				args.PreviousLeaderId ?? "none", args.NewLeaderId ?? "none");
		};

		// Start the election
		await election.StartAsync();

		// Simulate work
		var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
		while (!cts.Token.IsCancellationRequested)
		{
			if (election.IsLeader)
			{
				logger?.LogInformation("Performing leader work...");
				// Do leader-specific work here
			}
			else
			{
				logger?.LogInformation("Standing by as follower. Current leader: {Leader}",
					election.CurrentLeaderId ?? "none");
			}

			await Task.Delay(TimeSpan.FromSeconds(10), cts.Token);
		}

		// Stop the election
		await election.StopAsync();
	}

	/// <summary>
	///     Example using health-based leader election.
	/// </summary>
	public static async Task HealthBasedExample()
	{
		var services = new ServiceCollection();

		// Configure logging
		services.AddLogging(builder => builder.AddConsole());

		// Add Kubernetes leader election with health checks
		services.AddKubernetesLeaderElection(options =>
		{
			options.EnableHealthChecks = true;
			options.MinimumHealthScore = 0.8;
			options.StepDownWhenUnhealthy = true;
			options.LeaseDurationSeconds = 15;
			options.RenewIntervalMilliseconds = 5000;
		});

		var serviceProvider = services.BuildServiceProvider();
		var factory = serviceProvider.GetRequiredService<ILeaderElectionFactory>();
		var logger = serviceProvider.GetService<ILogger<KubernetesLeaderElectionExample>>();

		// Create a health-based leader election instance
		var election = factory.CreateHealthBasedElection("my-healthy-app");

		// Start the election
		await election.StartAsync();

		// Simulate health checks
		var random = new Random();
		var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

		while (!cts.Token.IsCancellationRequested)
		{
			// Simulate health status
			var isHealthy = random.NextDouble() > 0.2; // 80% healthy
			var metadata = new Dictionary<string, string>
			{
				["cpu_usage"] = $"{random.Next(0, 100)}%",
				["memory_usage"] = $"{random.Next(0, 100)}%",
				["request_latency_ms"] = $"{random.Next(10, 500)}"
			};

			await election.UpdateHealthAsync(isHealthy, metadata);

			if (election.IsLeader)
			{
				if (isHealthy)
				{
					logger?.LogInformation("✅ Leader is healthy, performing work...");
				}
				else
				{
					logger?.LogWarning("⚠️ Leader is unhealthy, may step down...");
				}
			}

			// Get all candidate health
			var candidateHealth = await election.GetCandidateHealthAsync();
			foreach (var health in candidateHealth)
			{
				logger?.LogInformation("Candidate {Id}: Healthy={Healthy}, Score={Score:F2}, Leader={Leader}",
					health.CandidateId, health.IsHealthy, health.HealthScore, health.IsLeader);
			}

			await Task.Delay(TimeSpan.FromSeconds(10), cts.Token);
		}

		await election.StopAsync();
	}

	/// <summary>
	///     Example using hosted service for automatic leader election management.
	/// </summary>
	public static async Task HostedServiceExample()
	{
		var hostBuilder = Host.CreateDefaultBuilder()
			.ConfigureServices((context, services) =>
			{
				// Add Kubernetes leader election as a hosted service
				services.AddKubernetesLeaderElectionHostedService("my-service-leader", options =>
				{
					options.Namespace = context.Configuration["Kubernetes:Namespace"] ?? "default";
					options.LeaseDurationSeconds = 30;
					options.RenewIntervalMilliseconds = 10000;
					options.EnableHealthChecks = true;
					options.StepDownWhenUnhealthy = true;
				});

				// Add your application services that need leader election
				services.AddHostedService<LeaderAwareWorkerService>();
			});

		var host = hostBuilder.Build();
		await host.RunAsync();
	}

	/// <summary>
	///     Example with multiple resources and fine-grained leader election.
	/// </summary>
	public static async Task MultiResourceExample()
	{
		var services = new ServiceCollection();
		services.AddLogging(builder => builder.AddConsole());
		services.AddKubernetesLeaderElection();

		var serviceProvider = services.BuildServiceProvider();
		var factory = serviceProvider.GetRequiredService<ILeaderElectionFactory>();
		var logger = serviceProvider.GetService<ILogger<KubernetesLeaderElectionExample>>();

		// Create elections for different resources
		var resources = new[] { "scheduler", "processor", "reporter" };
		var elections = new Dictionary<string, ILeaderElection>();

		foreach (var resource in resources)
		{
			var election = factory.CreateElection($"app-{resource}");
			elections[resource] = election;

			var localResource = resource; // Capture for closure
			election.BecameLeader += (_, _) =>
			{
				logger?.LogInformation("Became leader for {Resource}", localResource);
			};

			await election.StartAsync();
		}

		// Simulate work with different leaders for different resources
		var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		while (!cts.Token.IsCancellationRequested)
		{
			foreach (var (resource, election) in elections)
			{
				if (election.IsLeader)
				{
					logger?.LogInformation("Leading {Resource} operations", resource);
				}
			}

			await Task.Delay(TimeSpan.FromSeconds(10), cts.Token);
		}

		// Stop all elections
		foreach (var election in elections.Values)
		{
			await election.StopAsync();
		}
	}

	/// <summary>
	///     Example worker service that responds to leader election changes.
	/// </summary>
	private class LeaderAwareWorkerService : BackgroundService
	{
		private readonly ILeaderElectionFactory _electionFactory;
		private readonly ILogger<LeaderAwareWorkerService> _logger;
		private ILeaderElection? _election;

		public LeaderAwareWorkerService(
			ILeaderElectionFactory electionFactory,
			ILogger<LeaderAwareWorkerService> logger)
		{
			_electionFactory = electionFactory;
			_logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			// Create election for this specific worker
			_election = _electionFactory.CreateElection("worker-service");

			_election.BecameLeader += (_, _) =>
			{
				_logger.LogInformation("Worker became leader, starting primary work...");
			};

			_election.LostLeadership += (_, _) =>
			{
				_logger.LogWarning("Worker lost leadership, switching to standby mode...");
			};

			// Start election
			await _election.StartAsync(stoppingToken);

			// Main work loop
			while (!stoppingToken.IsCancellationRequested)
			{
				if (_election.IsLeader)
				{
					// Perform leader-specific work
					await PerformLeaderWorkAsync(stoppingToken);
				}
				else
				{
					// Perform follower/standby work
					await PerformFollowerWorkAsync(stoppingToken);
				}

				await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
			}

			// Clean shutdown
			await _election.StopAsync(CancellationToken.None);
		}

		private async Task PerformLeaderWorkAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Performing leader work: processing batch jobs...");
			// Simulate leader work
			await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
		}

		private async Task PerformFollowerWorkAsync(CancellationToken cancellationToken)
		{
			_logger.LogDebug("Standing by as follower...");
			// Followers might perform read-only operations or cache warming
			await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
		}
	}
}
