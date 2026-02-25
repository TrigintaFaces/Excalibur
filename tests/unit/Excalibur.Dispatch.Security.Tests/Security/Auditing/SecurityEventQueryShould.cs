// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Auditing;

/// <summary>
/// Unit tests for <see cref="SecurityEventQuery"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class SecurityEventQueryShould
{
	[Fact]
	public void HaveNullStartTime_ByDefault()
	{
		// Arrange & Act
		var query = new SecurityEventQuery();

		// Assert
		query.StartTime.ShouldBeNull();
	}

	[Fact]
	public void HaveNullEndTime_ByDefault()
	{
		// Arrange & Act
		var query = new SecurityEventQuery();

		// Assert
		query.EndTime.ShouldBeNull();
	}

	[Fact]
	public void HaveNullEventType_ByDefault()
	{
		// Arrange & Act
		var query = new SecurityEventQuery();

		// Assert
		query.EventType.ShouldBeNull();
	}

	[Fact]
	public void HaveNullMinimumSeverity_ByDefault()
	{
		// Arrange & Act
		var query = new SecurityEventQuery();

		// Assert
		query.MinimumSeverity.ShouldBeNull();
	}

	[Fact]
	public void HaveNullUserId_ByDefault()
	{
		// Arrange & Act
		var query = new SecurityEventQuery();

		// Assert
		query.UserId.ShouldBeNull();
	}

	[Fact]
	public void HaveNullSourceIp_ByDefault()
	{
		// Arrange & Act
		var query = new SecurityEventQuery();

		// Assert
		query.SourceIp.ShouldBeNull();
	}

	[Fact]
	public void HaveNullCorrelationId_ByDefault()
	{
		// Arrange & Act
		var query = new SecurityEventQuery();

		// Assert
		query.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultMaxResultsOf1000()
	{
		// Arrange & Act
		var query = new SecurityEventQuery();

		// Assert
		query.MaxResults.ShouldBe(1000);
	}

	[Fact]
	public void AllowSettingStartTime()
	{
		// Arrange
		var query = new SecurityEventQuery();
		var startTime = DateTimeOffset.UtcNow.AddHours(-24);

		// Act
		query.StartTime = startTime;

		// Assert
		query.StartTime.ShouldBe(startTime);
	}

	[Fact]
	public void AllowSettingEndTime()
	{
		// Arrange
		var query = new SecurityEventQuery();
		var endTime = DateTimeOffset.UtcNow;

		// Act
		query.EndTime = endTime;

		// Assert
		query.EndTime.ShouldBe(endTime);
	}

	[Fact]
	public void AllowSettingEventType()
	{
		// Arrange
		var query = new SecurityEventQuery();

		// Act
		query.EventType = SecurityEventType.AuthenticationFailure;

		// Assert
		query.EventType.ShouldBe(SecurityEventType.AuthenticationFailure);
	}

	[Fact]
	public void AllowSettingMinimumSeverity()
	{
		// Arrange
		var query = new SecurityEventQuery();

		// Act
		query.MinimumSeverity = SecuritySeverity.High;

		// Assert
		query.MinimumSeverity.ShouldBe(SecuritySeverity.High);
	}

	[Fact]
	public void AllowSettingUserId()
	{
		// Arrange
		var query = new SecurityEventQuery();

		// Act
		query.UserId = "user-123";

		// Assert
		query.UserId.ShouldBe("user-123");
	}

	[Fact]
	public void AllowSettingSourceIp()
	{
		// Arrange
		var query = new SecurityEventQuery();

		// Act
		query.SourceIp = "192.168.1.100";

		// Assert
		query.SourceIp.ShouldBe("192.168.1.100");
	}

	[Fact]
	public void AllowSettingCorrelationId()
	{
		// Arrange
		var query = new SecurityEventQuery();
		var correlationId = Guid.NewGuid();

		// Act
		query.CorrelationId = correlationId;

		// Assert
		query.CorrelationId.ShouldBe(correlationId);
	}

	[Fact]
	public void AllowSettingMaxResults()
	{
		// Arrange
		var query = new SecurityEventQuery();

		// Act
		query.MaxResults = 500;

		// Assert
		query.MaxResults.ShouldBe(500);
	}

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange
		var startTime = DateTimeOffset.UtcNow.AddDays(-7);
		var endTime = DateTimeOffset.UtcNow;
		var correlationId = Guid.NewGuid();

		// Act
		var query = new SecurityEventQuery
		{
			StartTime = startTime,
			EndTime = endTime,
			EventType = SecurityEventType.InjectionAttempt,
			MinimumSeverity = SecuritySeverity.Critical,
			UserId = "admin",
			SourceIp = "10.0.0.1",
			CorrelationId = correlationId,
			MaxResults = 100,
		};

		// Assert
		query.StartTime.ShouldBe(startTime);
		query.EndTime.ShouldBe(endTime);
		query.EventType.ShouldBe(SecurityEventType.InjectionAttempt);
		query.MinimumSeverity.ShouldBe(SecuritySeverity.Critical);
		query.UserId.ShouldBe("admin");
		query.SourceIp.ShouldBe("10.0.0.1");
		query.CorrelationId.ShouldBe(correlationId);
		query.MaxResults.ShouldBe(100);
	}

	[Theory]
	[InlineData(SecurityEventType.AuthenticationSuccess)]
	[InlineData(SecurityEventType.AuthenticationFailure)]
	[InlineData(SecurityEventType.RateLimitExceeded)]
	[InlineData(SecurityEventType.EncryptionFailure)]
	public void AllowFilteringByAnyEventType(SecurityEventType eventType)
	{
		// Arrange
		var query = new SecurityEventQuery();

		// Act
		query.EventType = eventType;

		// Assert
		query.EventType.ShouldBe(eventType);
	}

	[Theory]
	[InlineData(SecuritySeverity.Low)]
	[InlineData(SecuritySeverity.Medium)]
	[InlineData(SecuritySeverity.High)]
	[InlineData(SecuritySeverity.Critical)]
	public void AllowFilteringByAnySeverity(SecuritySeverity severity)
	{
		// Arrange
		var query = new SecurityEventQuery();

		// Act
		query.MinimumSeverity = severity;

		// Assert
		query.MinimumSeverity.ShouldBe(severity);
	}

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(SecurityEventQuery).IsSealed.ShouldBeTrue();
	}
}
