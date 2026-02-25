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
/// Benchmarks for outbox message polling operations.
/// Measures performance of retrieving unsent messages for publishing.
/// </summary>
/// <remarks>
/// Performance Targets (from testing-and-benchmarking-strategy-spec.md):
/// - Poll empty outbox: &lt; 5ms (P50), &lt; 10ms (P95)
/// - Poll with 10 messages: &lt; 10ms (P50), &lt; 20ms (P95)
/// - Poll with 100 messages: &lt; 50ms (P50), &lt; 100ms (P95)
/// - Poll with 1000 messages: &lt; 200ms (P50), &lt; 400ms (P95)
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class OutboxPollingBenchmarks
{
	private MsSqlContainer? _sqlContainer;
	private SqlServerOutboxStore? _outboxStore;
	private string? _connectionString;

	/// <summary>
	/// Number of messages to pre-populate for polling tests.
	/// </summary>
	[Params(0, 10, 100, 1000)]
	public int MessageCount { get; set; }

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

		// Pre-populate with messages
		await PopulateOutboxAsync(MessageCount);
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
	/// Benchmark: Poll for unsent messages (batch of 10).
	/// </summary>
	[Benchmark(Baseline = true)]
	public async Task<int> PollBatch10()
	{
		var messages = await _outboxStore.GetUnsentMessagesAsync(batchSize: 10, CancellationToken.None);
		return messages.Count();
	}

	/// <summary>
	/// Benchmark: Poll for unsent messages (batch of 100).
	/// </summary>
	[Benchmark]
	public async Task<int> PollBatch100()
	{
		var messages = await _outboxStore.GetUnsentMessagesAsync(batchSize: 100, CancellationToken.None);
		return messages.Count();
	}

	/// <summary>
	/// Benchmark: Poll for unsent messages (batch of 500).
	/// </summary>
	[Benchmark]
	public async Task<int> PollBatch500()
	{
		var messages = await _outboxStore.GetUnsentMessagesAsync(batchSize: 500, CancellationToken.None);
		return messages.Count();
	}

	/// <summary>
	/// Benchmark: Get outbox statistics.
	/// </summary>
	[Benchmark]
	public async Task<long> GetStatistics()
	{
		var stats = await _outboxStore.GetStatisticsAsync(CancellationToken.None);
		return stats.StagedMessageCount;
	}

	private async Task PopulateOutboxAsync(int messageCount)
	{
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
		}
	}

	private async Task CreateOutboxTablesAsync()
	{
		const string createTableSql = """
			-- Create OutboxMessages table
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

			-- Create OutboxMessageTransports table
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
