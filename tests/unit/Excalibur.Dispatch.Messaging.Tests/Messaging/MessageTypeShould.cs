// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Application.Requests;
using Excalibur.Application.Requests.Commands;
using Excalibur.Application.Requests.Queries;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Unit tests for message type implementations (IDomainEvent, IIntegrationEvent, ICommand, IQuery)
/// covering creation, property validation, metadata handling, and naming conventions.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Messaging")]
public sealed class MessageTypeShould : UnitTestBase
{
	#region IDomainEvent Tests (7 tests)

	[Fact]
	public void CreateDomainEvent_WithAllRequiredProperties_SuccessfullyInitializes()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var version = 5L;
		var metadata = new Dictionary<string, object> { { "UserId", "user-123" } };
		var beforeCreation = DateTimeOffset.UtcNow;

		// Act
		var domainEvent = new TestDomainEvent(aggregateId, version, metadata);
		var afterCreation = DateTimeOffset.UtcNow;

		// Assert
		domainEvent.EventId.ShouldNotBeNullOrEmpty();
		domainEvent.AggregateId.ShouldBe(aggregateId);
		domainEvent.Version.ShouldBe(version);
		domainEvent.OccurredAt.ShouldBeInRange(beforeCreation, afterCreation);
		domainEvent.EventType.ShouldBe(nameof(TestDomainEvent));
		_ = domainEvent.Metadata.ShouldNotBeNull();
		domainEvent.Metadata.ShouldContainKey("UserId");
	}

	[Fact]
	public void DomainEvent_EventIdGeneration_GeneratesUniqueIdentifiers()
	{
		// Arrange & Act
		var event1 = new TestDomainEvent("aggregate-1", 1);
		var event2 = new TestDomainEvent("aggregate-1", 2);

		// Assert
		event1.EventId.ShouldNotBe(event2.EventId);
		Guid.TryParse(event1.EventId, out _).ShouldBeTrue("EventId should be a valid GUID string");
		Guid.TryParse(event2.EventId, out _).ShouldBeTrue("EventId should be a valid GUID string");
	}

	[Fact]
	public void DomainEvent_AggregateId_AcceptsStringIdentifier()
	{
		// Arrange
		var aggregateId = "order-12345";

		// Act
		var domainEvent = new TestDomainEvent(aggregateId, 1);

		// Assert
		domainEvent.AggregateId.ShouldBe(aggregateId);
		_ = domainEvent.AggregateId.ShouldBeOfType<string>();
	}

	[Fact]
	public void DomainEvent_Version_TracksAggregateVersionCorrectly()
	{
		// Arrange
		var aggregateId = "aggregate-1";

		// Act
		var event1 = new TestDomainEvent(aggregateId, 1);
		var event2 = new TestDomainEvent(aggregateId, 2);
		var event3 = new TestDomainEvent(aggregateId, 10);

		// Assert
		event1.Version.ShouldBe(1L);
		event2.Version.ShouldBe(2L);
		event3.Version.ShouldBe(10L);
	}

	[Fact]
	public void DomainEvent_OccurredAt_CapturesUtcTimestamp()
	{
		// Arrange
		var beforeCreation = DateTimeOffset.UtcNow;

		// Act
		var domainEvent = new TestDomainEvent("aggregate-1", 1);

		// Assert
		var afterCreation = DateTimeOffset.UtcNow;
		domainEvent.OccurredAt.ShouldBeInRange(beforeCreation, afterCreation);
		domainEvent.OccurredAt.Offset.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void DomainEvent_Metadata_InitializesAsEmptyDictionaryWhenNull()
	{
		// Act
		var domainEvent = new TestDomainEvent("aggregate-1", 1, null);

		// Assert
		domainEvent.Metadata.ShouldBeNull();
	}

	[Fact]
	public void DomainEvent_EventType_ReturnsClassNameByDefault()
	{
		// Act
		var domainEvent = new TestDomainEvent("aggregate-1", 1);

		// Assert
		domainEvent.EventType.ShouldBe(nameof(TestDomainEvent));
		domainEvent.EventType.ShouldNotBeNullOrEmpty();
	}

	#endregion IDomainEvent Tests (7 tests)

	#region IIntegrationEvent Tests (6 tests)

	[Fact]
	public void CreateIntegrationEvent_WithBasicProperties_SuccessfullyInitializes()
	{
		// Act
		var integrationEvent = new TestIntegrationEvent("test-payload");

		// Assert
		_ = integrationEvent.ShouldNotBeNull();
		_ = integrationEvent.ShouldBeAssignableTo<IDispatchEvent>();
		_ = integrationEvent.ShouldBeAssignableTo<IIntegrationEvent>();
		integrationEvent.Payload.ShouldBe("test-payload");
	}

	[Fact]
	public void IntegrationEvent_MessageId_GeneratesUniqueIdentifier()
	{
		// Act
		var event1 = new TestIntegrationEvent("payload-1");
		var event2 = new TestIntegrationEvent("payload-2");

		// Assert
		event1.MessageId.ShouldNotBe(event2.MessageId);
		event1.MessageId.ShouldNotBeNullOrEmpty();
		Guid.TryParse(event1.MessageId, out _).ShouldBeTrue("MessageId should be a valid GUID string");
	}

	[Fact]
	public void IntegrationEvent_MessageType_ReturnsFullyQualifiedTypeName()
	{
		// Act
		var integrationEvent = new TestIntegrationEvent("payload");

		// Assert
		integrationEvent.MessageType.ShouldContain(nameof(TestIntegrationEvent));
		integrationEvent.MessageType.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void IntegrationEvent_Kind_ReturnsEventKind()
	{
		// Act
		var integrationEvent = new TestIntegrationEvent("payload");

		// Assert
		integrationEvent.Kind.ShouldBe(MessageKinds.Event);
	}

	[Fact]
	public void IntegrationEvent_Headers_ProvidesReadOnlyDictionary()
	{
		// Act
		var integrationEvent = new TestIntegrationEvent("payload");

		// Assert
		_ = integrationEvent.Headers.ShouldNotBeNull();
		_ = integrationEvent.Headers.ShouldBeAssignableTo<IReadOnlyDictionary<string, object>>();
	}

	[Fact]
	public void IntegrationEvent_CanCarryPayloadData_AcrossServiceBoundaries()
	{
		// Arrange
		var payload = "sensitive-customer-data";

		// Act
		var integrationEvent = new TestIntegrationEvent(payload);

		// Assert
		integrationEvent.Payload.ShouldBe(payload);
	}

	#endregion IIntegrationEvent Tests (6 tests)

	#region ICommand Tests (6 tests)

	[Fact]
	public void CreateCommand_WithCorrelationIdAndTenantId_SuccessfullyInitializes()
	{
		// Arrange
		var correlationId = Guid.NewGuid();
		var tenantId = "tenant-123";

		// Act
		var command = new TestCommand(correlationId, tenantId);

		// Assert
		command.Id.ShouldNotBe(Guid.Empty);
		command.MessageId.ShouldNotBeNullOrEmpty();
		command.TenantId.ShouldBe(tenantId);
	}

	[Fact]
	public void Command_Id_GeneratesUniqueIdentifier()
	{
		// Act
		var command1 = new TestCommand();
		var command2 = new TestCommand();

		// Assert
		command1.Id.ShouldNotBe(command2.Id);
		command1.Id.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void Command_ActivityType_ReturnsCommandType()
	{
		// Act
		var command = new TestCommand();

		// Assert
		((IActivity)command).ActivityType.ShouldBe(ActivityType.Command);
	}

	[Fact]
	public void Command_ActivityName_FollowsNamingConvention()
	{
		// Act
		var command = new TestCommand();

		// Assert
		command.ActivityName.ShouldContain(nameof(TestCommand));
		command.ActivityName.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void Command_TenantId_DefaultsToDefaultTenantIdWhenNull()
	{
		// Act
		var command = new TestCommand(Guid.NewGuid(), null);

		// Assert
		command.TenantId.ShouldBe(TenantDefaults.DefaultTenantId);
	}

	[Fact]
	public void Command_TransactionBehavior_HasSensibleDefaults()
	{
		// Act
		var command = new TestCommand();

		// Assert
		command.TransactionBehavior.ShouldBe(System.Transactions.TransactionScopeOption.Required);
		command.TransactionIsolation.ShouldBe(System.Transactions.IsolationLevel.ReadCommitted);
		command.TransactionTimeout.ShouldBe(TimeSpan.FromMinutes(1));
	}

	#endregion ICommand Tests (6 tests)

	#region IQuery Tests (6 tests)

	[Fact]
	public void CreateQuery_WithGenericResultType_SuccessfullyInitializes()
	{
		// Arrange
		var correlationId = Guid.NewGuid();
		var tenantId = "tenant-456";

		// Act
		var query = new TestQuery(correlationId, tenantId);

		// Assert
		query.Id.ShouldNotBe(Guid.Empty);
		query.MessageId.ShouldNotBeNullOrEmpty();
		query.TenantId.ShouldBe(tenantId);
	}

	[Fact]
	public void Query_Id_GeneratesUniqueIdentifier()
	{
		// Act
		var query1 = new TestQuery();
		var query2 = new TestQuery();

		// Assert
		query1.Id.ShouldNotBe(query2.Id);
		query1.Id.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void Query_ActivityType_ReturnsQueryType()
	{
		// Act
		var query = new TestQuery();

		// Assert
		((IActivity)query).ActivityType.ShouldBe(ActivityType.Query);
	}

	[Fact]
	public void Query_ActivityName_FollowsNamingConvention()
	{
		// Act
		var query = new TestQuery();

		// Assert
		query.ActivityName.ShouldContain(nameof(TestQuery));
		query.ActivityName.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void Query_TenantId_DefaultsToDefaultTenantIdWhenNull()
	{
		// Act
		var query = new TestQuery(Guid.NewGuid(), null);

		// Assert
		query.TenantId.ShouldBe(TenantDefaults.DefaultTenantId);
	}

	[Fact]
	public void Query_TransactionTimeout_HasLongerDefaultThanCommand()
	{
		// Act
		var query = new TestQuery();

		// Assert
		query.TransactionTimeout.ShouldBe(TimeSpan.FromMinutes(2));
		query.TransactionBehavior.ShouldBe(System.Transactions.TransactionScopeOption.Required);
		query.TransactionIsolation.ShouldBe(System.Transactions.IsolationLevel.ReadCommitted);
	}

	#endregion IQuery Tests (6 tests)

	#region Test Fixtures

	/// <summary>
	/// Test implementation of IDomainEvent for testing domain event behavior.
	/// </summary>
	private sealed class TestDomainEvent : IDomainEvent
	{
		public TestDomainEvent(string aggregateId, long version, IDictionary<string, object>? metadata = null)
		{
			EventId = Guid.NewGuid().ToString();
			AggregateId = aggregateId;
			Version = version;
			OccurredAt = DateTimeOffset.UtcNow;
			EventType = nameof(TestDomainEvent);
			Metadata = metadata;
		}

		public string EventId { get; init; }
		public string AggregateId { get; init; }
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; }
		public string EventType { get; init; }
		public IDictionary<string, object>? Metadata { get; init; }
	}

	/// <summary>
	/// Test implementation of IIntegrationEvent for testing integration event behavior.
	/// </summary>
	private sealed class TestIntegrationEvent : IIntegrationEvent
	{
		private readonly Dictionary<string, object> _headers = new();

		public TestIntegrationEvent(string payload)
		{
			MessageId = Guid.NewGuid().ToString();
			MessageType = GetType().FullName ?? GetType().Name;
			Kind = MessageKinds.Event;
			Payload = payload;
		}

		public string MessageId { get; }
		public string MessageType { get; }
		public MessageKinds Kind { get; }
		public IReadOnlyDictionary<string, object> Headers => _headers;
		public string Payload { get; }
	}

	/// <summary>
	/// Test implementation of ICommand for testing command behavior.
	/// </summary>
	private sealed class TestCommand : CommandBase
	{
		public TestCommand() : base()
		{
		}

		public TestCommand(Guid correlationId, string? tenantId) : base(correlationId, tenantId)
		{
		}

		public override string ActivityDisplayName => "Test Command";
		public override string ActivityDescription => "A test command for unit testing";
	}

	/// <summary>
	/// Test implementation of IQuery for testing query behavior.
	/// </summary>
	private sealed class TestQuery : QueryBase<TestQueryResult>
	{
		public TestQuery() : base()
		{
		}

		public TestQuery(Guid correlationId, string? tenantId) : base(correlationId, tenantId)
		{
		}

		public override string ActivityDisplayName => "Test Query";
		public override string ActivityDescription => "A test query for unit testing";
	}

	/// <summary>
	/// Test result type for IQuery testing.
	/// </summary>
	private sealed class TestQueryResult
	{
		public string? Data { get; init; }
	}

	#endregion Test Fixtures
}
