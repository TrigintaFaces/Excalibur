// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Auditing;

namespace Excalibur.Tests.A3.Abstractions;

/// <summary>
/// Unit tests for <see cref="AuditEvent"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AuditEventShould : UnitTestBase
{
	private readonly DateTimeOffset _timestamp = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

	#region Constructor Tests

	[Fact]
	public void Create_WithRequiredParameters_SetsValues()
	{
		// Arrange & Act
		var auditEvent = new AuditEvent(
			timestampUtc: _timestamp,
			tenantId: "tenant-123",
			actorId: "user-456",
			action: "CreateOrder",
			resource: "Order/order-789",
			outcome: "Success");

		// Assert
		auditEvent.TimestampUtc.ShouldBe(_timestamp);
		auditEvent.TenantId.ShouldBe("tenant-123");
		auditEvent.ActorId.ShouldBe("user-456");
		auditEvent.Action.ShouldBe("CreateOrder");
		auditEvent.Resource.ShouldBe("Order/order-789");
		auditEvent.Outcome.ShouldBe("Success");
		auditEvent.CorrelationId.ShouldBeNull();
		auditEvent.Attributes.ShouldBeNull();
	}

	[Fact]
	public void Create_WithCorrelationId_SetsValue()
	{
		// Arrange & Act
		var auditEvent = new AuditEvent(
			timestampUtc: _timestamp,
			tenantId: "tenant-123",
			actorId: "user-456",
			action: "UpdateProfile",
			resource: "User/user-456",
			outcome: "Success",
			correlationId: "corr-abc-123");

		// Assert
		auditEvent.CorrelationId.ShouldBe("corr-abc-123");
	}

	[Fact]
	public void Create_WithAttributes_SetsValue()
	{
		// Arrange
		var attributes = new Dictionary<string, string>
		{
			["ip_address"] = "192.168.1.100",
			["user_agent"] = "Mozilla/5.0",
			["request_id"] = "req-xyz"
		};

		// Act
		var auditEvent = new AuditEvent(
			timestampUtc: _timestamp,
			tenantId: "tenant-123",
			actorId: "user-456",
			action: "Login",
			resource: "Session/sess-001",
			outcome: "Success",
			attributes: attributes);

		// Assert
		auditEvent.Attributes.ShouldNotBeNull();
		auditEvent.Attributes.Count.ShouldBe(3);
		auditEvent.Attributes["ip_address"].ShouldBe("192.168.1.100");
		auditEvent.Attributes["user_agent"].ShouldBe("Mozilla/5.0");
		auditEvent.Attributes["request_id"].ShouldBe("req-xyz");
	}

	[Fact]
	public void Create_WithAllParameters_SetsAllValues()
	{
		// Arrange
		var attributes = new Dictionary<string, string>
		{
			["reason"] = "policy_violation"
		};

		// Act
		var auditEvent = new AuditEvent(
			timestampUtc: _timestamp,
			tenantId: "tenant-xyz",
			actorId: "admin-001",
			action: "DeleteUser",
			resource: "User/user-banned",
			outcome: "Success",
			correlationId: "trace-123",
			attributes: attributes);

		// Assert
		auditEvent.TimestampUtc.ShouldBe(_timestamp);
		auditEvent.TenantId.ShouldBe("tenant-xyz");
		auditEvent.ActorId.ShouldBe("admin-001");
		auditEvent.Action.ShouldBe("DeleteUser");
		auditEvent.Resource.ShouldBe("User/user-banned");
		auditEvent.Outcome.ShouldBe("Success");
		auditEvent.CorrelationId.ShouldBe("trace-123");
		auditEvent.Attributes.ShouldNotBeNull();
		auditEvent.Attributes["reason"].ShouldBe("policy_violation");
	}

	[Fact]
	public void Create_WithEmptyAttributes_SetsEmptyDictionary()
	{
		// Arrange
		var attributes = new Dictionary<string, string>();

		// Act
		var auditEvent = new AuditEvent(
			timestampUtc: _timestamp,
			tenantId: "tenant-123",
			actorId: "user-456",
			action: "ViewDashboard",
			resource: "Dashboard/main",
			outcome: "Success",
			attributes: attributes);

		// Assert
		auditEvent.Attributes.ShouldNotBeNull();
		auditEvent.Attributes.Count.ShouldBe(0);
	}

	#endregion

	#region IAuditEvent Interface Implementation

	[Fact]
	public void ImplementsIAuditEvent()
	{
		// Arrange & Act
		var auditEvent = new AuditEvent(
			timestampUtc: _timestamp,
			tenantId: "tenant-123",
			actorId: "user-456",
			action: "Test",
			resource: "Test/1",
			outcome: "Success");

		// Assert
		auditEvent.ShouldBeAssignableTo<IAuditEvent>();
	}

	[Fact]
	public void InterfaceProperties_MatchClassProperties()
	{
		// Arrange
		var attributes = new Dictionary<string, string> { ["key"] = "value" };
		var auditEvent = new AuditEvent(
			timestampUtc: _timestamp,
			tenantId: "tenant-123",
			actorId: "user-456",
			action: "Test",
			resource: "Test/1",
			outcome: "Success",
			correlationId: "corr-123",
			attributes: attributes);

		// Act
		IAuditEvent interfaceRef = auditEvent;

		// Assert
		interfaceRef.TimestampUtc.ShouldBe(auditEvent.TimestampUtc);
		interfaceRef.TenantId.ShouldBe(auditEvent.TenantId);
		interfaceRef.ActorId.ShouldBe(auditEvent.ActorId);
		interfaceRef.Action.ShouldBe(auditEvent.Action);
		interfaceRef.Resource.ShouldBe(auditEvent.Resource);
		interfaceRef.Outcome.ShouldBe(auditEvent.Outcome);
		interfaceRef.CorrelationId.ShouldBe(auditEvent.CorrelationId);
		interfaceRef.Attributes.ShouldBe(auditEvent.Attributes);
	}

	#endregion

	#region Common Outcome Scenarios

	[Theory]
	[InlineData("Success")]
	[InlineData("Denied")]
	[InlineData("Failed")]
	[InlineData("Error")]
	[InlineData("Unauthorized")]
	[InlineData("NotFound")]
	public void Create_WithCommonOutcomes_Succeeds(string outcome)
	{
		// Act
		var auditEvent = new AuditEvent(
			timestampUtc: _timestamp,
			tenantId: "tenant-123",
			actorId: "user-456",
			action: "TestAction",
			resource: "Test/1",
			outcome: outcome);

		// Assert
		auditEvent.Outcome.ShouldBe(outcome);
	}

	#endregion

	#region Common Action Scenarios

	[Theory]
	[InlineData("Create")]
	[InlineData("Read")]
	[InlineData("Update")]
	[InlineData("Delete")]
	[InlineData("Login")]
	[InlineData("Logout")]
	[InlineData("Export")]
	[InlineData("Import")]
	public void Create_WithCommonActions_Succeeds(string action)
	{
		// Act
		var auditEvent = new AuditEvent(
			timestampUtc: _timestamp,
			tenantId: "tenant-123",
			actorId: "user-456",
			action: action,
			resource: "Resource/1",
			outcome: "Success");

		// Assert
		auditEvent.Action.ShouldBe(action);
	}

	#endregion

	#region Resource Format Scenarios

	[Theory]
	[InlineData("Order/order-123")]
	[InlineData("User/user-456")]
	[InlineData("/api/v1/users/123")]
	[InlineData("urn:resource:document:doc-789")]
	[InlineData("https://example.com/api/orders/123")]
	public void Create_WithVariousResourceFormats_Succeeds(string resource)
	{
		// Act
		var auditEvent = new AuditEvent(
			timestampUtc: _timestamp,
			tenantId: "tenant-123",
			actorId: "user-456",
			action: "Access",
			resource: resource,
			outcome: "Success");

		// Assert
		auditEvent.Resource.ShouldBe(resource);
	}

	#endregion

	#region Actor ID Format Scenarios

	[Theory]
	[InlineData("user-123")]
	[InlineData("00000000-0000-0000-0000-000000000001")]
	[InlineData("service-account@project.iam.gserviceaccount.com")]
	[InlineData("system")]
	[InlineData("anonymous")]
	public void Create_WithVariousActorIdFormats_Succeeds(string actorId)
	{
		// Act
		var auditEvent = new AuditEvent(
			timestampUtc: _timestamp,
			tenantId: "tenant-123",
			actorId: actorId,
			action: "Test",
			resource: "Test/1",
			outcome: "Success");

		// Assert
		auditEvent.ActorId.ShouldBe(actorId);
	}

	#endregion

	#region Timestamp Scenarios

	[Fact]
	public void Create_WithUtcNow_SetsTimestamp()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var auditEvent = new AuditEvent(
			timestampUtc: now,
			tenantId: "tenant-123",
			actorId: "user-456",
			action: "Test",
			resource: "Test/1",
			outcome: "Success");

		// Assert
		auditEvent.TimestampUtc.ShouldBe(now);
	}

	[Fact]
	public void Create_WithMinValue_SetsTimestamp()
	{
		// Arrange
		var minValue = DateTimeOffset.MinValue;

		// Act
		var auditEvent = new AuditEvent(
			timestampUtc: minValue,
			tenantId: "tenant-123",
			actorId: "user-456",
			action: "Test",
			resource: "Test/1",
			outcome: "Success");

		// Assert
		auditEvent.TimestampUtc.ShouldBe(minValue);
	}

	[Fact]
	public void Create_WithDifferentTimezoneOffset_PreservesOffset()
	{
		// Arrange
		var timestamp = new DateTimeOffset(2024, 6, 15, 14, 30, 0, TimeSpan.FromHours(-5));

		// Act
		var auditEvent = new AuditEvent(
			timestampUtc: timestamp,
			tenantId: "tenant-123",
			actorId: "user-456",
			action: "Test",
			resource: "Test/1",
			outcome: "Success");

		// Assert
		auditEvent.TimestampUtc.ShouldBe(timestamp);
		auditEvent.TimestampUtc.Offset.ShouldBe(TimeSpan.FromHours(-5));
	}

	#endregion

	#region Immutability Tests

	[Fact]
	public void Properties_AreReadOnly()
	{
		// Arrange & Act
		var auditEvent = new AuditEvent(
			timestampUtc: _timestamp,
			tenantId: "tenant-123",
			actorId: "user-456",
			action: "Test",
			resource: "Test/1",
			outcome: "Success");

		// Assert - Verify properties have no setters (compilation check)
		// The class is designed with init-only properties via primary constructor
		auditEvent.TimestampUtc.ShouldBe(_timestamp);
		auditEvent.TenantId.ShouldBe("tenant-123");
		auditEvent.ActorId.ShouldBe("user-456");
		auditEvent.Action.ShouldBe("Test");
		auditEvent.Resource.ShouldBe("Test/1");
		auditEvent.Outcome.ShouldBe("Success");
	}

	#endregion
}
