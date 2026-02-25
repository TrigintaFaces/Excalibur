// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;

using Excalibur.Outbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Testcontainers.MsSql;

namespace Excalibur.Dispatch.Benchmarks.LoadTests;

/// <summary>
/// Outbox staging benchmarks under backpressure scenarios.
/// Measures memory growth and latency degradation when staging rate exceeds processing rate.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
[BenchmarkCategory("LoadTest")]
public class OutboxBackpressureLoadTest
{
	private MsSqlContainer? _sqlContainer;
	private SqlServerOutboxStore? _outboxStore;
	private string? _connectionString;

	/// <summary>
	/// Number of messages to stage in the backpressure scenario.
	/// </summary>
	[Params(100, 1_000)]
	public int MessageCount { get; set; }

	/// <summary>
	/// Payload size in kilobytes for each outbox message.
	/// </summary>
	[Params(1, 10)]
	public int PayloadSizeKb { get; set; }

	/// <summary>
	/// Initialize SQL Server container and outbox store.
	/// </summary>
	[GlobalSetup]
	public async Task GlobalSetup()
	{
		_sqlContainer = new MsSqlBuilder()
			.WithImage("mcr.microsoft.com/mssql/server:2022-latest")
			.Build();

		await _sqlContainer.StartAsync().ConfigureAwait(false);
		_connectionString = _sqlContainer.GetConnectionString();

		await CreateOutboxTablesAsync().ConfigureAwait(false);

		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = _connectionString,
			SchemaName = "dbo",
			OutboxTableName = "OutboxMessages",
			TransportsTableName = "OutboxMessageTransports",
		});
		var logger = NullLoggerFactory.Instance.CreateLogger<SqlServerOutboxStore>();
		_outboxStore = new SqlServerOutboxStore(options, logger);
	}

	/// <summary>
	/// Cleanup SQL Server container.
	/// </summary>
	[GlobalCleanup]
	public async Task GlobalCleanup()
	{
		if (_sqlContainer != null)
		{
			await _sqlContainer.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Truncate outbox tables between iterations to prevent accumulation.
	/// </summary>
	[IterationSetup]
	public void IterationSetup()
	{
		using var connection = new SqlConnection(_connectionString);
		connection.Open();
		using var cmd = new SqlCommand(
			"TRUNCATE TABLE [dbo].[OutboxMessageTransports]; DELETE FROM [dbo].[OutboxMessages];",
			connection);
		_ = cmd.ExecuteNonQuery();
	}

	/// <summary>
	/// Benchmark: Sustained sequential staging — stages messages one at a time without
	/// any concurrent processing to simulate backpressure.
	/// </summary>
	[Benchmark(Baseline = true, Description = "Sequential Staging (Backpressure)")]
	public async Task SequentialStagingBackpressure()
	{
		for (var i = 0; i < MessageCount; i++)
		{
			var message = CreateOutboxMessage(PayloadSizeKb);
			await _outboxStore!.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Benchmark: Concurrent staging — 10 concurrent tasks staging simultaneously.
	/// Simulates high-throughput producers overwhelming the outbox.
	/// </summary>
	[Benchmark(Description = "Concurrent Staging (10 producers)")]
	public async Task ConcurrentStagingBackpressure()
	{
		var messagesPerProducer = MessageCount / 10;
		var tasks = new Task[10];

		for (var t = 0; t < 10; t++)
		{
			tasks[t] = Task.Run(async () =>
			{
				for (var i = 0; i < messagesPerProducer; i++)
				{
					var message = CreateOutboxMessage(PayloadSizeKb);
					await _outboxStore!.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
				}
			});
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	/// <summary>
	/// Benchmark: Burst staging — stages all messages as fast as possible,
	/// measuring peak throughput and memory allocation.
	/// </summary>
	[Benchmark(Description = "Burst Staging")]
	public async Task BurstStaging()
	{
		var tasks = new List<Task>(MessageCount);

		for (var i = 0; i < MessageCount; i++)
		{
			var message = CreateOutboxMessage(PayloadSizeKb);
			tasks.Add(_outboxStore!.StageMessageAsync(message, CancellationToken.None).AsTask());
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	private static OutboundMessage CreateOutboxMessage(int payloadSizeKb)
	{
		var payloadData = new byte[payloadSizeKb * 1024];
		Random.Shared.NextBytes(payloadData);

		return new OutboundMessage(
			"BackpressureTestEvent",
			payloadData,
			"test-destination",
			new Dictionary<string, object>
			{
				["CorrelationId"] = Guid.NewGuid().ToString(),
				["ProducerId"] = Environment.CurrentManagedThreadId.ToString(),
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
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = new SqlCommand(createTableSql, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}
}
