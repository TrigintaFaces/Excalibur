// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Saga.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Benchmarks.Saga;

/// <summary>
/// Benchmarks for SqlServerSagaStore operations.
/// </summary>
/// <remarks>
/// Requires a SQL Server instance. Set the BENCHMARK_SQL_CONNECTIONSTRING environment variable
/// to enable these benchmarks. When not set, all benchmarks return immediately.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class SqlServerSagaStoreBenchmarks
{
	private static readonly string? ConnectionString =
		Environment.GetEnvironmentVariable("BENCHMARK_SQL_CONNECTIONSTRING");

	private SqlServerSagaStore? _sagaStore;
	private Guid _preloadedSagaId;
	private Guid _iterationSagaId;
	private string _correlationId = null!;

	private static bool IsAvailable => ConnectionString is not null;

	[GlobalSetup]
	public void GlobalSetup()
	{
		if (!IsAvailable)
		{
			return;
		}

		_sagaStore = new SqlServerSagaStore(
			ConnectionString!,
			NullLogger<SqlServerSagaStore>.Instance,
			new BenchmarkJsonSerializer());

		// Ensure schema exists
		EnsureSchemaAsync().GetAwaiter().GetResult();

		// Pre-populate a saga for load benchmarks
		_preloadedSagaId = Guid.NewGuid();
		_correlationId = Guid.NewGuid().ToString();

		var sagaState = new BenchmarkSagaState
		{
			SagaId = _preloadedSagaId,
			OrderId = "order-benchmark-001",
			CorrelationId = _correlationId,
			CurrentStep = "Completed",
			Completed = false,
		};

		_sagaStore.SaveAsync(sagaState, CancellationToken.None).GetAwaiter().GetResult();
	}

	[IterationSetup]
	public void IterationSetup()
	{
		_iterationSagaId = Guid.NewGuid();
	}

	#region Load Benchmarks

	/// <summary>
	/// Benchmark: Load an existing saga state by ID.
	/// </summary>
	[Benchmark(Baseline = true)]
	public async Task<BenchmarkSagaState?> LoadSagaState()
	{
		if (!IsAvailable)
		{
			return null;
		}

		return await _sagaStore!.LoadAsync<BenchmarkSagaState>(
			_preloadedSagaId, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Load a non-existent saga (miss scenario).
	/// </summary>
	[Benchmark]
	public async Task<BenchmarkSagaState?> LoadSagaStateMiss()
	{
		if (!IsAvailable)
		{
			return null;
		}

		return await _sagaStore!.LoadAsync<BenchmarkSagaState>(
			Guid.NewGuid(), CancellationToken.None);
	}

	#endregion

	#region Save Benchmarks

	/// <summary>
	/// Benchmark: Save a new saga state.
	/// </summary>
	[Benchmark]
	public async Task SaveSagaState()
	{
		if (!IsAvailable)
		{
			return;
		}

		var sagaState = new BenchmarkSagaState
		{
			SagaId = _iterationSagaId,
			OrderId = $"order-{_iterationSagaId:N}",
			CorrelationId = Guid.NewGuid().ToString(),
			CurrentStep = "Started",
			Completed = false,
		};

		await _sagaStore!.SaveAsync(sagaState, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Update an existing saga state (upsert).
	/// </summary>
	[Benchmark]
	public async Task UpdateSagaState()
	{
		if (!IsAvailable)
		{
			return;
		}

		var sagaState = new BenchmarkSagaState
		{
			SagaId = _preloadedSagaId,
			OrderId = "order-benchmark-001",
			CorrelationId = _correlationId,
			CurrentStep = $"Updated-{DateTime.UtcNow.Ticks}",
			Completed = false,
		};

		await _sagaStore!.SaveAsync(sagaState, CancellationToken.None);
	}

	#endregion

	#region Round-Trip Benchmarks

	/// <summary>
	/// Benchmark: Full save-then-load round trip.
	/// </summary>
	[Benchmark]
	public async Task SaveAndLoadRoundTrip()
	{
		if (!IsAvailable)
		{
			return;
		}

		var sagaId = Guid.NewGuid();
		var sagaState = new BenchmarkSagaState
		{
			SagaId = sagaId,
			OrderId = $"order-{sagaId:N}",
			CorrelationId = Guid.NewGuid().ToString(),
			CurrentStep = "RoundTrip",
			Completed = false,
		};

		await _sagaStore!.SaveAsync(sagaState, CancellationToken.None);
		_ = await _sagaStore.LoadAsync<BenchmarkSagaState>(sagaId, CancellationToken.None);
	}

	#endregion

	#region Helpers

	private static async Task EnsureSchemaAsync()
	{
		await using var connection = new SqlConnection(ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = new SqlCommand("""
			IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'dispatch')
				EXEC('CREATE SCHEMA dispatch');

			IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'sagas' AND schema_id = SCHEMA_ID('dispatch'))
			CREATE TABLE [dispatch].[sagas] (
				[SagaId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
				[SagaType] NVARCHAR(500) NOT NULL,
				[StateData] NVARCHAR(MAX) NOT NULL,
				[Completed] BIT NOT NULL DEFAULT 0,
				[CreatedAt] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
				[UpdatedAt] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
				[RowVersion] ROWVERSION NOT NULL
			);
			""", connection);

		await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	#endregion
}

/// <summary>
/// Test saga state for benchmark scenarios.
/// </summary>
public sealed class BenchmarkSagaState : SagaState
{
	public string OrderId { get; set; } = string.Empty;
	public string CorrelationId { get; set; } = string.Empty;
	public string CurrentStep { get; set; } = string.Empty;
}

/// <summary>
/// Minimal JSON serializer for benchmark scenarios using System.Text.Json.
/// </summary>
internal sealed class BenchmarkJsonSerializer : IJsonSerializer
{
	public string Serialize(object value, Type type) =>
		System.Text.Json.JsonSerializer.Serialize(value, type);

	public object? Deserialize(string json, Type type) =>
		System.Text.Json.JsonSerializer.Deserialize(json, type);
}
