// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Auditing;

/// <summary>
/// Unit tests for <see cref="SecurityEventSerializerContext"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Auditing")]
public sealed class SecurityEventSerializerContextShould
{
    [Fact]
    public void BeInternalAndSealed()
    {
        typeof(SecurityEventSerializerContext).IsNotPublic.ShouldBeTrue();
        typeof(SecurityEventSerializerContext).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void InheritFromJsonSerializerContext()
    {
        typeof(SecurityEventSerializerContext).IsSubclassOf(typeof(JsonSerializerContext)).ShouldBeTrue();
    }

    [Fact]
    public void SupportSecurityEventSerialization()
    {
        // Arrange
        var securityEvent = new SecurityEvent
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            EventType = SecurityEventType.AuthenticationSuccess,
            Description = "Test event",
            Severity = SecuritySeverity.Low,
        };

        // Act
        var json = JsonSerializer.Serialize(securityEvent, SecurityEventSerializerContext.Default.SecurityEvent);

        // Assert
        json.ShouldNotBeNullOrWhiteSpace();
        json.ShouldContain("description");
    }

    [Fact]
    public void SupportSecurityEventQuerySerialization()
    {
        // Arrange
        var query = new SecurityEventQuery
        {
            EventType = SecurityEventType.AuthenticationFailure,
        };

        // Act
        var json = JsonSerializer.Serialize(query, SecurityEventSerializerContext.Default.SecurityEventQuery);

        // Assert
        json.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void UseCamelCaseNaming()
    {
        // Arrange
        var securityEvent = new SecurityEvent
        {
            Id = Guid.NewGuid(),
            EventType = SecurityEventType.AuthenticationSuccess,
            Description = "test",
            Severity = SecuritySeverity.Low,
        };

        // Act
        var json = JsonSerializer.Serialize(securityEvent, SecurityEventSerializerContext.Default.SecurityEvent);

        // Assert
        json.ShouldContain("\"eventType\"");
        json.ShouldContain("\"description\"");
        json.ShouldContain("\"severity\"");
    }
}
