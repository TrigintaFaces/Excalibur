// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Benchmarks.LeaderElection;

/// <summary>
/// Benchmarks for SqlServerLeaderElection operations.
/// </summary>
/// <remarks>
/// Requires a SQL Server instance. Set the BENCHMARK_SQL_CONNECTIONSTRING environment variable
/// to enable these benchmarks. When not set, all benchmarks return immediately.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class SqlServerLeaderElectionBenchmarks
{
	private static readonly string? ConnectionString =
		Environment.GetEnvironmentVariable("BENCHMARK_SQL_CONNECTIONSTRING");

	private string _iterationResource = null!;

	private static bool IsAvailable => ConnectionString is not null;

	[IterationSetup]
	public void IterationSetup()
	{
		_iterationResource = $"bench-{Guid.NewGuid():N}";
	}

	#region Lease Acquisition Benchmarks

	/// <summary>
	/// Benchmark: Acquire a new lease (start + acquire lock).
	/// </summary>
	[Benchmark(Baseline = true)]
	public async Task TryAcquireLease()
	{
		if (!IsAvailable)
		{
			return;
		}

		var options = Options.Create(new LeaderElectionOptions
		{
			LeaseDuration = TimeSpan.FromSeconds(30),
			RenewInterval = TimeSpan.FromSeconds(10),
			RetryInterval = TimeSpan.FromSeconds(2),
			InstanceId = $"bench-{Guid.NewGuid():N}"[..24]
		});

		await using var election = new SqlServerLeaderElection(
			ConnectionString!,
			_iterationResource,
			options,
			NullLogger<SqlServerLeaderElection>.Instance);

		await election.StartAsync(CancellationToken.None);
		await election.StopAsync(CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Acquire then release lease (full lifecycle).
	/// </summary>
	[Benchmark]
	public async Task AcquireAndReleaseLease()
	{
		if (!IsAvailable)
		{
			return;
		}

		var options = Options.Create(new LeaderElectionOptions
		{
			LeaseDuration = TimeSpan.FromSeconds(30),
			RenewInterval = TimeSpan.FromSeconds(10),
			RetryInterval = TimeSpan.FromSeconds(2),
			InstanceId = $"bench-{Guid.NewGuid():N}"[..24]
		});

		await using var election = new SqlServerLeaderElection(
			ConnectionString!,
			_iterationResource,
			options,
			NullLogger<SqlServerLeaderElection>.Instance);

		await election.StartAsync(CancellationToken.None);

		// Verify leadership was acquired
		_ = election.IsLeader;
		_ = election.CurrentLeaderId;

		await election.StopAsync(CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Two candidates competing for the same resource.
	/// </summary>
	[Benchmark]
	public async Task TwoCandidatesCompete()
	{
		if (!IsAvailable)
		{
			return;
		}

		var resource = $"compete-{Guid.NewGuid():N}";

		var options1 = Options.Create(new LeaderElectionOptions
		{
			LeaseDuration = TimeSpan.FromSeconds(30),
			RenewInterval = TimeSpan.FromSeconds(10),
			RetryInterval = TimeSpan.FromSeconds(1),
			InstanceId = $"candidate-1-{Guid.NewGuid():N}"[..24]
		});

		var options2 = Options.Create(new LeaderElectionOptions
		{
			LeaseDuration = TimeSpan.FromSeconds(30),
			RenewInterval = TimeSpan.FromSeconds(10),
			RetryInterval = TimeSpan.FromSeconds(1),
			InstanceId = $"candidate-2-{Guid.NewGuid():N}"[..24]
		});

		await using var election1 = new SqlServerLeaderElection(
			ConnectionString!, resource, options1,
			NullLogger<SqlServerLeaderElection>.Instance);

		await using var election2 = new SqlServerLeaderElection(
			ConnectionString!, resource, options2,
			NullLogger<SqlServerLeaderElection>.Instance);

		// Start both candidates concurrently
		await Task.WhenAll(
			election1.StartAsync(CancellationToken.None),
			election2.StartAsync(CancellationToken.None));

		// Clean up
		await Task.WhenAll(
			election1.StopAsync(CancellationToken.None),
			election2.StopAsync(CancellationToken.None));
	}

	#endregion

	#region Property Access Benchmarks

	/// <summary>
	/// Benchmark: Check IsLeader property after acquiring lease.
	/// </summary>
	[Benchmark]
	public async Task<bool> CheckIsLeaderAfterAcquire()
	{
		if (!IsAvailable)
		{
			return false;
		}

		var options = Options.Create(new LeaderElectionOptions
		{
			LeaseDuration = TimeSpan.FromSeconds(30),
			RenewInterval = TimeSpan.FromSeconds(10),
			RetryInterval = TimeSpan.FromSeconds(2),
			InstanceId = $"bench-{Guid.NewGuid():N}"[..24]
		});

		await using var election = new SqlServerLeaderElection(
			ConnectionString!,
			_iterationResource,
			options,
			NullLogger<SqlServerLeaderElection>.Instance);

		await election.StartAsync(CancellationToken.None);
		var isLeader = election.IsLeader;
		await election.StopAsync(CancellationToken.None);

		return isLeader;
	}

	#endregion
}
