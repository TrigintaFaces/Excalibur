// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging;

[Trait("Category", "Unit")]
[UnitTest]
public sealed class AuditHasherShould
{
	#region ComputeHash Tests

	[Fact]
	public void ComputeHash_ProducesConsistentHashForSameEvent()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();

		// Act
		var hash1 = AuditHasher.ComputeHash(auditEvent, null);
		var hash2 = AuditHasher.ComputeHash(auditEvent, null);

		// Assert
		hash1.ShouldNotBeNullOrWhiteSpace();
		hash2.ShouldBe(hash1);
	}

	[Fact]
	public void ComputeHash_ProducesDifferentHashesForDifferentEvents()
	{
		// Arrange
		var event1 = CreateTestAuditEvent("event-1");
		var event2 = CreateTestAuditEvent("event-2");

		// Act
		var hash1 = AuditHasher.ComputeHash(event1, null);
		var hash2 = AuditHasher.ComputeHash(event2, null);

		// Assert
		hash1.ShouldNotBe(hash2);
	}

	[Fact]
	public void ComputeHash_IncludesPreviousHashInChain()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		const string previousHash = "previousHashValue123";

		// Act
		var hashWithoutPrevious = AuditHasher.ComputeHash(auditEvent, null);
		var hashWithPrevious = AuditHasher.ComputeHash(auditEvent, previousHash);

		// Assert
		hashWithoutPrevious.ShouldNotBe(hashWithPrevious);
	}

	[Fact]
	public void ComputeHash_ProducesBase64EncodedSha256Hash()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();

		// Act
		var hash = AuditHasher.ComputeHash(auditEvent, null);

		// Assert - SHA-256 produces 32 bytes, Base64 encoding produces 44 chars (with padding)
		hash.ShouldNotBeNullOrWhiteSpace();
		var decoded = Convert.FromBase64String(hash);
		decoded.Length.ShouldBe(32); // SHA-256 produces 32 bytes
	}

	[Fact]
	public void ComputeHash_ThrowsOnNullEvent()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => AuditHasher.ComputeHash(null!, null));
	}

	[Fact]
	public void ComputeHash_IncludesMetadataInHash()
	{
		// Arrange
		var eventWithoutMetadata = CreateTestAuditEvent();
		var eventWithMetadata = eventWithoutMetadata with
		{
			Metadata = new Dictionary<string, string> { ["key"] = "value" }
		};

		// Act
		var hashWithout = AuditHasher.ComputeHash(eventWithoutMetadata, null);
		var hashWith = AuditHasher.ComputeHash(eventWithMetadata, null);

		// Assert
		hashWithout.ShouldNotBe(hashWith);
	}

	[Fact]
	public void ComputeHash_MetadataOrderDoesNotAffectHash()
	{
		// Arrange
		var metadata1 = new Dictionary<string, string>
		{
			["a"] = "1",
			["b"] = "2"
		};
		var metadata2 = new Dictionary<string, string>
		{
			["b"] = "2",
			["a"] = "1"
		};

		var event1 = CreateTestAuditEvent() with { Metadata = metadata1 };
		var event2 = CreateTestAuditEvent() with { Metadata = metadata2 };

		// Act
		var hash1 = AuditHasher.ComputeHash(event1, null);
		var hash2 = AuditHasher.ComputeHash(event2, null);

		// Assert - metadata is sorted before hashing
		hash1.ShouldBe(hash2);
	}

	[Fact]
	public void ComputeHash_HandlesEventWithNullOptionalFields()
	{
		// Arrange
		var minimalEvent = new AuditEvent
		{
			EventId = "minimal-event",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero),
			ActorId = "user-1",
			// All optional fields are null
			ActorType = null,
			ResourceId = null,
			ResourceType = null,
			ResourceClassification = null,
			TenantId = null,
			CorrelationId = null,
			SessionId = null,
			IpAddress = null,
			UserAgent = null,
			Reason = null,
			Metadata = null
		};

		// Act
		var hash = AuditHasher.ComputeHash(minimalEvent, null);

		// Assert
		hash.ShouldNotBeNullOrWhiteSpace();
		var decoded = Convert.FromBase64String(hash);
		decoded.Length.ShouldBe(32);
	}

	[Fact]
	public void ComputeHash_DiffersByAction()
	{
		// Arrange
		var event1 = CreateTestAuditEvent() with { Action = "Read" };
		var event2 = CreateTestAuditEvent() with { Action = "Write" };

		// Act
		var hash1 = AuditHasher.ComputeHash(event1, null);
		var hash2 = AuditHasher.ComputeHash(event2, null);

		// Assert
		hash1.ShouldNotBe(hash2);
	}

	[Fact]
	public void ComputeHash_DiffersByOutcome()
	{
		// Arrange
		var event1 = CreateTestAuditEvent() with { Outcome = AuditOutcome.Success };
		var event2 = CreateTestAuditEvent() with { Outcome = AuditOutcome.Failure };

		// Act
		var hash1 = AuditHasher.ComputeHash(event1, null);
		var hash2 = AuditHasher.ComputeHash(event2, null);

		// Assert
		hash1.ShouldNotBe(hash2);
	}

	[Fact]
	public void ComputeHash_DiffersByTimestamp()
	{
		// Arrange
		var event1 = CreateTestAuditEvent() with
		{
			Timestamp = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero)
		};
		var event2 = CreateTestAuditEvent() with
		{
			Timestamp = new DateTimeOffset(2025, 1, 15, 11, 0, 0, TimeSpan.Zero)
		};

		// Act
		var hash1 = AuditHasher.ComputeHash(event1, null);
		var hash2 = AuditHasher.ComputeHash(event2, null);

		// Assert
		hash1.ShouldNotBe(hash2);
	}

	[Fact]
	public void ComputeHash_DiffersByEventType()
	{
		// Arrange
		var event1 = CreateTestAuditEvent() with { EventType = AuditEventType.DataAccess };
		var event2 = CreateTestAuditEvent() with { EventType = AuditEventType.Authentication };

		// Act
		var hash1 = AuditHasher.ComputeHash(event1, null);
		var hash2 = AuditHasher.ComputeHash(event2, null);

		// Assert
		hash1.ShouldNotBe(hash2);
	}

	[Fact]
	public void ComputeHash_HandlesEmptyMetadata()
	{
		// Arrange
		var eventEmptyMeta = CreateTestAuditEvent() with
		{
			Metadata = new Dictionary<string, string>()
		};
		var eventNullMeta = CreateTestAuditEvent() with { Metadata = null };

		// Act
		var hashEmpty = AuditHasher.ComputeHash(eventEmptyMeta, null);
		var hashNull = AuditHasher.ComputeHash(eventNullMeta, null);

		// Assert - both should produce valid hashes
		hashEmpty.ShouldNotBeNullOrWhiteSpace();
		hashNull.ShouldNotBeNullOrWhiteSpace();
		// Empty metadata and null metadata should produce the same hash
		// since the code checks Count > 0
		hashEmpty.ShouldBe(hashNull);
	}

	[Fact]
	public void ComputeHash_MetadataWithNullValueTreatedAsEmpty()
	{
		// Arrange
		var eventWithNull = CreateTestAuditEvent() with
		{
			Metadata = new Dictionary<string, string> { ["key"] = null! }
		};

		// Act - should not throw
		var hash = AuditHasher.ComputeHash(eventWithNull, null);

		// Assert
		hash.ShouldNotBeNullOrWhiteSpace();
	}

	#endregion ComputeHash Tests

	#region VerifyHash Tests

	[Fact]
	public void VerifyHash_ReturnsTrueForValidHash()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		var hash = AuditHasher.ComputeHash(auditEvent, null);
		var eventWithHash = auditEvent with { EventHash = hash };

		// Act
		var isValid = AuditHasher.VerifyHash(eventWithHash, null);

		// Assert
		isValid.ShouldBeTrue();
	}

	[Fact]
	public void VerifyHash_ReturnsTrueForValidHashWithPreviousHash()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		const string previousHash = "somePreviousHash";
		var hash = AuditHasher.ComputeHash(auditEvent, previousHash);
		var eventWithHash = auditEvent with { EventHash = hash };

		// Act
		var isValid = AuditHasher.VerifyHash(eventWithHash, previousHash);

		// Assert
		isValid.ShouldBeTrue();
	}

	[Fact]
	public void VerifyHash_ReturnsFalseForTamperedEvent()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		var hash = AuditHasher.ComputeHash(auditEvent, null);
		var eventWithHash = auditEvent with
		{
			EventHash = hash,
			Action = "TamperedAction"
		};

		// Act
		var isValid = AuditHasher.VerifyHash(eventWithHash, null);

		// Assert
		isValid.ShouldBeFalse();
	}

	[Fact]
	public void VerifyHash_ReturnsFalseForWrongPreviousHash()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		var hash = AuditHasher.ComputeHash(auditEvent, "correctPreviousHash");
		var eventWithHash = auditEvent with { EventHash = hash };

		// Act - verify with wrong previous hash
		var isValid = AuditHasher.VerifyHash(eventWithHash, "wrongPreviousHash");

		// Assert
		isValid.ShouldBeFalse();
	}

	[Fact]
	public void VerifyHash_ReturnsFalseForNullEvent()
	{
		// Act
		var isValid = AuditHasher.VerifyHash(null!, null);

		// Assert
		isValid.ShouldBeFalse();
	}

	[Fact]
	public void VerifyHash_ReturnsFalseForEventWithoutHash()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();

		// Act
		var isValid = AuditHasher.VerifyHash(auditEvent, null);

		// Assert
		isValid.ShouldBeFalse();
	}

	[Fact]
	public void VerifyHash_ReturnsFalseForEventWithNullHash()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent() with { EventHash = null };

		// Act
		var isValid = AuditHasher.VerifyHash(auditEvent, null);

		// Assert
		isValid.ShouldBeFalse();
	}

	[Fact]
	public void VerifyHash_ReturnsFalseForCorruptedHash()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent() with { EventHash = "not-a-valid-hash" };

		// Act
		var isValid = AuditHasher.VerifyHash(auditEvent, null);

		// Assert
		isValid.ShouldBeFalse();
	}

	#endregion VerifyHash Tests

	#region ComputeGenesisHash Tests

	[Fact]
	public void ComputeGenesisHash_ProducesConsistentHash()
	{
		// Arrange
		var initTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

		// Act
		var hash1 = AuditHasher.ComputeGenesisHash("tenant1", initTime);
		var hash2 = AuditHasher.ComputeGenesisHash("tenant1", initTime);

		// Assert
		hash1.ShouldNotBeNullOrWhiteSpace();
		hash2.ShouldBe(hash1);
	}

	[Fact]
	public void ComputeGenesisHash_DiffersByTenant()
	{
		// Arrange
		var initTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

		// Act
		var hash1 = AuditHasher.ComputeGenesisHash("tenant1", initTime);
		var hash2 = AuditHasher.ComputeGenesisHash("tenant2", initTime);

		// Assert
		hash1.ShouldNotBe(hash2);
	}

	[Fact]
	public void ComputeGenesisHash_DiffersByTime()
	{
		// Arrange
		var time1 = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var time2 = new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero);

		// Act
		var hash1 = AuditHasher.ComputeGenesisHash("tenant1", time1);
		var hash2 = AuditHasher.ComputeGenesisHash("tenant1", time2);

		// Assert
		hash1.ShouldNotBe(hash2);
	}

	[Fact]
	public void ComputeGenesisHash_HandlesNullTenantId()
	{
		// Arrange
		var initTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

		// Act
		var hash = AuditHasher.ComputeGenesisHash(null, initTime);

		// Assert
		hash.ShouldNotBeNullOrWhiteSpace();
		var decoded = Convert.FromBase64String(hash);
		decoded.Length.ShouldBe(32);
	}

	[Fact]
	public void ComputeGenesisHash_NullTenantDiffersFromNamedTenant()
	{
		// Arrange
		var initTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

		// Act
		var hashNull = AuditHasher.ComputeGenesisHash(null, initTime);
		var hashNamed = AuditHasher.ComputeGenesisHash("tenant1", initTime);

		// Assert
		hashNull.ShouldNotBe(hashNamed);
	}

	[Fact]
	public void ComputeGenesisHash_ProducesBase64EncodedSha256()
	{
		// Arrange
		var initTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

		// Act
		var hash = AuditHasher.ComputeGenesisHash("tenant1", initTime);

		// Assert
		var decoded = Convert.FromBase64String(hash);
		decoded.Length.ShouldBe(32);
	}

	#endregion ComputeGenesisHash Tests

	private static AuditEvent CreateTestAuditEvent(string? eventId = null) =>
		new()
		{
			EventId = eventId ?? "test-event-id",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero),
			ActorId = "user-123",
			ActorType = "User",
			ResourceId = "resource-456",
			ResourceType = "Document",
			ResourceClassification = DataClassification.Confidential,
			TenantId = "tenant-789",
			CorrelationId = "correlation-abc",
			SessionId = "session-xyz",
			IpAddress = "192.168.1.1",
			UserAgent = "TestClient/1.0"
		};
}
