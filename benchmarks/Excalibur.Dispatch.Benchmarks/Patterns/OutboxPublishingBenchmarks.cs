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
/// Benchmarks for outbox message publishing operations.
/// Measures performance of reservation, deletion (publish), retries, and dead letter operations.
/// </summary>
/// <remarks>
/// Performance Targets (from testing-and-benchmarking-strategy-spec.md):
/// - Single message publish: &lt; 5ms (P50), &lt; 10ms (P95)
/// - Batch publish (100 messages): &lt; 50ms (P50), &lt; 100ms (P95)
/// - Publishing with retries: &lt; 10ms per retry (P50), &lt; 20ms (P95)
/// - Dead letter operations: &lt; 10ms (P50), &lt; 20ms (P95)
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class OutboxPublishingBenchmarks
{
	private MsSqlContainer? _sqlContainer;
	private SqlServerOutboxStore? _outboxStore;
	private string? _connectionString;
	private List<string> _messageIds = new();

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

		// Pre-populate with messages to publish
		_messageIds = await PopulateOutboxAsync(1000);
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
	/// Benchmark: Stage a single message (outbox write).
	/// </summary>
	[Benchmark(Baseline = true)]
	public async Task StageMessage()
	{
		var message = new OutboundMessage(
			"TestEvent",
			new byte[1024],
			"test-destination",
			new Dictionary<string, object> { ["CorrelationId"] = Guid.NewGuid().ToString() });
		await _outboxStore.StageMessageAsync(message, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Mark a message as sent (publish completion).
	/// </summary>
	[Benchmark]
	public async Task MarkSent()
	{
		if (_messageIds.Count > 0)
		{
			var messageId = _messageIds[Random.Shared.Next(_messageIds.Count)];
			await _outboxStore.MarkSentAsync(messageId, CancellationToken.None);
		}
	}

	/// <summary>
	/// Benchmark: Full publishing cycle (stage + mark sent).
	/// </summary>
	[Benchmark]
	public async Task FullPublishCycle()
	{
		var message = new OutboundMessage(
			"TestEvent",
			new byte[1024],
			"test-destination",
			new Dictionary<string, object> { ["CorrelationId"] = Guid.NewGuid().ToString() });
		await _outboxStore.StageMessageAsync(message, CancellationToken.None);
		await _outboxStore.MarkSentAsync(message.Id, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Get and mark batch of messages as sent.
	/// </summary>
	[Benchmark]
	public async Task<int> ProcessBatch100()
	{
		var messages = await _outboxStore.GetUnsentMessagesAsync(batchSize: 100, CancellationToken.None);
		var count = 0;
		foreach (var message in messages)
		{
			await _outboxStore.MarkSentAsync(message.Id, CancellationToken.None);
			count++;
		}
		return count;
	}

	private async Task<List<string>> PopulateOutboxAsync(int messageCount)
	{
		var ids = new List<string>(messageCount);
		for (var i = 0; i < messageCount; i++)
		{
			var message = new OutboundMessage(
				"TestEvent",
				new byte[1024],
				"test-destination",
				new Dictionary<string, object>
				{
					["CorrelationId"] = Guid.NewGuid().ToString(),
					["Index"] = i
				});
			await _outboxStore.StageMessageAsync(message, CancellationToken.None);
			ids.Add(message.Id);
		}
		return ids;
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
