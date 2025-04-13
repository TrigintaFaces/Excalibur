using System.Data;

using Dapper;

using Excalibur.Core.Domain.Events;
using Excalibur.Core.Extensions;
using Excalibur.Data;
using Excalibur.Data.Outbox;
using Excalibur.Domain;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Infrastructure.TestBaseClasses.PersistenceOnly;
using Excalibur.Tests.Mothers;
using Excalibur.Tests.Shared;

using MediatR;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit.Abstractions;

namespace Excalibur.Tests.Functional.Data.Outbox;

public class OutboxManagerShould(SqlServerContainerFixture fixture, ITestOutputHelper output)
	: SqlServerPersistenceOnlyTestBase(fixture, output)
{
	[Fact]
	public async Task ProcessEntireOutboxQueueSuccessfully()
	{
		// Arrange
		var outbox = GetRequiredService<IOutbox>();
		var outboxManager = GetRequiredService<IOutboxManager>();
		var domainDb = GetRequiredService<IDomainDb>();
		var connection = domainDb.Connection;

		await DatabaseCleaner.ClearOutboxTablesAsync(connection).ConfigureAwait(true);

		// Prepare test data - multiple messages
		var messages = new List<OutboxMessage>();
		for (var i = 1; i <= 20; i++)
		{
			messages.Add(new OutboxMessage
			{
				MessageId = Uuid7Extensions.GenerateString(),
				MessageBody = new TestDomainEvent { Id = i, Name = $"Test Event {i}" },
				MessageHeaders = new Dictionary<string, string> { ["test-header"] = $"test-value-{i}" }
			});
		}

		// Save messages to outbox
		_ = await outbox.SaveMessagesAsync(messages).ConfigureAwait(true);

		// Verify messages were saved
		var savedCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Outbox").ConfigureAwait(true);
		savedCount.ShouldBe(1); // One outbox record containing multiple messages

		// Act
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		var dispatchResult = await outboxManager.RunOutboxDispatchAsync("functional-test-dispatcher", cts.Token)
			.ConfigureAwait(true);

		// Verify all records were processed
		var remainingCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Outbox").ConfigureAwait(true);
		var deadLetterCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM OutboxDeadLetter").ConfigureAwait(true);

		// Assert
		dispatchResult.ShouldBeGreaterThan(0); // Should successfully dispatch messages
		remainingCount.ShouldBe(0); // All records should be processed
		deadLetterCount.ShouldBe(0); // No records should end up in dead letter queue
	}

	[Fact]
	public async Task HandleMultipleBatchesProcessingAllMessages()
	{
		// Arrange
		var outbox = GetRequiredService<IOutbox>();
		var outboxManager = GetRequiredService<IOutboxManager>();
		var domainDb = GetRequiredService<IDomainDb>();
		var connection = domainDb.Connection;

		await DatabaseCleaner.ClearOutboxTablesAsync(connection).ConfigureAwait(true);

		// Create multiple outbox records with single messages to test batch processing
		for (var i = 1; i <= 5; i++)
		{
			var message = new OutboxMessage
			{
				MessageId = Uuid7Extensions.GenerateString(),
				MessageBody = new TestDomainEvent { Id = i, Name = $"Batch Test Event {i}" },
				MessageHeaders = new Dictionary<string, string> { ["batch-test"] = $"batch-value-{i}" }
			};

			_ = await outbox.SaveMessagesAsync([message]).ConfigureAwait(true);
		}

		// Verify messages were saved
		var savedCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Outbox").ConfigureAwait(true);
		savedCount.ShouldBe(5); // Five separate outbox records

		// Act
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		var dispatchResult = await outboxManager.RunOutboxDispatchAsync("batch-test-dispatcher", cts.Token)
			.ConfigureAwait(true);

		// Verify all records were processed
		var remainingCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Outbox").ConfigureAwait(true);

		// Assert
		dispatchResult.ShouldBe(5); // Should match the number of messages dispatched
		remainingCount.ShouldBe(0); // All records should be processed
	}

	[Fact]
	public async Task HandlesDisposalGracefullyDuringOperation()
	{
		// Arrange
		var outbox = GetRequiredService<IOutbox>();
		var outboxManager = GetRequiredService<IOutboxManager>();

		// Create a large number of outbox messages
		var messages = new List<OutboxMessage>();
		for (var i = 1; i <= 50; i++)
		{
			messages.Add(new OutboxMessage
			{
				MessageId = Uuid7Extensions.GenerateString(),
				MessageBody = new TestDomainEvent { Id = i, Name = $"Disposal Test Event {i}" },
				MessageHeaders = new Dictionary<string, string> { ["disposal-test"] = $"value-{i}" }
			});
		}

		_ = await outbox.SaveMessagesAsync(messages).ConfigureAwait(true);

		// Act Start the outbox manager in a separate task
		var processTask = Task.Run(async () =>
		{
			_ = await outboxManager.RunOutboxDispatchAsync("disposal-test-dispatcher", CancellationToken.None)
				.ConfigureAwait(true);
		});

		// Allow some processing to occur
		await Task.Delay(500).ConfigureAwait(true);

		// Dispose the outbox manager while it's running
		await outboxManager.DisposeAsync().ConfigureAwait(true);

		// Wait for the process task to complete with a timeout
		var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
		var completedTask = await Task.WhenAny(processTask, timeoutTask).ConfigureAwait(true);

		// Assert
		completedTask.ShouldBe(processTask, "Process task should complete after disposal without timing out");

		// Verify no exceptions were thrown
		Should.NotThrow(() => processTask.GetAwaiter().GetResult());
	}

	protected override void AddServices(IServiceCollection services, IConfiguration configuration)
	{
		base.AddServices(services, configuration);

		_ = services.AddScoped<IDomainDb, TestDb>();

		// Configure OutboxConfiguration options by creating a new instance
		_ = services.AddSingleton(
			Options.Create(new OutboxConfiguration
			{
				TableName = "Outbox",
				DeadLetterTableName = "OutboxDeadLetter",
				MaxAttempts = 3,
				DispatcherTimeoutMilliseconds = 60000,
				QueueSize = 100,
				ProducerBatchSize = 10,
				ConsumerBatchSize = 5
			}));

		_ = services.AddExcaliburDataOutboxServices(configuration);
		_ = services.AddExcaliburMediatorOutboxMessageDispatcher();
	}

	protected override async Task OnDatabaseInitialized(IDbConnection connection)
	{
		await base.OnDatabaseInitialized(connection).ConfigureAwait(true);

		// Create the outbox tables if they don't exist
		_ = await connection.ExecuteAsync(@"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Outbox')
            BEGIN
                CREATE TABLE Outbox (
                    OutboxId uniqueidentifier PRIMARY KEY,
                    EventData nvarchar(max) NOT NULL,
                    CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),
                    DispatcherId nvarchar(100) NULL,
                    DispatcherTimeout datetime2 NULL,
                    Attempts int NOT NULL DEFAULT 0
                );
            END

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OutboxDeadLetter')
            BEGIN
                CREATE TABLE OutboxDeadLetter (
                    OutboxId uniqueidentifier PRIMARY KEY,
                    EventData nvarchar(max) NOT NULL,
                    ErrorMessage nvarchar(max) NULL,
                    OriginalAttempts int NOT NULL DEFAULT 0,
                    CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE()
                );
            END").ConfigureAwait(true);
	}

	private sealed class TestDomainEvent : IDomainEvent, INotification
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
	}
}
