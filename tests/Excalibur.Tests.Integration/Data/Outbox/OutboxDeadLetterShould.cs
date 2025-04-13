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

using MediatR;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit.Abstractions;

using IsolationLevel = System.Transactions.IsolationLevel;

namespace Excalibur.Tests.Integration.Data.Outbox;

public class OutboxDeadLetterShould(SqlServerContainerFixture fixture, ITestOutputHelper output)
	: SqlServerPersistenceOnlyTestBase(fixture, output)
{
	[Fact]
	public async Task MoveToDeadLetterAfterMaxAttempts()
	{
		var domainDb = GetRequiredService<IDomainDb>();
		var outbox = GetRequiredService<IOutbox>();

		var message = new OutboxMessage
		{
			MessageId = Uuid7Extensions.GenerateString(),
			MessageBody = new TestDomainEvent { Id = 1, Name = "Test Event" },
			MessageHeaders = new Dictionary<string, string> { ["test-header"] = "test-value" }
		};

		_ = await outbox.SaveMessagesAsync([message]).ConfigureAwait(true);

		var dispatcherId = "test-dispatcher";
		var connection = domainDb.Connection;

		// Get the OutboxId that was just inserted
		var outboxId = await connection.QuerySingleAsync<Guid>(
			"SELECT TOP 1 OutboxId FROM Outbox.Outbox ORDER BY CreatedAt DESC").ConfigureAwait(true);

		var maxAttempts = 3;
		_ = await connection.ExecuteAsync(
			"UPDATE Outbox.Outbox SET Attempts = @Attempts WHERE OutboxId = @OutboxId",
			new { Attempts = maxAttempts - 1, OutboxId = outboxId }).ConfigureAwait(true);

		// Directly assign dispatcher to the known record
		_ = await connection.ExecuteAsync("""
		                                   UPDATE Outbox.Outbox
		                                   SET DispatcherId = @DispatcherId,
		                                       DispatcherTimeout = DATEADD(MILLISECOND, 60000, GETUTCDATE())
		                                   WHERE OutboxId = @OutboxId
		                                  """,
			new { DispatcherId = dispatcherId, OutboxId = outboxId }).ConfigureAwait(true);

		var record = await connection.QuerySingleAsync<OutboxRecord>(
			"SELECT * FROM Outbox.Outbox WHERE OutboxId = @OutboxId",
			new { OutboxId = outboxId }).ConfigureAwait(true);

		_ = await outbox.DispatchReservedRecordAsync(dispatcherId, record).ConfigureAwait(true);

		var stillInOutbox = await connection.QueryFirstOrDefaultAsync<int>(
			"SELECT COUNT(1) FROM Outbox.Outbox WHERE OutboxId = @OutboxId",
			new { OutboxId = outboxId }).ConfigureAwait(true);

		var inDeadLetter = await connection.QueryFirstOrDefaultAsync<int>(
			"SELECT COUNT(1) FROM Outbox.DeadLetterOutbox WHERE OutboxId = @OutboxId",
			new { OutboxId = outboxId }).ConfigureAwait(true);

		stillInOutbox.ShouldBe(0);
		inDeadLetter.ShouldBe(1);
	}

	protected override void AddServices(IServiceCollection services, IConfiguration configuration)
	{
		base.AddServices(services, configuration);

		// This handler always fails to simulate dead-letter triggering
		_ = services.AddScoped<INotificationHandler<TestDomainEvent>, FailingTestDomainEventHandler>();

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
			        OriginalAttempts int NOT NULL DEFAULT 0
			    );
			END
		").ConfigureAwait(true);
	}

	private sealed class FailingTestDomainEventHandler : INotificationHandler<TestDomainEvent>
	{
		public Task Handle(TestDomainEvent notification, CancellationToken cancellationToken)
		{
			throw new InvalidOperationException("Simulated failure");
		}
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
		private readonly List<IDomainEvent> _domainEvents = new();

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
}
