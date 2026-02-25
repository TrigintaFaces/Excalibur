// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Auditing;

/// <summary>
/// Unit tests for SecurityEvent, SecurityEventType, and SecuritySeverity.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SecurityEventShould
{
	#region SecurityEvent Property Tests

	[Fact]
	public void InitializeWithId_SetsIdProperty()
	{
		// Arrange
		var id = Guid.NewGuid();

		// Act
		var sut = new SecurityEvent { Id = id };

		// Assert
		sut.Id.ShouldBe(id);
	}

	[Fact]
	public void InitializeWithTimestamp_SetsTimestampProperty()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var sut = new SecurityEvent { Timestamp = timestamp };

		// Assert
		sut.Timestamp.ShouldBe(timestamp);
	}

	[Fact]
	public void InitializeWithEventType_SetsEventTypeProperty()
	{
		// Arrange
		var eventType = SecurityEventType.AuthenticationSuccess;

		// Act
		var sut = new SecurityEvent { EventType = eventType };

		// Assert
		sut.EventType.ShouldBe(eventType);
	}

	[Fact]
	public void InitializeWithSeverity_SetsSeverityProperty()
	{
		// Arrange
		var severity = SecuritySeverity.High;

		// Act
		var sut = new SecurityEvent { Severity = severity };

		// Assert
		sut.Severity.ShouldBe(severity);
	}

	[Fact]
	public void InitializeWithDescription_SetsDescriptionProperty()
	{
		// Arrange
		var description = "Authentication failed for user";

		// Act
		var sut = new SecurityEvent { Description = description };

		// Assert
		sut.Description.ShouldBe(description);
	}

	[Fact]
	public void InitializeWithCorrelationId_SetsCorrelationIdProperty()
	{
		// Arrange
		var correlationId = Guid.NewGuid();

		// Act
		var sut = new SecurityEvent { CorrelationId = correlationId };

		// Assert
		sut.CorrelationId.ShouldBe(correlationId);
	}

	[Fact]
	public void InitializeWithUserId_SetsUserIdProperty()
	{
		// Arrange
		var userId = "user-123";

		// Act
		var sut = new SecurityEvent { UserId = userId };

		// Assert
		sut.UserId.ShouldBe(userId);
	}

	[Fact]
	public void InitializeWithSourceIp_SetsSourceIpProperty()
	{
		// Arrange
		var sourceIp = "192.168.1.100";

		// Act
		var sut = new SecurityEvent { SourceIp = sourceIp };

		// Assert
		sut.SourceIp.ShouldBe(sourceIp);
	}

	[Fact]
	public void InitializeWithUserAgent_SetsUserAgentProperty()
	{
		// Arrange
		var userAgent = "Mozilla/5.0";

		// Act
		var sut = new SecurityEvent { UserAgent = userAgent };

		// Assert
		sut.UserAgent.ShouldBe(userAgent);
	}

	[Fact]
	public void InitializeWithMessageType_SetsMessageTypeProperty()
	{
		// Arrange
		var messageType = "LoginRequest";

		// Act
		var sut = new SecurityEvent { MessageType = messageType };

		// Assert
		sut.MessageType.ShouldBe(messageType);
	}

	[Fact]
	public void InitializeWithAdditionalData_SetsAdditionalDataProperty()
	{
		// Arrange
		var additionalData = new Dictionary<string, object?>(StringComparer.Ordinal)
		{
			["key1"] = "value1",
			["key2"] = 42,
		};

		// Act
		var sut = new SecurityEvent { AdditionalData = additionalData };

		// Assert
		sut.AdditionalData.Count.ShouldBe(2);
		sut.AdditionalData["key1"].ShouldBe("value1");
		sut.AdditionalData["key2"].ShouldBe(42);
	}

	[Fact]
	public void InitializeWithAllProperties_SetsAllValues()
	{
		// Arrange
		var id = Guid.NewGuid();
		var timestamp = DateTimeOffset.UtcNow;
		var correlationId = Guid.NewGuid();

		// Act
		var sut = new SecurityEvent
		{
			Id = id,
			Timestamp = timestamp,
			EventType = SecurityEventType.AuthorizationFailure,
			Severity = SecuritySeverity.Critical,
			Description = "Access denied",
			CorrelationId = correlationId,
			UserId = "admin",
			SourceIp = "10.0.0.1",
			UserAgent = "TestClient/1.0",
			MessageType = "SecureOperation",
			AdditionalData = new Dictionary<string, object?>(StringComparer.Ordinal) { ["action"] = "delete" },
		};

		// Assert
		sut.Id.ShouldBe(id);
		sut.Timestamp.ShouldBe(timestamp);
		sut.EventType.ShouldBe(SecurityEventType.AuthorizationFailure);
		sut.Severity.ShouldBe(SecuritySeverity.Critical);
		sut.Description.ShouldBe("Access denied");
		sut.CorrelationId.ShouldBe(correlationId);
		sut.UserId.ShouldBe("admin");
		sut.SourceIp.ShouldBe("10.0.0.1");
		sut.UserAgent.ShouldBe("TestClient/1.0");
		sut.MessageType.ShouldBe("SecureOperation");
		sut.AdditionalData["action"].ShouldBe("delete");
	}

	[Fact]
	public void DefaultDescription_IsEmptyString()
	{
		// Act
		var sut = new SecurityEvent();

		// Assert
		sut.Description.ShouldBe(string.Empty);
	}

	[Fact]
	public void DefaultAdditionalData_IsEmptyDictionary()
	{
		// Act
		var sut = new SecurityEvent();

		// Assert
		_ = sut.AdditionalData.ShouldNotBeNull();
		sut.AdditionalData.ShouldBeEmpty();
	}

	[Fact]
	public void NullableProperties_AllowNullValues()
	{
		// Act
		var sut = new SecurityEvent
		{
			CorrelationId = null,
			UserId = null,
			SourceIp = null,
			UserAgent = null,
			MessageType = null,
		};

		// Assert
		sut.CorrelationId.ShouldBeNull();
		sut.UserId.ShouldBeNull();
		sut.SourceIp.ShouldBeNull();
		sut.UserAgent.ShouldBeNull();
		sut.MessageType.ShouldBeNull();
	}

	#endregion SecurityEvent Property Tests

	#region SecurityEventType Enum Tests

	[Fact]
	public void SecurityEventType_AuthenticationSuccess_HasExpectedValue()
	{
		// Assert
		SecurityEventType.AuthenticationSuccess.ShouldBe((SecurityEventType)0);
	}

	[Fact]
	public void SecurityEventType_AuthenticationFailure_HasExpectedValue()
	{
		// Assert
		SecurityEventType.AuthenticationFailure.ShouldBe((SecurityEventType)1);
	}

	[Fact]
	public void SecurityEventType_AuthorizationSuccess_HasExpectedValue()
	{
		// Assert
		SecurityEventType.AuthorizationSuccess.ShouldBe((SecurityEventType)2);
	}

	[Fact]
	public void SecurityEventType_AuthorizationFailure_HasExpectedValue()
	{
		// Assert
		SecurityEventType.AuthorizationFailure.ShouldBe((SecurityEventType)3);
	}

	[Theory]
	[InlineData(SecurityEventType.ValidationFailure, 4)]
	[InlineData(SecurityEventType.ValidationError, 5)]
	[InlineData(SecurityEventType.InjectionAttempt, 6)]
	[InlineData(SecurityEventType.RateLimitExceeded, 7)]
	[InlineData(SecurityEventType.SuspiciousActivity, 8)]
	[InlineData(SecurityEventType.DataExfiltrationAttempt, 9)]
	[InlineData(SecurityEventType.ConfigurationChange, 10)]
	[InlineData(SecurityEventType.CredentialRotation, 11)]
	[InlineData(SecurityEventType.AuditLogAccess, 12)]
	[InlineData(SecurityEventType.SecurityPolicyViolation, 13)]
	[InlineData(SecurityEventType.EncryptionFailure, 14)]
	[InlineData(SecurityEventType.DecryptionFailure, 15)]
	public void SecurityEventType_AllValues_HaveExpectedNumericValues(SecurityEventType eventType, int expectedValue)
	{
		// Assert
		((int)eventType).ShouldBe(expectedValue);
	}

	[Fact]
	public void SecurityEventType_EnumCount_HasExpectedNumberOfValues()
	{
		// Arrange
		var values = Enum.GetValues<SecurityEventType>();

		// Assert
		values.Length.ShouldBe(16);
	}

	#endregion SecurityEventType Enum Tests

	#region SecuritySeverity Enum Tests

	[Fact]
	public void SecuritySeverity_Low_HasExpectedValue()
	{
		// Assert
		SecuritySeverity.Low.ShouldBe((SecuritySeverity)0);
	}

	[Fact]
	public void SecuritySeverity_Medium_HasExpectedValue()
	{
		// Assert
		SecuritySeverity.Medium.ShouldBe((SecuritySeverity)1);
	}

	[Fact]
	public void SecuritySeverity_High_HasExpectedValue()
	{
		// Assert
		SecuritySeverity.High.ShouldBe((SecuritySeverity)2);
	}

	[Fact]
	public void SecuritySeverity_Critical_HasExpectedValue()
	{
		// Assert
		SecuritySeverity.Critical.ShouldBe((SecuritySeverity)3);
	}

	[Fact]
	public void SecuritySeverity_EnumCount_HasExpectedNumberOfValues()
	{
		// Arrange
		var values = Enum.GetValues<SecuritySeverity>();

		// Assert
		values.Length.ShouldBe(4);
	}

	[Theory]
	[InlineData(SecuritySeverity.Low)]
	[InlineData(SecuritySeverity.Medium)]
	[InlineData(SecuritySeverity.High)]
	[InlineData(SecuritySeverity.Critical)]
	public void SecuritySeverity_AllValues_AreDefined(SecuritySeverity severity)
	{
		// Assert
		Enum.IsDefined(severity).ShouldBeTrue();
	}

	#endregion SecuritySeverity Enum Tests
}
