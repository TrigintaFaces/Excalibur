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
using Excalibur.Dispatch.LeaderElection.Consul;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace examples.Excalibur.Patterns.LeaderElection.Examples;

/// <summary>
///     Example demonstrating Consul-based leader election in a distributed system.
/// </summary>
public class ConsulLeaderElectionExample
{
	/// <summary>
	///     Demonstrates basic Consul leader election setup and usage.
	/// </summary>
	public static async Task BasicExample()
	{
		var services = new ServiceCollection();

		// Configure logging
		services.AddLogging(builder => builder.AddConsole());

		// Configure Consul leader election
		services.AddConsulLeaderElection(options =>
		{
			options.ConsulAddress = "http://localhost:8500";
			options.KeyPrefix = "myapp/leader";
			options.LeaseDuration = TimeSpan.FromSeconds(15);
			options.RenewInterval = TimeSpan.FromSeconds(5);
			options.SessionTTL = TimeSpan.FromSeconds(30);
			options.InstanceId = $"instance-{Environment.MachineName}";
		});

		var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<ILeaderElectionFactory>();

		// Create leader election for a specific resource
		var election = factory.CreateElection("database-migration");

		// Subscribe to events
		election.BecameLeader += (sender, args) =>
		{
			Console.WriteLine($"[{args.CandidateId}] Became leader for {args.ResourceName}");
		};

		election.LostLeadership += (sender, args) =>
		{
			Console.WriteLine($"[{args.CandidateId}] Lost leadership for {args.ResourceName}");
		};

		election.LeaderChanged += (sender, args) =>
		{
			Console.WriteLine($"Leader changed from {args.PreviousLeaderId ?? "none"} to {args.NewLeaderId ?? "none"}");
		};

		// Start participating in election
		await election.StartAsync();

		// Simulate work
		var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
		while (!cts.Token.IsCancellationRequested)
		{
			if (election.IsLeader)
			{
				Console.WriteLine("Performing leader-only work...");
				// Do work that only the leader should perform
			}
			else
			{
				Console.WriteLine($"Standing by. Current leader: {election.CurrentLeaderId ?? "none"}");
			}

			await Task.Delay(TimeSpan.FromSeconds(10), cts.Token);
		}

		// Stop election participation
		await election.StopAsync();
	}

	/// <summary>
	///     Demonstrates health-based leader election with automatic step-down.
	/// </summary>
	public static async Task HealthBasedExample()
	{
		var services = new ServiceCollection();

		services.AddLogging(builder => builder.AddConsole());

		// Configure health-based Consul leader election
		services.AddConsulLeaderElection(options =>
		{
			options.ConsulAddress = "http://localhost:8500";
			options.KeyPrefix = "myapp/leader";
			options.EnableHealthChecks = true;
			options.MinimumHealthScore = 0.8;
			options.StepDownWhenUnhealthy = true;
			options.HealthCheckId = "service:myapp";
		});

		var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<ILeaderElectionFactory>();

		// Create health-based leader election
		var election = factory.CreateHealthBasedElection("critical-service");

		// Start election
		await election.StartAsync();

		// Simulate health monitoring
		var random = new Random();
		var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

		while (!cts.Token.IsCancellationRequested)
		{
			// Simulate health check
			var isHealthy = random.NextDouble() > 0.2; // 80% healthy
			var healthScore = isHealthy ? 1.0 : 0.3;

			await election.UpdateHealthAsync(isHealthy,
				new Dictionary<string, string>
				{
					["cpu_usage"] = $"{random.Next(10, 90)}%",
					["memory_usage"] = $"{random.Next(30, 80)}%",
					["health_score"] = healthScore.ToString("F2")
				});

			if (election.IsLeader)
			{
				Console.WriteLine($"Leader performing work (Health: {(isHealthy ? "Healthy" : "Unhealthy")})");
			}

			// Get all candidates' health
			var candidatesHealth = await election.GetCandidateHealthAsync();
			Console.WriteLine($"Total candidates: {candidatesHealth.Count()}");
			foreach (var health in candidatesHealth)
			{
				Console.WriteLine($" {health.CandidateId}: {(health.IsHealthy ? "Healthy" : "Unhealthy")} " +
									$"(Score: {health.HealthScore:F2}, Leader: {health.IsLeader})");
			}

			await Task.Delay(TimeSpan.FromSeconds(15), cts.Token);
		}

		await election.StopAsync();
	}

	/// <summary>
	///     Demonstrates multi-instance leader election coordination.
	/// </summary>
	public static async Task MultiInstanceExample()
	{
		var tasks = new Task[3];
		var cancellationTokenSource = new CancellationTokenSource();

		// Simulate 3 instances competing for leadership
		for (int i = 0; i < 3; i++)
		{
			var instanceId = $"instance-{i + 1}";
			tasks[i] = RunInstance(instanceId, cancellationTokenSource.Token);
		}

		// Run for 2 minutes
		await Task.Delay(TimeSpan.FromMinutes(2));
		cancellationTokenSource.Cancel();

		await Task.WhenAll(tasks);
	}

	/// <summary>
	///     Main entry point for the examples.
	/// </summary>
	public static async Task Main(string[] args)
	{
		Console.WriteLine("Consul Leader Election Examples");
		Console.WriteLine("================================");
		Console.WriteLine("1. Basic Example");
		Console.WriteLine("2. Health-Based Example");
		Console.WriteLine("3. Multi-Instance Example");
		Console.WriteLine("4. Hosted Service Example");
		Console.Write("Select example (1-4): ");

		var choice = Console.ReadLine();

		switch (choice)
		{
			case "1":
				await BasicExample();
				break;

			case "2":
				await HealthBasedExample();
				break;

			case "3":
				await MultiInstanceExample();
				break;

			case "4":
				// Create host for hosted service example
				var host = Host.CreateDefaultBuilder(args)
					.ConfigureServices((context, services) =>
					{
						services.AddConsulLeaderElection(context.Configuration.GetSection("ConsulLeaderElection"));
						services.AddHostedService<LeaderElectionHostedService>();
					})
					.Build();

				await host.RunAsync();
				break;

			default:
				Console.WriteLine("Invalid choice");
				break;
		}
	}

	private static async Task RunInstance(string instanceId, CancellationToken cancellationToken)
	{
		var services = new ServiceCollection();
		services.AddLogging(builder => builder.AddConsole());

		services.AddConsulLeaderElection(options =>
		{
			options.ConsulAddress = "http://localhost:8500";
			options.KeyPrefix = "myapp/leader";
			options.InstanceId = instanceId;
			options.LeaseDuration = TimeSpan.FromSeconds(10);
			options.RenewInterval = TimeSpan.FromSeconds(3);
		});

		var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<ILeaderElectionFactory>();
		var election = factory.CreateElection("shared-resource");

		election.BecameLeader += (_, e) =>
		{
			Console.WriteLine($"[{instanceId}] BECAME LEADER");
		};

		election.LostLeadership += (_, e) =>
		{
			Console.WriteLine($"[{instanceId}] LOST LEADERSHIP");
		};

		await election.StartAsync(cancellationToken);

		while (!cancellationToken.IsCancellationRequested)
		{
			if (election.IsLeader)
			{
				Console.WriteLine($"[{instanceId}] Executing leader work...");
			}

			await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
		}

		await election.StopAsync();
	}

	/// <summary>
	///     Demonstrates using leader election in a hosted service.
	/// </summary>
	public class LeaderElectionHostedService : IHostedService
	{
		private readonly IHealthBasedLeaderElection _leaderElection;
		private readonly ILogger<LeaderElectionHostedService> _logger;
		private Timer? _workTimer;

		public LeaderElectionHostedService(
			ILeaderElectionFactory factory,
			ILogger<LeaderElectionHostedService> logger)
		{
			_leaderElection = factory.CreateHealthBasedElection("background-job-processor");
			_logger = logger;
		}

		public async Task StartAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Starting leader election hosted service");

			// Subscribe to events
			_leaderElection.BecameLeader += BecameLeader;
			_leaderElection.LostLeadership += LostLeadership;

			// Start election
			await _leaderElection.StartAsync(cancellationToken);

			// Start health monitoring
			_workTimer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
		}

		public async Task StopAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Stopping leader election hosted service");

			_workTimer?.Change(Timeout.Infinite, 0);
			_workTimer?.Dispose();

			await _leaderElection.StopAsync(cancellationToken);

			_leaderElection.BecameLeader -= BecameLeader;
			_leaderElection.LostLeadership -= LostLeadership;
		}

		private void BecameLeader(object? sender, LeaderElectionEventArgs e) =>
			_logger.LogInformation("Became leader, starting background job processing");

		private void LostLeadership(object? sender, LeaderElectionEventArgs e) =>
			_logger.LogInformation("Lost leadership, stopping background job processing");

		private async void DoWork(object? state)
		{
			try
			{
				// Update health based on system state
				var isHealthy = CheckSystemHealth();
				await _leaderElection.UpdateHealthAsync(isHealthy);

				// Only process jobs if we're the leader
				if (_leaderElection.IsLeader)
				{
					_logger.LogInformation("Processing background jobs as leader");
					await ProcessBackgroundJobs();
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in background job processing");
				await _leaderElection.UpdateHealthAsync(false);
			}
		}

		private bool CheckSystemHealth() =>
			// Implement actual health checks
			true;

		private async Task ProcessBackgroundJobs() =>
			// Implement actual job processing
			await Task.Delay(1000);
	}
}
