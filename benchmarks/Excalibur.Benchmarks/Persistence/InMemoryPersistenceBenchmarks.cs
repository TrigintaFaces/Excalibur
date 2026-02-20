// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Data.InMemory;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Benchmarks.Persistence;

/// <summary>
/// Benchmarks for InMemory persistence provider operations.
/// </summary>
/// <remarks>
/// AD-221-6: Persistence benchmark category for data access operations.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class InMemoryPersistenceBenchmarks
{
	private InMemoryPersistenceProvider _provider = null!;

	[Params(10, 100, 1000)]
	public int EntityCount { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		var options = Options.Create(new InMemoryProviderOptions { Name = "benchmark" });
		_provider = new InMemoryPersistenceProvider(options, NullLogger<InMemoryPersistenceProvider>.Instance);
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		_provider?.Dispose();
	}

	#region Connection Benchmarks

	/// <summary>
	/// Benchmark: Create connection (sync).
	/// </summary>
	[Benchmark(Baseline = true)]
	public void CreateConnection()
	{
		using var connection = _provider.CreateConnection();
	}

	/// <summary>
	/// Benchmark: Create connection (async).
	/// </summary>
	[Benchmark]
	public async ValueTask CreateConnectionAsync()
	{
		var connection = await _provider.CreateConnectionAsync(CancellationToken.None);
		connection.Dispose();
	}

	/// <summary>
	/// Benchmark: Create multiple connections.
	/// </summary>
	[Benchmark]
	public void CreateMultipleConnections()
	{
		for (int i = 0; i < 10; i++)
		{
			using var connection = _provider.CreateConnection();
		}
	}

	/// <summary>
	/// Benchmark: Create and open connection.
	/// </summary>
	[Benchmark]
	public void CreateAndOpenConnection()
	{
		using var connection = _provider.CreateConnection();
		connection.Open();
	}

	#endregion

	#region Connection Pool Simulation

	/// <summary>
	/// Benchmark: Connection reuse pattern.
	/// </summary>
	[Benchmark]
	public void ConnectionReusePattern()
	{
		// Simulate typical connection usage
		using var connection = _provider.CreateConnection();
		connection.Open();
		// Work would happen here
		connection.Close();
	}

	/// <summary>
	/// Benchmark: Concurrent connection creation.
	/// </summary>
	[Benchmark]
	public async Task ConcurrentConnectionCreation()
	{
		var tasks = new Task[10];
		for (int i = 0; i < 10; i++)
		{
			tasks[i] = Task.Run(async () =>
			{
				var connection = await _provider.CreateConnectionAsync(CancellationToken.None);
				connection.Dispose();
			});
		}

		await Task.WhenAll(tasks);
	}

	#endregion

	#region Transaction Benchmarks

	/// <summary>
	/// Benchmark: Begin transaction.
	/// </summary>
	[Benchmark]
	public void BeginTransaction()
	{
		using var connection = _provider.CreateConnection();
		connection.Open();
		using var transaction = connection.BeginTransaction();
		transaction.Commit();
	}

	/// <summary>
	/// Benchmark: Transaction rollback.
	/// </summary>
	[Benchmark]
	public void TransactionRollback()
	{
		using var connection = _provider.CreateConnection();
		connection.Open();
		using var transaction = connection.BeginTransaction();
		transaction.Rollback();
	}

	#endregion

	#region Provider Lifecycle

	/// <summary>
	/// Benchmark: Create new provider.
	/// </summary>
	[Benchmark]
	public void CreateNewProvider()
	{
		var options = Options.Create(new InMemoryProviderOptions { Name = $"bench-{Guid.NewGuid():N}" });
		using var provider = new InMemoryPersistenceProvider(options, NullLogger<InMemoryPersistenceProvider>.Instance);
	}

	#endregion
}
