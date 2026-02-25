// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Data.SqlServer.Inbox;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Testcontainers.MsSql;

namespace Excalibur.Dispatch.Benchmarks.Patterns;

/// <summary>
/// Benchmarks for InboxProcessor throughput operations.
/// Measures message processing performance for the inbox pattern.
/// </summary>
/// <remarks>
/// Performance Targets (from sprint-34-plan.md):
/// - InboxProcessor Throughput: &gt;10,000 msg/sec (MUST PASS)
/// - Memory Allocation: &lt;1KB per batch (SHOULD PASS)
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class InboxThroughputBenchmarks
{
	private MsSqlContainer? _sqlContainer;
	private SqlServerInboxStore? _inboxStore;
	private string? _connectionString;

	/// <summary>
	/// Number of messages to pre-populate for throughput tests.
	/// </summary>
	[Params(100, 1000, 10000)]
	public int MessageCount { get; set; }

	/// <summary>
	/// Initialize SQL Server container and inbox store before benchmarks.
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

		// Create Inbox tables
		await CreateInboxTablesAsync();

		// Initialize inbox store with new API
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerInboxOptions
		{
			ConnectionString = _connectionString,
			SchemaName = "dispatch",
			TableName = "inbox"
		});
		var logger = NullLoggerFactory.Instance.CreateLogger<SqlServerInboxStore>();
		_inboxStore = new SqlServerInboxStore(options, logger);

		// Pre-populate inbox with messages
		await PopulateInboxAsync(MessageCount);
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
	/// Benchmark: Get all entries from inbox (batch retrieval).
	/// Target: &gt;10,000 msg/sec throughput.
	/// </summary>
	[Benchmark(Baseline = true)]
	public async Task<int> GetAllEntries()
	{
		var entries = await _inboxStore.GetAllEntriesAsync(CancellationToken.None);
		return entries.Count();
	}

	/// <summary>
	/// Benchmark: Get inbox statistics.
	/// </summary>
	[Benchmark]
	public async Task<long> GetStatistics()
	{
		var stats = await _inboxStore.GetStatisticsAsync(CancellationToken.None);
		return stats.TotalEntries;
	}

	/// <summary>
	/// Benchmark: Create inbox entry (deduplication check).
	/// </summary>
	[Benchmark]
	public async Task CreateEntry()
	{
		var messageId = Guid.NewGuid().ToString();
		_ = await _inboxStore.CreateEntryAsync(
			messageId,
			"TestHandler",
			"TestEvent",
			new byte[1024],
			new Dictionary<string, object> { ["CorrelationId"] = Guid.NewGuid().ToString() },
			CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Check if message is processed (idempotency lookup).
	/// </summary>
	[Benchmark]
	public async Task<bool> IsAlreadyProcessed()
	{
		// Use a known message ID from pre-populated data
		return await _inboxStore.IsProcessedAsync("msg-0", "TestHandler", CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Full message processing cycle (create + mark processed).
	/// </summary>
	[Benchmark]
	public async Task FullProcessingCycle()
	{
		var messageId = Guid.NewGuid().ToString();

		// Create entry
		_ = await _inboxStore.CreateEntryAsync(
			messageId,
			"TestHandler",
			"TestEvent",
			new byte[1024],
			new Dictionary<string, object> { ["CorrelationId"] = Guid.NewGuid().ToString() },
			CancellationToken.None);

		// Mark as processed
		await _inboxStore.MarkProcessedAsync(messageId, "TestHandler", CancellationToken.None);
	}

	private async Task PopulateInboxAsync(int messageCount)
	{
		for (var i = 0; i < messageCount; i++)
		{
			_ = await _inboxStore.CreateEntryAsync(
				$"msg-{i}",
				"TestHandler",
				"TestEvent",
				new byte[1024],
				new Dictionary<string, object>
				{
					["UserId"] = "benchmark-user",
					["TenantId"] = "benchmark-tenant",
					["CorrelationId"] = Guid.NewGuid().ToString(),
				},
				CancellationToken.None);
		}
	}

	private async Task CreateInboxTablesAsync()
	{
		const string createTableSql = """
			-- Create dispatch schema if it doesn't exist
			IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'dispatch')
			BEGIN
				EXEC('CREATE SCHEMA dispatch');
			END;

			-- Create dispatch.inbox deduplication table
			IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'inbox' AND schema_id = SCHEMA_ID('dispatch'))
			BEGIN
				CREATE TABLE dispatch.inbox (
					MessageId NVARCHAR(200) NOT NULL,
					HandlerType NVARCHAR(500) NOT NULL,
					MessageType NVARCHAR(500) NOT NULL,
					Payload VARBINARY(MAX) NULL,
					Metadata NVARCHAR(MAX) NULL,
					CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
					ProcessedAt DATETIMEOFFSET NULL,
					FailedAt DATETIMEOFFSET NULL,
					ErrorMessage NVARCHAR(MAX) NULL,
					CONSTRAINT PK_dispatch_inbox PRIMARY KEY (MessageId, HandlerType)
				);
			END;
			""";

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync();

		await using var command = new SqlCommand(createTableSql, connection);
		_ = await command.ExecuteNonQueryAsync();
	}
}
