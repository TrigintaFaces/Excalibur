// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Serialization;

/// <summary>
/// Verifies SecurityEvent and SecurityEventQuery round-trip through the
/// source-generated SecurityEventSerializerContext. Sprint 754 task i7nsac.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class SecurityEventSerializationShould
{
	[Fact]
	public void RoundTripSecurityEvent()
	{
		// Arrange
		var original = new SecurityEvent
		{
			Id = Guid.NewGuid(),
			Timestamp = DateTimeOffset.UtcNow,
			EventType = SecurityEventType.AuthenticationFailure,
			Description = "Failed login attempt"
		};

		// Act
		var json = JsonSerializer.Serialize(original, SecurityEventSerializerContext.Default.SecurityEvent);
		var deserialized = JsonSerializer.Deserialize(json, SecurityEventSerializerContext.Default.SecurityEvent);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.Id.ShouldBe(original.Id);
		deserialized.Timestamp.ShouldBe(original.Timestamp);
		deserialized.EventType.ShouldBe(SecurityEventType.AuthenticationFailure);
		deserialized.Description.ShouldBe("Failed login attempt");
	}

	[Fact]
	public void RoundTripSecurityEventQuery()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var original = new SecurityEventQuery
		{
			EventType = SecurityEventType.AuthorizationFailure,
			StartTime = now.AddHours(-1),
			EndTime = now,
			MaxResults = 50
		};

		// Act
		var json = JsonSerializer.Serialize(original, SecurityEventSerializerContext.Default.SecurityEventQuery);
		var deserialized = JsonSerializer.Deserialize(json, SecurityEventSerializerContext.Default.SecurityEventQuery);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.EventType.ShouldBe(SecurityEventType.AuthorizationFailure);
		deserialized.StartTime.ShouldNotBeNull();
		deserialized.EndTime.ShouldNotBeNull();
		deserialized.MaxResults.ShouldBe(50);
	}

	[Fact]
	public void SerializeWithCamelCasePropertyNames()
	{
		// Arrange -- SecurityEventSerializerContext uses CamelCase
		var evt = new SecurityEvent
		{
			Id = Guid.NewGuid(),
			Timestamp = DateTimeOffset.UtcNow,
			EventType = SecurityEventType.AuthenticationFailure,
			Description = "test"
		};

		// Act
		var json = JsonSerializer.Serialize(evt, SecurityEventSerializerContext.Default.SecurityEvent);

		// Assert
		json.ShouldContain("\"id\"");
		json.ShouldContain("\"timestamp\"");
		json.ShouldContain("\"eventType\"");
		json.ShouldContain("\"description\"");
	}
}
