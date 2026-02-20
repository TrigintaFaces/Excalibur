// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Auditing;

[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class SecurityAuditingModelsCoverageShould
{
    [Fact]
    public void SecurityEventSetAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        var correlationId = Guid.NewGuid();

        // Act
        var evt = new SecurityEvent
        {
            Id = id,
            Timestamp = timestamp,
            EventType = SecurityEventType.AuthenticationFailure,
            Description = "Login failed",
            Severity = SecuritySeverity.High,
            CorrelationId = correlationId,
            UserId = "user-123",
            SourceIp = "192.168.1.1",
            UserAgent = "Mozilla/5.0",
            MessageType = "LoginCommand",
            AdditionalData = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["attempt"] = 3,
            },
        };

        // Assert
        evt.Id.ShouldBe(id);
        evt.Timestamp.ShouldBe(timestamp);
        evt.EventType.ShouldBe(SecurityEventType.AuthenticationFailure);
        evt.Description.ShouldBe("Login failed");
        evt.Severity.ShouldBe(SecuritySeverity.High);
        evt.CorrelationId.ShouldBe(correlationId);
        evt.UserId.ShouldBe("user-123");
        evt.SourceIp.ShouldBe("192.168.1.1");
        evt.UserAgent.ShouldBe("Mozilla/5.0");
        evt.MessageType.ShouldBe("LoginCommand");
        evt.AdditionalData["attempt"].ShouldBe(3);
    }

    [Fact]
    public void SecurityEventHaveDefaultValues()
    {
        // Act
        var evt = new SecurityEvent();

        // Assert
        evt.Id.ShouldBe(Guid.Empty);
        evt.Description.ShouldBe(string.Empty);
        evt.CorrelationId.ShouldBeNull();
        evt.UserId.ShouldBeNull();
        evt.SourceIp.ShouldBeNull();
        evt.UserAgent.ShouldBeNull();
        evt.MessageType.ShouldBeNull();
        evt.AdditionalData.ShouldNotBeNull();
        evt.AdditionalData.Count.ShouldBe(0);
    }

    [Fact]
    public void SecurityEventQueryHaveDefaultValues()
    {
        // Act
        var query = new SecurityEventQuery();

        // Assert
        query.StartTime.ShouldBeNull();
        query.EndTime.ShouldBeNull();
        query.EventType.ShouldBeNull();
        query.MinimumSeverity.ShouldBeNull();
        query.UserId.ShouldBeNull();
        query.SourceIp.ShouldBeNull();
        query.CorrelationId.ShouldBeNull();
        query.MaxResults.ShouldBe(1000);
    }

    [Fact]
    public void SecurityEventQuerySetAllProperties()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow.AddHours(-1);
        var endTime = DateTimeOffset.UtcNow;
        var correlationId = Guid.NewGuid();

        // Act
        var query = new SecurityEventQuery
        {
            StartTime = startTime,
            EndTime = endTime,
            EventType = SecurityEventType.ValidationFailure,
            MinimumSeverity = SecuritySeverity.Medium,
            UserId = "user-abc",
            SourceIp = "10.0.0.1",
            CorrelationId = correlationId,
            MaxResults = 50,
        };

        // Assert
        query.StartTime.ShouldBe(startTime);
        query.EndTime.ShouldBe(endTime);
        query.EventType.ShouldBe(SecurityEventType.ValidationFailure);
        query.MinimumSeverity.ShouldBe(SecuritySeverity.Medium);
        query.UserId.ShouldBe("user-abc");
        query.SourceIp.ShouldBe("10.0.0.1");
        query.CorrelationId.ShouldBe(correlationId);
        query.MaxResults.ShouldBe(50);
    }

    [Theory]
    [InlineData(SecurityEventType.AuthenticationFailure)]
    [InlineData(SecurityEventType.AuthorizationFailure)]
    [InlineData(SecurityEventType.ValidationFailure)]
    [InlineData(SecurityEventType.EncryptionFailure)]
    [InlineData(SecurityEventType.RateLimitExceeded)]
    [InlineData(SecurityEventType.ValidationError)]
    public void SecurityEventTypeEnumValues(SecurityEventType eventType)
    {
        // Assert - verify enum value is defined
        Enum.IsDefined(typeof(SecurityEventType), eventType).ShouldBeTrue();
    }

    [Theory]
    [InlineData(SecuritySeverity.Low)]
    [InlineData(SecuritySeverity.Medium)]
    [InlineData(SecuritySeverity.High)]
    [InlineData(SecuritySeverity.Critical)]
    public void SecuritySeverityEnumValues(SecuritySeverity severity)
    {
        // Assert - verify enum value is defined
        Enum.IsDefined(typeof(SecuritySeverity), severity).ShouldBeTrue();
    }
}
