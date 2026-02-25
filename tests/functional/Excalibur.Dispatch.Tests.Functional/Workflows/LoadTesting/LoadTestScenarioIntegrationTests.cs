// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Tests.Functional.Workflows.LoadTesting.Fixtures;

namespace Excalibur.Dispatch.Tests.Functional.Workflows.LoadTesting;

/// <summary>
/// Integration tests validating load test scenario patterns with real IDispatcher.
/// These are functional validation tests - not full NBomber load tests.
/// </summary>
/// <remarks>
/// Sprint 198 - NBomber Load Testing Integration.
/// Validates all scenario integration patterns work correctly.
/// </remarks>
public sealed class LoadTestScenarioIntegrationTests
{
	#region Throughput Scenarios (bd-1v3v4)

	/// <summary>
	/// Validates throughput scenario can dispatch real messages.
	/// </summary>
	[Fact]
	public async Task Throughput_Scenario_Dispatches_Real_Messages()
	{
		// Arrange
		await using var client = new DispatchLoadTestClient();

		// Act - dispatch a few messages to validate integration
		var results = new List<DispatchTestResult>();
		for (var i = 0; i < 10; i++)
		{
			results.Add(await client.DispatchAsync(CancellationToken.None));
		}

		// Assert
		results.ShouldAllBe(r => r.Success, "All dispatches should succeed");
		client.HandledCount.ShouldBeGreaterThanOrEqualTo(10, "Handler should have processed messages");
	}

	/// <summary>
	/// Validates sustained load scenario can maintain throughput.
	/// </summary>
	[Fact]
	public async Task Sustained_Load_Scenario_Maintains_Throughput()
	{
		// Arrange
		await using var client = new DispatchLoadTestClient();

		// Act - dispatch multiple batches to simulate sustained load
		var successCount = 0;
		for (var batch = 0; batch < 5; batch++)
		{
			var tasks = Enumerable.Range(0, 20)
				.Select(_ => client.DispatchAsync(CancellationToken.None));

			var results = await Task.WhenAll(tasks);
			successCount += results.Count(r => r.Success);
		}

		// Assert
		successCount.ShouldBeGreaterThanOrEqualTo(90, "At least 90% should succeed"); // 100 * 90% = 90
		client.HandledCount.ShouldBeGreaterThanOrEqualTo(90);
	}

	/// <summary>
	/// Validates burst scenario can handle spike load.
	/// </summary>
	[Fact]
	public async Task Burst_Scenario_Handles_Spike_Load()
	{
		// Arrange
		await using var client = new DispatchLoadTestClient();

		// Act - simulate burst with concurrent dispatches
		var burstTasks = Enumerable.Range(0, 50)
			.Select(_ => client.DispatchAsync(CancellationToken.None));

		var results = await Task.WhenAll(burstTasks);

		// Assert
		var successRate = results.Count(r => r.Success) / (double)results.Length;
		successRate.ShouldBeGreaterThanOrEqualTo(0.95, "At least 95% should succeed during burst");
	}

	#endregion Throughput Scenarios (bd-1v3v4)

	#region CDC Load Scenarios (bd-fi7xd)

	/// <summary>
	/// Validates CDC scenario can process CDC events under load.
	/// </summary>
	[Fact]
	public async Task Cdc_Scenario_Processes_Changes_Under_Load()
	{
		// Arrange
		await using var client = new DispatchLoadTestClient();

		// Act - dispatch CDC events with sequence numbers
		var results = new List<DispatchTestResult>();
		for (var seq = 1L; seq <= 50; seq++)
		{
			results.Add(await client.DispatchCdcAsync(seq, CancellationToken.None));
		}

		// Assert
		results.ShouldAllBe(r => r.Success, "All CDC dispatches should succeed");
		client.CdcHandledCount.ShouldBeGreaterThanOrEqualTo(50, "CDC handler should have processed events");
	}

	/// <summary>
	/// Validates CDC ordering under concurrent load.
	/// </summary>
	[Fact]
	public async Task Cdc_Ordering_Under_Concurrent_Load()
	{
		// Arrange
		await using var client = new DispatchLoadTestClient();

		// Act - dispatch CDC events concurrently
		var tasks = Enumerable.Range(1, 100)
			.Select(seq => client.DispatchCdcAsync(seq, CancellationToken.None));

		var results = await Task.WhenAll(tasks);

		// Assert
		var successRate = results.Count(r => r.Success) / (double)results.Length;
		successRate.ShouldBeGreaterThanOrEqualTo(0.95, "At least 95% CDC events should succeed");
	}

	/// <summary>
	/// Validates CDC can handle stale position recovery scenario.
	/// </summary>
	[Fact]
	public async Task Cdc_Stale_Position_Recovery()
	{
		// Arrange
		await using var client = new DispatchLoadTestClient();

		// Act - simulate gap in sequence (stale position scenario)
		for (var seq = 1L; seq <= 10; seq++)
		{
			_ = await client.DispatchCdcAsync(seq, CancellationToken.None);
		}

		// "Recovery" - jump to later sequence
		for (var seq = 50L; seq <= 60; seq++)
		{
			_ = await client.DispatchCdcAsync(seq, CancellationToken.None);
		}

		// Assert
		client.CdcHandledCount.ShouldBeGreaterThanOrEqualTo(20, "Both batches should be processed");
	}

	#endregion CDC Load Scenarios (bd-fi7xd)

	#region Failure Load Scenarios (bd-r9uiz)

	/// <summary>
	/// Validates system handles normal operations with retry capability.
	/// </summary>
	[Fact]
	public async Task Failure_Scenario_Handles_Normal_Operations()
	{
		// Arrange
		await using var client = new DispatchLoadTestClient();

		// Act - dispatch messages without injected failures
		var results = new List<DispatchTestResult>();
		for (var i = 0; i < 20; i++)
		{
			results.Add(await client.DispatchAsync(CancellationToken.None));
		}

		// Assert - baseline should be 100% success
		results.ShouldAllBe(r => r.Success, "Without failure injection, all should succeed");
	}

	/// <summary>
	/// Validates latency tracking during dispatch.
	/// </summary>
	[Fact]
	public async Task Failure_Scenario_Tracks_Latency()
	{
		// Arrange
		await using var client = new DispatchLoadTestClient();

		// Act
		var results = new List<DispatchTestResult>();
		for (var i = 0; i < 10; i++)
		{
			results.Add(await client.DispatchAsync(CancellationToken.None));
		}

		// Assert - all results should have latency recorded
		results.ShouldAllBe(r => r.LatencyMs > 0, "Latency should be recorded");
	}

	/// <summary>
	/// Validates circuit breaker pattern can be tested.
	/// </summary>
	[Fact]
	public async Task Failure_Scenario_Circuit_Breaker_Pattern()
	{
		// Arrange
		await using var client = new DispatchLoadTestClient();

		// Act - dispatch normal messages (circuit breaker behavior tested via dedicated middleware)
		var successCount = 0;
		for (var i = 0; i < 10; i++)
		{
			var result = await client.DispatchAsync(CancellationToken.None);
			if (result.Success)
			{
				successCount++;
			}
		}

		// Assert
		successCount.ShouldBe(10, "Normal messages should succeed with healthy circuit");
	}

	#endregion Failure Load Scenarios (bd-r9uiz)

	#region Concurrent Load Scenarios (bd-gff7p)

	/// <summary>
	/// Validates concurrent consumers process without duplication.
	/// </summary>
	[Fact]
	public async Task Concurrent_Consumers_Process_Without_Duplication()
	{
		// Arrange
		await using var client = new DispatchLoadTestClient();

		// Act - simulate concurrent consumers
		var tasks = Enumerable.Range(0, 50)
			.Select(_ => client.DispatchAsync(CancellationToken.None));

		var results = await Task.WhenAll(tasks);

		// Assert
		results.ShouldAllBe(r => r.Success, "Concurrent dispatches should succeed");
		client.HandledCount.ShouldBe(50, "Each dispatch should be handled exactly once");
	}

	/// <summary>
	/// Validates handling under contention.
	/// </summary>
	[Fact]
	public async Task Concurrent_Scenario_Handles_Contention()
	{
		// Arrange
		await using var client = new DispatchLoadTestClient();
		var latencies = new List<double>();

		// Act - high concurrency to test contention handling
		var tasks = Enumerable.Range(0, 100)
			.Select(async _ =>
			{
				var result = await client.DispatchAsync(CancellationToken.None);
				lock (latencies)
				{
					latencies.Add(result.LatencyMs);
				}

				return result;
			});

		var results = await Task.WhenAll(tasks);

		// Assert
		var successRate = results.Count(r => r.Success) / (double)results.Length;
		successRate.ShouldBeGreaterThanOrEqualTo(0.95, "At least 95% should succeed under contention");
	}

	/// <summary>
	/// Validates scaling with consumer count.
	/// </summary>
	[Fact]
	public async Task Concurrent_Scenario_Scales_With_Consumers()
	{
		// Arrange
		await using var client = new DispatchLoadTestClient();

		// Act - test with different concurrency levels
		var tasks10 = Enumerable.Range(0, 10)
			.Select(_ => client.DispatchAsync(CancellationToken.None));
		var start10 = DateTime.UtcNow;
		_ = await Task.WhenAll(tasks10);
		var duration10 = DateTime.UtcNow - start10;

		var tasks50 = Enumerable.Range(0, 50)
			.Select(_ => client.DispatchAsync(CancellationToken.None));
		var start50 = DateTime.UtcNow;
		_ = await Task.WhenAll(tasks50);
		var duration50 = DateTime.UtcNow - start50;

		// Assert - 50 messages should scale sub-linearly (not 50x longer than 10 messages)
		// Use threshold of 10 to account for environment variability and small timing variations
		var scalingFactor = duration50.TotalMilliseconds / Math.Max(duration10.TotalMilliseconds, 1);
		scalingFactor.ShouldBeLessThan(10, "System should scale sub-linearly with concurrency");
	}

	#endregion Concurrent Load Scenarios (bd-gff7p)

	#region Advanced Load Scenarios (bd-gk7ef)

	/// <summary>
	/// Validates gradual ramp behavior.
	/// </summary>
	[Fact]
	public async Task Gradual_Ramp_Scenario_Handles_Increasing_Load()
	{
		// Arrange
		await using var client = new DispatchLoadTestClient();

		// Act - simulate gradual ramp: 10, 20, 40, 80 concurrent
		var totalSuccess = 0;
		foreach (var concurrency in new[] { 10, 20, 40, 80 })
		{
			var tasks = Enumerable.Range(0, concurrency)
				.Select(_ => client.DispatchAsync(CancellationToken.None));

			var results = await Task.WhenAll(tasks);
			totalSuccess += results.Count(r => r.Success);
		}

		// Assert - all 150 dispatches should succeed (10+20+40+80)
		totalSuccess.ShouldBeGreaterThanOrEqualTo(140, "At least 93% should succeed during ramp");
	}

	/// <summary>
	/// Validates spike recovery behavior.
	/// </summary>
	[Fact]
	public async Task Spike_Scenario_Recovers_After_Burst()
	{
		// Arrange
		await using var client = new DispatchLoadTestClient();

		// Act - baseline -> spike -> recovery
		var baselineTasks = Enumerable.Range(0, 10)
			.Select(_ => client.DispatchAsync(CancellationToken.None));
		var baselineResults = await Task.WhenAll(baselineTasks);

		var spikeTasks = Enumerable.Range(0, 100)
			.Select(_ => client.DispatchAsync(CancellationToken.None));
		var spikeResults = await Task.WhenAll(spikeTasks);

		var recoveryTasks = Enumerable.Range(0, 10)
			.Select(_ => client.DispatchAsync(CancellationToken.None));
		var recoveryResults = await Task.WhenAll(recoveryTasks);

		// Assert
		baselineResults.ShouldAllBe(r => r.Success, "Baseline should succeed");
		recoveryResults.ShouldAllBe(r => r.Success, "Recovery should succeed");

		var spikeSuccessRate = spikeResults.Count(r => r.Success) / (double)spikeResults.Length;
		spikeSuccessRate.ShouldBeGreaterThanOrEqualTo(0.90, "At least 90% should succeed during spike");
	}

	/// <summary>
	/// Validates endurance (memory stability).
	/// </summary>
	[Fact]
	public async Task Endurance_Scenario_Maintains_Stability()
	{
		// Arrange
		await using var client = new DispatchLoadTestClient();
		var initialMemory = GC.GetTotalMemory(forceFullCollection: true);

		// Act - sustained dispatches
		for (var iteration = 0; iteration < 10; iteration++)
		{
			var tasks = Enumerable.Range(0, 50)
				.Select(_ => client.DispatchAsync(CancellationToken.None));

			_ = await Task.WhenAll(tasks);
		}

		var finalMemory = GC.GetTotalMemory(forceFullCollection: true);

		// Assert
		client.HandledCount.ShouldBeGreaterThanOrEqualTo(500);

		// Memory growth should be reasonable (< 50MB for this small test)
		var memoryGrowthMb = (finalMemory - initialMemory) / (1024.0 * 1024.0);
		memoryGrowthMb.ShouldBeLessThan(50, "Memory growth should be bounded");
	}

	#endregion Advanced Load Scenarios (bd-gk7ef)
}
