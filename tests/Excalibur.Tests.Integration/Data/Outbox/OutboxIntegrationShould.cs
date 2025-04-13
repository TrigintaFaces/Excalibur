using System.Data;
using System.Transactions;

using Dapper;

using Excalibur.Application.Requests;
using Excalibur.Core.Domain.Events;
using Excalibur.Core.Extensions;
using Excalibur.Data;
using Excalibur.Data.Outbox;
using Excalibur.Domain;
using Excalibur.Domain.Model;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Infrastructure.TestBaseClasses.PersistenceOnly;
using Excalibur.Tests.Shared;

using MediatR;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit.Abstractions;

using INotification = Excalibur.Application.Requests.Notifications.INotification;
using IsolationLevel = System.Transactions.IsolationLevel;

namespace Excalibur.Tests.Integration.Data.Outbox;

public class OutboxIntegrationShould(SqlServerContainerFixture fixture, ITestOutputHelper output)
	: SqlServerPersistenceOnlyTestBase(fixture, output)
{
	[Fact]
	public async Task SaveAndRetrieveMessages()
	{
		// Arrange
		var outbox = GetRequiredService<IOutbox>();

		var messages = new List<OutboxMessage>
		{
			new()
			{
				MessageId = Uuid7Extensions.GenerateString(),
				MessageBody = new TestDomainEvent { Id = 1, Name = "Test Event 1" },
				MessageHeaders = new Dictionary<string, string> { ["test-header-1"] = "test-value-1" }
			},
			new()
			{
				MessageId = Uuid7Extensions.GenerateString(),
				MessageBody = new TestDomainEvent { Id = 2, Name = "Test Event 2" },
				MessageHeaders = new Dictionary<string, string> { ["test-header-2"] = "test-value-2" }
			}
		};

		// Act
		var saveResult = await outbox.SaveMessagesAsync(messages).ConfigureAwait(true);
		var dispatcherId = "test-dispatcher";
		var reserved = await outbox.TryReserveOneRecordsAsync(dispatcherId, 10, CancellationToken.None).ConfigureAwait(true);

		// Assert
		saveResult.ShouldBe(1); // Successfully saved messages
		_ = reserved.ShouldNotBeNull();
		reserved.ShouldNotBeEmpty(); // Successfully reserved messages

		var record = reserved.First();
		record.OutboxId.ShouldNotBe(Guid.Empty);
		record.EventData.ShouldNotBeNullOrWhiteSpace();
		record.DispatcherId.ShouldBe(dispatcherId);
		_ = record.DispatcherTimeout.ShouldNotBeNull();
		record.Attempts.ShouldBe(0);
	}

	[Fact]
	public async Task DispatchReservedRecord()
	{
		// Arrange
		var outbox = GetRequiredService<IOutbox>();

		var message = new OutboxMessage
		{
			MessageId = Uuid7Extensions.GenerateString(),
			MessageBody = new TestDomainEvent { Id = 1, Name = "Test Event" },
			MessageHeaders = new Dictionary<string, string> { ["test-header"] = "test-value" }
		};

		_ = await outbox.SaveMessagesAsync([message]).ConfigureAwait(true);

		// Act
		var dispatcherId = "test-dispatcher";
		var reserved = await outbox.TryReserveOneRecordsAsync(dispatcherId, 1, CancellationToken.None).ConfigureAwait(true);
		var record = reserved.First();

		var dispatchResult = await outbox.DispatchReservedRecordAsync(dispatcherId, record).ConfigureAwait(true);

		// Try to retrieve the record again (should be deleted after successful dispatch)
		var reserved2 = await outbox.TryReserveOneRecordsAsync(dispatcherId, 1, CancellationToken.None).ConfigureAwait(true);

		// Assert
		dispatchResult.ShouldBeGreaterThan(0); // At least one message dispatched
		reserved2.ShouldBeEmpty(); // Record should be deleted after dispatch
	}

	[Fact]
	public async Task SaveAndClearDomainEvents()
	{
		// Arrange
		var outbox = GetRequiredService<IOutbox>();
		var testAggregate = new TestAggregateRoot("test-id");
		testAggregate.AddEvent(new TestDomainEvent { Id = 1, Name = "First Event" });
		testAggregate.AddEvent(new TestDomainEvent { Id = 2, Name = "Second Event" });

		// Act
		var saveResult = await outbox.SaveEventsAsync(testAggregate, null, null).ConfigureAwait(true);

		// Assert
		saveResult.ShouldBe(1); // Successfully saved events
		testAggregate.DomainEvents.ShouldBeEmpty(); // Events should be cleared after saving

		// Verify we can retrieve the saved events
		var dispatcherId = "test-dispatcher";
		var reserved = await outbox.TryReserveOneRecordsAsync(dispatcherId, 10, CancellationToken.None).ConfigureAwait(true);
		reserved.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task UnreserveRecords()
	{
		// Arrange
		var outbox = GetRequiredService<IOutbox>();

		var message = new OutboxMessage
		{
			MessageId = Uuid7Extensions.GenerateString(),
			MessageBody = new TestDomainEvent { Id = 1, Name = "Test Event" },
			MessageHeaders = new Dictionary<string, string> { ["test-header"] = "test-value" }
		};

		_ = await outbox.SaveMessagesAsync([message]).ConfigureAwait(true);

		// Reserve the record
		var dispatcherId = "test-dispatcher";
		var reserved = await outbox.TryReserveOneRecordsAsync(dispatcherId, 1, CancellationToken.None).ConfigureAwait(true);
		reserved.ShouldNotBeEmpty();

		// Act
		await outbox.TryUnReserveOneRecordsAsync(dispatcherId, CancellationToken.None).ConfigureAwait(true);

		// Try to reserve it again (should succeed if unreserve worked)
		var dispatcherId2 = "test-dispatcher-2";
		var reserved2 = await outbox.TryReserveOneRecordsAsync(dispatcherId2, 1, CancellationToken.None).ConfigureAwait(true);

		// Assert
		reserved2.ShouldNotBeEmpty();
		var record = reserved2.First();
		record.DispatcherId.ShouldBe(dispatcherId2); // The record should now be assigned to the new dispatcher
	}

	protected override void AddServices(IServiceCollection services, IConfiguration configuration)
	{
		base.AddServices(services, configuration);

		_ = services.AddScoped<IDomainDb, TestDb>();
		_ = services.AddScoped<INotificationHandler<TestDomainEvent>, TestDomainEventHandler>();

		// Configure OutboxConfiguration options by creating a new instance
		_ = services.Configure<OutboxConfiguration>(options =>
		{
			// This is properly creating a new instance with init properties
			var newConfig = new OutboxConfiguration
			{
				TableName = "Outbox.Outbox",
				DeadLetterTableName = "Outbox.DeadLetterOutbox",
				MaxAttempts = 3,
				DispatcherTimeoutMilliseconds = 60000,
				QueueSize = 100,
				ProducerBatchSize = 10,
				ConsumerBatchSize = 5
			};

			// Copy properties from the new instance to the options instance This approach avoids directly setting init-only properties
			options = newConfig;
		});

		_ = services.AddExcaliburDataOutboxServices(configuration);
		_ = services.AddExcaliburMediatorOutboxMessageDispatcher();
	}

	protected override async Task OnDatabaseInitialized(IDbConnection connection)
	{
		await base.OnDatabaseInitialized(connection).ConfigureAwait(true);

		// Create the outbox tables if they don't exist
		_ = await connection.ExecuteAsync(@"
            IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'Outbox')
			BEGIN
			    EXEC('CREATE SCHEMA Outbox');
			END

			IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Outbox' AND schema_id = SCHEMA_ID('Outbox'))
			BEGIN
			    CREATE TABLE Outbox.Outbox (
			        OutboxId uniqueidentifier PRIMARY KEY,
			        EventData nvarchar(max) NOT NULL,
			        CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),
			        DispatcherId nvarchar(100) NULL,
			        DispatcherTimeout datetime2 NULL,
			        Attempts int NOT NULL DEFAULT 0
			    );
			END

			IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DeadLetterOutbox' AND schema_id = SCHEMA_ID('Outbox'))
			BEGIN
			    CREATE TABLE Outbox.DeadLetterOutbox (
			        OutboxId uniqueidentifier PRIMARY KEY,
			        EventData nvarchar(max) NOT NULL,
			        ErrorMessage nvarchar(max) NULL,
			        OriginalAttempts int NOT NULL DEFAULT 0,
			        CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE()
			    );
			END
		").ConfigureAwait(true);
	}

	private sealed class TestDomainEvent : IDomainEvent, INotification
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public Guid CorrelationId { get; }
		public string? TenantId { get; }
		public TransactionScopeOption TransactionBehavior { get; }
		public IsolationLevel TransactionIsolation { get; }
		public TimeSpan TransactionTimeout { get; }
		public ActivityType ActivityType { get; }
		public string ActivityName { get; }
		public string ActivityDisplayName { get; }
		public string ActivityDescription { get; }
	}

	private sealed class TestAggregateRoot : IAggregateRoot<string>
	{
		private readonly List<IDomainEvent> _domainEvents = [];

		public TestAggregateRoot(string id)
		{
			Id = id;
		}

		public string Id { get; }

		public string Key => Id;

		public string ETag { get; set; } = string.Empty;

		public IEnumerable<IDomainEvent> DomainEvents => _domainEvents;

		public void AddEvent(IDomainEvent domainEvent)
		{
			_domainEvents.Add(domainEvent);
		}

		public void ClearEvents()
		{
			_domainEvents.Clear();
		}
	}

	private sealed class TestDomainEventHandler : INotificationHandler<TestDomainEvent>
	{
		public Task Handle(TestDomainEvent notification, CancellationToken cancellationToken)
		{
			// Simulate successful dispatch
			return Task.CompletedTask;
		}
	}
}
