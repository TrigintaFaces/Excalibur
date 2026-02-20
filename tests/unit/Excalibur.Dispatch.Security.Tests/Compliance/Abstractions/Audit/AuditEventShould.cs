// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Audit;

/// <summary>
/// Unit tests for <see cref="AuditEvent"/> record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "Audit")]
public sealed class AuditEventShould : UnitTestBase
{
	[Fact]
	public void CreateValidAuditEventWithRequiredProperties()
	{
		// Arrange
		var eventId = Guid.NewGuid().ToString();
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var auditEvent = new AuditEvent
		{
			EventId = eventId,
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = timestamp,
			ActorId = "user-123"
		};

		// Assert
		auditEvent.EventId.ShouldBe(eventId);
		auditEvent.EventType.ShouldBe(AuditEventType.DataAccess);
		auditEvent.Action.ShouldBe("Read");
		auditEvent.Outcome.ShouldBe(AuditOutcome.Success);
		auditEvent.Timestamp.ShouldBe(timestamp);
		auditEvent.ActorId.ShouldBe("user-123");
	}

	[Fact]
	public void CreateFullyPopulatedAuditEvent()
	{
		// Arrange
		var metadata = new Dictionary<string, string>
		{
			["key1"] = "value1",
			["key2"] = "value2"
		};

		// Act
		var auditEvent = new AuditEvent
		{
			EventId = "event-001",
			EventType = AuditEventType.Security,
			Action = "KeyRotation",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "system",
			ActorType = "Service",
			ResourceId = "key-456",
			ResourceType = "EncryptionKey",
			ResourceClassification = DataClassification.Restricted,
			TenantId = "tenant-789",
			CorrelationId = "corr-abc",
			SessionId = "session-def",
			IpAddress = "192.168.1.1",
			UserAgent = "Dispatch/1.0",
			Reason = "Scheduled rotation",
			Metadata = metadata,
			PreviousEventHash = "prev-hash-123",
			EventHash = "current-hash-456"
		};

		// Assert
		auditEvent.EventId.ShouldBe("event-001");
		auditEvent.EventType.ShouldBe(AuditEventType.Security);
		auditEvent.Action.ShouldBe("KeyRotation");
		auditEvent.Outcome.ShouldBe(AuditOutcome.Success);
		auditEvent.ActorType.ShouldBe("Service");
		auditEvent.ResourceId.ShouldBe("key-456");
		auditEvent.ResourceType.ShouldBe("EncryptionKey");
		auditEvent.ResourceClassification.ShouldBe(DataClassification.Restricted);
		auditEvent.TenantId.ShouldBe("tenant-789");
		auditEvent.CorrelationId.ShouldBe("corr-abc");
		auditEvent.SessionId.ShouldBe("session-def");
		auditEvent.IpAddress.ShouldBe("192.168.1.1");
		auditEvent.UserAgent.ShouldBe("Dispatch/1.0");
		auditEvent.Reason.ShouldBe("Scheduled rotation");
		auditEvent.Metadata.ShouldBe(metadata);
		auditEvent.PreviousEventHash.ShouldBe("prev-hash-123");
		auditEvent.EventHash.ShouldBe("current-hash-456");
	}

	[Theory]
	[InlineData(AuditEventType.System)]
	[InlineData(AuditEventType.Authentication)]
	[InlineData(AuditEventType.Authorization)]
	[InlineData(AuditEventType.DataAccess)]
	[InlineData(AuditEventType.DataModification)]
	[InlineData(AuditEventType.ConfigurationChange)]
	[InlineData(AuditEventType.Security)]
	[InlineData(AuditEventType.Compliance)]
	[InlineData(AuditEventType.Administrative)]
	[InlineData(AuditEventType.Integration)]
	public void SupportAllAuditEventTypes(AuditEventType eventType)
	{
		// Act
		var auditEvent = new AuditEvent
		{
			EventId = "test",
			EventType = eventType,
			Action = "Test",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user"
		};

		// Assert
		auditEvent.EventType.ShouldBe(eventType);
	}

	[Theory]
	[InlineData(AuditOutcome.Success)]
	[InlineData(AuditOutcome.Failure)]
	[InlineData(AuditOutcome.Denied)]
	[InlineData(AuditOutcome.Error)]
	[InlineData(AuditOutcome.Pending)]
	public void SupportAllAuditOutcomes(AuditOutcome outcome)
	{
		// Act
		var auditEvent = new AuditEvent
		{
			EventId = "test",
			EventType = AuditEventType.System,
			Action = "Test",
			Outcome = outcome,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user"
		};

		// Assert
		auditEvent.Outcome.ShouldBe(outcome);
	}

	[Fact]
	public void HaveNullOptionalPropertiesByDefault()
	{
		// Act
		var auditEvent = new AuditEvent
		{
			EventId = "test",
			EventType = AuditEventType.System,
			Action = "Test",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user"
		};

		// Assert
		auditEvent.ActorType.ShouldBeNull();
		auditEvent.ResourceId.ShouldBeNull();
		auditEvent.ResourceType.ShouldBeNull();
		auditEvent.ResourceClassification.ShouldBeNull();
		auditEvent.TenantId.ShouldBeNull();
		auditEvent.CorrelationId.ShouldBeNull();
		auditEvent.SessionId.ShouldBeNull();
		auditEvent.IpAddress.ShouldBeNull();
		auditEvent.UserAgent.ShouldBeNull();
		auditEvent.Reason.ShouldBeNull();
		auditEvent.Metadata.ShouldBeNull();
		auditEvent.PreviousEventHash.ShouldBeNull();
		auditEvent.EventHash.ShouldBeNull();
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		var event1 = new AuditEvent
		{
			EventId = "event-001",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = timestamp,
			ActorId = "user-123"
		};

		var event2 = new AuditEvent
		{
			EventId = "event-001",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = timestamp,
			ActorId = "user-123"
		};

		// Assert
		event1.ShouldBe(event2);
	}

	[Theory]
	[InlineData(DataClassification.Public)]
	[InlineData(DataClassification.Internal)]
	[InlineData(DataClassification.Confidential)]
	[InlineData(DataClassification.Restricted)]
	public void SupportAllDataClassifications(DataClassification classification)
	{
		// Act
		var auditEvent = new AuditEvent
		{
			EventId = "test",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user",
			ResourceClassification = classification
		};

		// Assert
		auditEvent.ResourceClassification.ShouldBe(classification);
	}
}
