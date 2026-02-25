// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Data.InMemory.Inbox;
using Excalibur.Data.InMemory.Outbox;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

using InMemoryInboxStoreOptions = Excalibur.Data.InMemory.Inbox.InMemoryInboxOptions;
using InMemoryOutboxStoreOptions = Excalibur.Data.InMemory.Outbox.InMemoryOutboxOptions;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Unit tests for the helper methods in conformance test kits.
/// These tests verify that the protected helper methods work correctly.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class ConformanceTestKitHelperMethodsShould
{
	#region InboxStoreConformanceTestKit Helper Tests

	[Fact]
	public void InboxStore_GenerateMessageId_ShouldReturnValidGuid()
	{
		// Arrange
		var testKit = new TestableInboxStoreKit();

		// Act
		var messageId = testKit.PublicGenerateMessageId();

		// Assert
		messageId.ShouldNotBeNullOrEmpty();
		Guid.TryParse(messageId, out _).ShouldBeTrue();
	}

	[Fact]
	public void InboxStore_GenerateMessageId_ShouldReturnUniqueValues()
	{
		// Arrange
		var testKit = new TestableInboxStoreKit();

		// Act
		var ids = Enumerable.Range(0, 100)
			.Select(_ => testKit.PublicGenerateMessageId())
			.ToList();

		// Assert
		ids.Distinct().Count().ShouldBe(100);
	}

	[Fact]
	public void InboxStore_GenerateHandlerType_ShouldReturnUniqueNames()
	{
		// Arrange
		var testKit = new TestableInboxStoreKit();

		// Act
		var types = Enumerable.Range(0, 10)
			.Select(_ => testKit.PublicGenerateHandlerType())
			.ToList();

		// Assert
		types.Distinct().Count().ShouldBe(10);
		types.All(t => t.StartsWith("TestHandler_", StringComparison.Ordinal)).ShouldBeTrue();
	}

	[Fact]
	public void InboxStore_CreatePayload_ShouldEncodeContent()
	{
		// Arrange
		var testKit = new TestableInboxStoreKit();
		var content = "Test message content with unicode: \u4e2d\u6587";

		// Act
		var payload = testKit.PublicCreatePayload(content);
		var decoded = Encoding.UTF8.GetString(payload);

		// Assert
		decoded.ShouldBe(content);
	}

	[Fact]
	public void InboxStore_CreateDefaultMetadata_ShouldContainExpectedKeys()
	{
		// Arrange
		var testKit = new TestableInboxStoreKit();

		// Act
		var metadata = testKit.PublicCreateDefaultMetadata();

		// Assert
		metadata.ShouldNotBeNull();
		metadata.ShouldContainKey("TestKey");
		metadata.ShouldContainKey("Timestamp");
		metadata["TestKey"].ToString().ShouldBe("TestValue");
	}

	#endregion

	#region OutboxStoreConformanceTestKit Helper Tests

	[Fact]
	public void OutboxStore_GenerateMessageId_ShouldReturnValidGuid()
	{
		// Arrange
		var testKit = new TestableOutboxStoreKit();

		// Act
		var messageId = testKit.PublicGenerateMessageId();

		// Assert
		messageId.ShouldNotBeNullOrEmpty();
		Guid.TryParse(messageId, out _).ShouldBeTrue();
	}

	[Fact]
	public void OutboxStore_CreatePayload_ShouldEncodeContent()
	{
		// Arrange
		var testKit = new TestableOutboxStoreKit();
		var content = "Outbox message payload";

		// Act
		var payload = testKit.PublicCreatePayload(content);
		var decoded = Encoding.UTF8.GetString(payload);

		// Assert
		decoded.ShouldBe(content);
	}

	[Fact]
	public void OutboxStore_CreateTestMessage_ShouldHaveCorrectDefaults()
	{
		// Arrange
		var testKit = new TestableOutboxStoreKit();

		// Act
		var message = testKit.PublicCreateTestMessage();

		// Assert
		message.ShouldNotBeNull();
		message.MessageType.ShouldBe("TestMessageType");
		message.Destination.ShouldBe("test-destination");
		Guid.TryParse(message.Id, out _).ShouldBeTrue();
	}

	[Fact]
	public void OutboxStore_CreateTestMessage_WithId_ShouldUseProvidedId()
	{
		// Arrange
		var testKit = new TestableOutboxStoreKit();
		var customId = "custom-message-id-12345";

		// Act
		var message = testKit.PublicCreateTestMessage(customId);

		// Assert
		message.Id.ShouldBe(customId);
	}

	[Fact]
	public void OutboxStore_CreateScheduledMessage_ShouldSetScheduledAt()
	{
		// Arrange
		var testKit = new TestableOutboxStoreKit();
		var scheduledTime = DateTimeOffset.UtcNow.AddHours(2);

		// Act
		var message = testKit.PublicCreateScheduledMessage(scheduledTime);

		// Assert
		message.ScheduledAt.ShouldBe(scheduledTime);
		message.MessageType.ShouldBe("ScheduledTestMessage");
	}

	#endregion

	#region EventStoreConformanceTestKit Helper Tests

	[Fact]
	public void EventStore_CreateTestEvents_ShouldCreateCorrectNumberOfEvents()
	{
		// Arrange
		var testKit = new TestableEventStoreKit();
		var aggregateId = "test-aggregate-123";

		// Act
		var events = testKit.PublicCreateTestEvents(aggregateId, 5);

		// Assert
		events.Count.ShouldBe(5);
	}

	[Fact]
	public void EventStore_CreateTestEvents_ShouldSetCorrectVersions()
	{
		// Arrange
		var testKit = new TestableEventStoreKit();
		var aggregateId = "test-aggregate";

		// Act
		var events = testKit.PublicCreateTestEvents(aggregateId, 3, startVersion: 10);

		// Assert
		events[0].Version.ShouldBe(10);
		events[1].Version.ShouldBe(11);
		events[2].Version.ShouldBe(12);
	}

	[Fact]
	public void EventStore_CreateTestEvents_ShouldSetCorrectAggregateId()
	{
		// Arrange
		var testKit = new TestableEventStoreKit();
		var aggregateId = "my-aggregate-999";

		// Act
		var events = testKit.PublicCreateTestEvents(aggregateId, 2);

		// Assert
		events.All(e => e.AggregateId == aggregateId).ShouldBeTrue();
	}

	[Fact]
	public void EventStore_GenerateAggregateId_ShouldReturnValidGuid()
	{
		// Arrange
		var testKit = new TestableEventStoreKit();

		// Act
		var aggregateId = testKit.PublicGenerateAggregateId();

		// Assert
		aggregateId.ShouldNotBeNullOrEmpty();
		Guid.TryParse(aggregateId, out _).ShouldBeTrue();
	}

	[Fact]
	public void EventStore_GenerateAggregateId_ShouldReturnUniqueValues()
	{
		// Arrange
		var testKit = new TestableEventStoreKit();

		// Act
		var ids = Enumerable.Range(0, 50)
			.Select(_ => testKit.PublicGenerateAggregateId())
			.ToList();

		// Assert
		ids.Distinct().Count().ShouldBe(50);
	}

	#endregion

	#region TestDomainEvent Tests

	[Fact]
	public void TestDomainEvent_Create_ShouldSetPropertiesCorrectly()
	{
		// Arrange
		var aggregateId = "aggregate-abc";
		var version = 42L;

		// Act
		var evt = TestDomainEvent.Create(aggregateId, version);

		// Assert
		evt.AggregateId.ShouldBe(aggregateId);
		evt.Version.ShouldBe(version);
		evt.Payload.ShouldBe($"payload-v{version}");
		Guid.TryParse(evt.EventId, out _).ShouldBeTrue();
	}

	#endregion

	#region TestSagaState Tests

	[Fact]
	public void TestSagaState_Create_ShouldSetSagaId()
	{
		// Arrange
		var sagaId = Guid.NewGuid();

		// Act
		var state = TestSagaState.Create(sagaId);

		// Assert
		state.SagaId.ShouldBe(sagaId);
		state.Status.ShouldBe("Created");
		state.Counter.ShouldBe(0);
	}

	#endregion

	#region TestSnapshot Tests

	[Fact]
	public void TestSnapshot_Create_ShouldSetPropertiesCorrectly()
	{
		// Arrange
		var aggregateId = "snapshot-agg";
		var aggregateType = "MyAggregate";
		var version = 10L;

		// Act
		var snapshot = TestSnapshot.Create(aggregateId, aggregateType, version);

		// Assert
		snapshot.AggregateId.ShouldBe(aggregateId);
		snapshot.AggregateType.ShouldBe(aggregateType);
		snapshot.Version.ShouldBe(version);
		Encoding.UTF8.GetString(snapshot.Data).ShouldBe($"state-v{version}");
	}

	[Fact]
	public void TestSnapshot_Create_WithCustomState_ShouldUseProvidedState()
	{
		// Arrange
		var customState = "{\"key\": \"value\"}";

		// Act
		var snapshot = TestSnapshot.Create("agg", "Type", 1, customState);

		// Assert
		Encoding.UTF8.GetString(snapshot.Data).ShouldBe(customState);
	}

	#endregion

	#region Helper Test Classes

	/// <summary>
	/// Testable wrapper for InboxStoreConformanceTestKit to expose protected methods.
	/// </summary>
	private sealed class TestableInboxStoreKit : InboxStoreConformanceTestKit
	{
		protected override IInboxStore CreateStore()
		{
			var options = Options.Create(new InMemoryInboxStoreOptions());
			var logger = NullLogger<InMemoryInboxStore>.Instance;
			return new InMemoryInboxStore(options, logger);
		}

		public string PublicGenerateMessageId() => GenerateMessageId();
		public string PublicGenerateHandlerType() => GenerateHandlerType();
		public byte[] PublicCreatePayload(string content) => CreatePayload(content);
		public IDictionary<string, object> PublicCreateDefaultMetadata() => CreateDefaultMetadata();
	}

	/// <summary>
	/// Testable wrapper for OutboxStoreConformanceTestKit to expose protected methods.
	/// </summary>
	private sealed class TestableOutboxStoreKit : OutboxStoreConformanceTestKit
	{
		protected override IOutboxStore CreateStore()
		{
			var options = Options.Create(new InMemoryOutboxStoreOptions());
			var logger = NullLogger<InMemoryOutboxStore>.Instance;
			return new InMemoryOutboxStore(options, logger);
		}

		public string PublicGenerateMessageId() => GenerateMessageId();
		public byte[] PublicCreatePayload(string content) => CreatePayload(content);
		public OutboundMessage PublicCreateTestMessage() => CreateTestMessage();
		public OutboundMessage PublicCreateTestMessage(string messageId) => CreateTestMessage(messageId);
		public OutboundMessage PublicCreateScheduledMessage(DateTimeOffset scheduledAt) => CreateScheduledMessage(scheduledAt);
	}

	/// <summary>
	/// Testable wrapper for EventStoreConformanceTestKit to expose protected methods.
	/// </summary>
	private sealed class TestableEventStoreKit : EventStoreConformanceTestKit
	{
		protected override IEventStore CreateStore() =>
			throw new NotImplementedException("Not needed for helper method tests");

		public IReadOnlyList<IDomainEvent> PublicCreateTestEvents(string aggregateId, int count, long startVersion = 1) =>
			CreateTestEvents(aggregateId, count, startVersion);

		public string PublicGenerateAggregateId() => GenerateAggregateId();
	}

	#endregion
}
