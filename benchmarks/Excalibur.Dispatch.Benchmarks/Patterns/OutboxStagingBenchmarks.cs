// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;

using Excalibur.Outbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Testcontainers.MsSql;

namespace Excalibur.Dispatch.Benchmarks.Patterns;

/// <summary>
/// Benchmarks for outbox message staging operations.
/// Measures performance of single, batch, and concurrent staging scenarios.
/// </summary>
/// <remarks>
/// Performance Targets (from testing-and-benchmarking-strategy-spec.md):
/// - Single message staging: &lt; 5ms (P50), &lt; 10ms (P95)
/// - Batch staging (100 messages): &lt; 50ms (P50), &lt; 100ms (P95)
/// - Concurrent staging: no degradation with up to 10 concurrent transactions
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class OutboxStagingBenchmarks
{
	private MsSqlContainer? _sqlContainer;
	private SqlServerOutboxStore? _outboxStore;
	private string? _connectionString;

	/// <summary>
	/// Initialize SQL Server container and outbox store before benchmarks.
	/// </summary>
	[GlobalSetup]
	public async Task GlobalSetup()
	{
		// Start SQL Server container
		_sqlContainer = new MsSqlBuilder()
			.WithImage("mcr.microsoft.com/mssql/server:2022-latest")
			.Build();

		await _sqlContainer.StartAsync();
		_connectionString = _sqlContainer.GetConnectionString();

		// Create outbox tables
		await CreateOutboxTablesAsync();

		// Initialize outbox store
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = _connectionString,
			SchemaName = "dbo",
			OutboxTableName = "OutboxMessages",
			TransportsTableName = "OutboxMessageTransports"
		});
		var logger = NullLoggerFactory.Instance.CreateLogger<SqlServerOutboxStore>();
		_outboxStore = new SqlServerOutboxStore(options, logger);
	}

	/// <summary>
	/// Cleanup SQL Server container after benchmarks.
	/// </summary>
	[GlobalCleanup]
	public async Task GlobalCleanup()
	{
		if (_sqlContainer != null)
		{
			await _sqlContainer.DisposeAsync();
		}
	}

	/// <summary>
	/// Benchmark: Stage a single small message (1KB payload).
	/// </summary>
	[Benchmark(Baseline = true)]
	public async Task StageSingleSmall()
	{
		var message = CreateTestOutboxMessage(payloadSizeKb: 1);
		await _outboxStore.StageMessageAsync(message, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Stage a medium message (10KB payload).
	/// </summary>
	[Benchmark]
	public async Task StageSingleMedium()
	{
		var message = CreateTestOutboxMessage(payloadSizeKb: 10);
		await _outboxStore.StageMessageAsync(message, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Stage a large message (100KB payload).
	/// </summary>
	[Benchmark]
	public async Task StageSingleLarge()
	{
		var message = CreateTestOutboxMessage(payloadSizeKb: 100);
		await _outboxStore.StageMessageAsync(message, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Stage a batch of 10 messages.
	/// </summary>
	[Benchmark]
	public async Task StageBatch10()
	{
		var tasks = new List<Task>(10);
		for (var i = 0; i < 10; i++)
		{
			var message = CreateTestOutboxMessage(payloadSizeKb: 1);
			tasks.Add(_outboxStore.StageMessageAsync(message, CancellationToken.None).AsTask());
		}
		await Task.WhenAll(tasks);
	}

	/// <summary>
	/// Benchmark: Stage a batch of 100 messages.
	/// </summary>
	[Benchmark]
	public async Task StageBatch100()
	{
		var tasks = new List<Task>(100);
		for (var i = 0; i < 100; i++)
		{
			var message = CreateTestOutboxMessage(payloadSizeKb: 1);
			tasks.Add(_outboxStore.StageMessageAsync(message, CancellationToken.None).AsTask());
		}
		await Task.WhenAll(tasks);
	}

	/// <summary>
	/// Benchmark: Stage a very large payload (1MB).
	/// </summary>
	[Benchmark]
	public async Task StageLargePayload()
	{
		var message = CreateTestOutboxMessage(payloadSizeKb: 1024); // 1MB
		await _outboxStore.StageMessageAsync(message, CancellationToken.None);
	}

	private static OutboundMessage CreateTestOutboxMessage(int payloadSizeKb = 1)
	{
		var payloadData = new byte[payloadSizeKb * 1024];
		Random.Shared.NextBytes(payloadData);

		return new OutboundMessage(
			"TestEvent",
			payloadData,
			"test-destination",
			new Dictionary<string, object>
			{
				["UserId"] = "benchmark-user",
				["TenantId"] = "benchmark-tenant",
				["CorrelationId"] = Guid.NewGuid().ToString(),
			});
	}

	private async Task CreateOutboxTablesAsync()
	{
		const string createTableSql = """
			IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OutboxMessages' AND schema_id = SCHEMA_ID('dbo'))
			BEGIN
				CREATE TABLE [dbo].[OutboxMessages] (
					[Id] NVARCHAR(200) NOT NULL PRIMARY KEY,
					[MessageType] NVARCHAR(500) NOT NULL,
					[Payload] VARBINARY(MAX) NOT NULL,
					[Destination] NVARCHAR(500) NOT NULL,
					[Headers] NVARCHAR(MAX) NULL,
					[CreatedAt] DATETIMEOFFSET NOT NULL,
					[SentAt] DATETIMEOFFSET NULL,
					[FailedAt] DATETIMEOFFSET NULL,
					[ErrorMessage] NVARCHAR(MAX) NULL,
					[RetryCount] INT NOT NULL DEFAULT 0,
					[NextRetryAt] DATETIMEOFFSET NULL,
					[Status] INT NOT NULL DEFAULT 0,
					[ScheduledAt] DATETIMEOFFSET NULL,
					[AggregateId] NVARCHAR(200) NULL,
					[AggregateType] NVARCHAR(500) NULL,
					[CorrelationId] NVARCHAR(200) NULL,
					INDEX IX_OutboxMessages_Status_CreatedAt (Status, CreatedAt),
					INDEX IX_OutboxMessages_ScheduledAt (ScheduledAt) WHERE ScheduledAt IS NOT NULL
				);
			END;

			IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OutboxMessageTransports' AND schema_id = SCHEMA_ID('dbo'))
			BEGIN
				CREATE TABLE [dbo].[OutboxMessageTransports] (
					[Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
					[MessageId] NVARCHAR(200) NOT NULL,
					[TransportName] NVARCHAR(200) NOT NULL,
					[Status] INT NOT NULL DEFAULT 0,
					[SentAt] DATETIMEOFFSET NULL,
					[FailedAt] DATETIMEOFFSET NULL,
					[ErrorMessage] NVARCHAR(MAX) NULL,
					[RetryCount] INT NOT NULL DEFAULT 0,
					[NextRetryAt] DATETIMEOFFSET NULL,
					CONSTRAINT FK_OutboxMessageTransports_Message FOREIGN KEY (MessageId) REFERENCES [dbo].[OutboxMessages](Id),
					INDEX IX_OutboxMessageTransports_MessageId (MessageId),
					INDEX IX_OutboxMessageTransports_Status (Status)
				);
			END;
			""";

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync();

		await using var command = new SqlCommand(createTableSql, connection);
		_ = await command.ExecuteNonQueryAsync();
	}
}
