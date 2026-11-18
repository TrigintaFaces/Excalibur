// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.LeaderElection;

using Excalibur.LeaderElection.InMemory;

namespace Excalibur.Benchmarks.LeaderElection;

/// <summary>
/// Benchmarks for Leader Election operations.
/// </summary>
/// <remarks>
/// AD-221-6: Leader Election benchmark category.
/// Measures the original in-memory startup, health and shutdown operations.
/// MultipleCandidatesCompete issues starts sequentially; Task.WhenAll does not establish
/// contention when non-fencing starts complete synchronously. Property and health rows
/// include startup, and health retrieval starts only candidate zero.
/// LeaderFailover with one candidate is a no-op. With five or ten candidates it includes
/// a fixed 10 ms delay, while follower renewal is configured at 10 seconds. Those rows
/// do not establish successor acquisition or measure failover latency.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class LeaderElectionBenchmarks
{
	private LeaderElectionOptions _options = null!;
	private InMemoryLeaderElection[] _candidates = null!;

	[Params(1, 5, 10)]
	public int CandidateCount { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		_options = new LeaderElectionOptions
		{
			LeaseDuration = TimeSpan.FromSeconds(30),
			RenewInterval = TimeSpan.FromSeconds(10),
			RetryInterval = TimeSpan.FromSeconds(5),
			StepDownWhenUnhealthy = true
		};

		_candidates = new InMemoryLeaderElection[CandidateCount];
	}

	[GlobalCleanup]
	public void GlobalCleanup() =>
		LeaderElectionBenchmarkSetup.DisposeCandidatesAsync(_candidates).AsTask().GetAwaiter().GetResult();

	[IterationSetup]
	public void IterationSetup()
	{
		GlobalCleanup();
		// Each iteration owns one isolated election domain with distinct candidate IDs.
		_candidates = LeaderElectionBenchmarkSetup.CreateCandidates(CandidateCount, _options);
	}

	#region Lease Acquisition Benchmarks

	/// <summary>
	/// Benchmark: Single candidate start and acquire leadership.
	/// </summary>
	[Benchmark(Baseline = true)]
	public async Task SingleCandidateAcquire()
	{
		if (CandidateCount < 1)
			return;

		await _candidates[0].StartAsync(CancellationToken.None);
		// Leadership should be acquired immediately for in-memory
	}

	/// <summary>
	/// Benchmark: Multiple candidates competing for leadership.
	/// </summary>
	[Benchmark]
	public async Task MultipleCandidatesCompete()
	{
		var tasks = new Task[CandidateCount];
		for (int i = 0; i < CandidateCount; i++)
		{
			tasks[i] = _candidates[i].StartAsync(CancellationToken.None);
		}
		await Task.WhenAll(tasks);
	}

	#endregion

	#region IsLeader Property Benchmarks

	/// <summary>
	/// Benchmark: Check IsLeader property (hot path).
	/// </summary>
	[Benchmark]
	public bool CheckIsLeader()
	{
		if (CandidateCount < 1)
			return false;

		// Start first to have a valid state
		_candidates[0].StartAsync(CancellationToken.None).GetAwaiter().GetResult();
		return _candidates[0].IsLeader;
	}

	/// <summary>
	/// Benchmark: Get CurrentLeaderId (hot path).
	/// </summary>
	[Benchmark]
	public string? GetCurrentLeaderId()
	{
		if (CandidateCount < 1)
			return null;

		_candidates[0].StartAsync(CancellationToken.None).GetAwaiter().GetResult();
		return _candidates[0].CurrentLeaderId;
	}

	#endregion

	#region Health Update Benchmarks

	/// <summary>
	/// Benchmark: Update health status.
	/// </summary>
	[Benchmark]
	public async Task UpdateHealth()
	{
		if (CandidateCount < 1)
			return;

		await _candidates[0].StartAsync(CancellationToken.None);
		await _candidates[0].UpdateHealthAsync(true, new Dictionary<string, string>
		{
			["cpu"] = "50%",
			["memory"] = "2GB"
		}, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Get candidate health info.
	/// </summary>
	[Benchmark]
	public async Task<IReadOnlyList<CandidateHealth>> GetCandidateHealth()
	{
		if (CandidateCount < 1)
			return [];

		await _candidates[0].StartAsync(CancellationToken.None);
		var result = await _candidates[0].GetCandidateHealthAsync(CancellationToken.None);
		return result.ToList();
	}

	#endregion

	#region Failover Benchmarks

	/// <summary>
	/// Benchmark: Stop leader and measure failover.
	/// </summary>
	[Benchmark]
	public async Task LeaderFailover()
	{
		if (CandidateCount < 2)
			return;

		// Start all candidates
		var startTasks = new Task[CandidateCount];
		for (int i = 0; i < CandidateCount; i++)
		{
			startTasks[i] = _candidates[i].StartAsync(CancellationToken.None);
		}
		await Task.WhenAll(startTasks);

		// Find and stop the leader
		var leader = _candidates.FirstOrDefault(c => c.IsLeader);
		if (leader != null)
		{
			await leader.StopAsync(CancellationToken.None);
		}

		// Wait for another candidate to pick up leadership
		await Task.Delay(10); // Give time for failover
	}

	/// <summary>
	/// Benchmark: Unhealthy step-down.
	/// </summary>
	[Benchmark]
	public async Task UnhealthyStepDown()
	{
		if (CandidateCount < 1)
			return;

		await _candidates[0].StartAsync(CancellationToken.None);

		// Mark as unhealthy - should trigger step down
		await _candidates[0].UpdateHealthAsync(false, null, CancellationToken.None);
	}

	#endregion

	#region Stop Benchmarks

	/// <summary>
	/// Benchmark: Graceful leader shutdown.
	/// </summary>
	[Benchmark]
	public async Task GracefulShutdown()
	{
		if (CandidateCount < 1)
			return;

		await _candidates[0].StartAsync(CancellationToken.None);
		await _candidates[0].StopAsync(CancellationToken.None);
	}

	#endregion
}
