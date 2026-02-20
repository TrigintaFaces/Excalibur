// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Audit;

/// <summary>
/// Unit tests for <see cref="AuditQuery"/> record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "Audit")]
public sealed class AuditQueryShould : UnitTestBase
{
	[Fact]
	public void HaveDefaultValues()
	{
		// Act
		var query = new AuditQuery();

		// Assert
		query.StartDate.ShouldBeNull();
		query.EndDate.ShouldBeNull();
		query.EventTypes.ShouldBeNull();
		query.Outcomes.ShouldBeNull();
		query.ActorId.ShouldBeNull();
		query.ResourceId.ShouldBeNull();
		query.ResourceType.ShouldBeNull();
		query.MinimumClassification.ShouldBeNull();
		query.TenantId.ShouldBeNull();
		query.CorrelationId.ShouldBeNull();
		query.Action.ShouldBeNull();
		query.IpAddress.ShouldBeNull();
		query.MaxResults.ShouldBe(100);
		query.Skip.ShouldBe(0);
		query.OrderByDescending.ShouldBeTrue();
	}

	[Fact]
	public void CreateFullyPopulatedQuery()
	{
		// Arrange
		var startDate = DateTimeOffset.UtcNow.AddDays(-7);
		var endDate = DateTimeOffset.UtcNow;
		var eventTypes = new List<AuditEventType> { AuditEventType.Security, AuditEventType.DataAccess };
		var outcomes = new List<AuditOutcome> { AuditOutcome.Success, AuditOutcome.Failure };

		// Act
		var query = new AuditQuery
		{
			StartDate = startDate,
			EndDate = endDate,
			EventTypes = eventTypes,
			Outcomes = outcomes,
			ActorId = "user-123",
			ResourceId = "doc-456",
			ResourceType = "Document",
			MinimumClassification = DataClassification.Confidential,
			TenantId = "tenant-abc",
			CorrelationId = "corr-def",
			Action = "Read",
			IpAddress = "192.168.1.1",
			MaxResults = 50,
			Skip = 100,
			OrderByDescending = false
		};

		// Assert
		query.StartDate.ShouldBe(startDate);
		query.EndDate.ShouldBe(endDate);
		query.EventTypes.ShouldBe(eventTypes);
		query.Outcomes.ShouldBe(outcomes);
		query.ActorId.ShouldBe("user-123");
		query.ResourceId.ShouldBe("doc-456");
		query.ResourceType.ShouldBe("Document");
		query.MinimumClassification.ShouldBe(DataClassification.Confidential);
		query.TenantId.ShouldBe("tenant-abc");
		query.CorrelationId.ShouldBe("corr-def");
		query.Action.ShouldBe("Read");
		query.IpAddress.ShouldBe("192.168.1.1");
		query.MaxResults.ShouldBe(50);
		query.Skip.ShouldBe(100);
		query.OrderByDescending.ShouldBeFalse();
	}

	[Fact]
	public void SupportDateRangeFiltering()
	{
		// Arrange
		var startDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var endDate = new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.Zero);

		// Act
		var query = new AuditQuery
		{
			StartDate = startDate,
			EndDate = endDate
		};

		// Assert
		query.StartDate.ShouldBe(startDate);
		query.EndDate.ShouldBe(endDate);
	}

	[Fact]
	public void SupportEventTypeFiltering()
	{
		// Act
		var query = new AuditQuery
		{
			EventTypes = [AuditEventType.Authentication, AuditEventType.Authorization]
		};

		// Assert
		_ = query.EventTypes.ShouldNotBeNull();
		query.EventTypes.Count.ShouldBe(2);
		query.EventTypes.ShouldContain(AuditEventType.Authentication);
		query.EventTypes.ShouldContain(AuditEventType.Authorization);
	}

	[Fact]
	public void SupportPagination()
	{
		// Act - page 3 with 20 items per page
		var query = new AuditQuery
		{
			MaxResults = 20,
			Skip = 40
		};

		// Assert
		query.MaxResults.ShouldBe(20);
		query.Skip.ShouldBe(40);
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var query1 = new AuditQuery
		{
			ActorId = "user-123",
			MaxResults = 50
		};

		var query2 = new AuditQuery
		{
			ActorId = "user-123",
			MaxResults = 50
		};

		// Assert
		query1.ShouldBe(query2);
	}

	[Theory]
	[InlineData(DataClassification.Public)]
	[InlineData(DataClassification.Internal)]
	[InlineData(DataClassification.Confidential)]
	[InlineData(DataClassification.Restricted)]
	public void SupportAllClassificationFilters(DataClassification classification)
	{
		// Act
		var query = new AuditQuery
		{
			MinimumClassification = classification
		};

		// Assert
		query.MinimumClassification.ShouldBe(classification);
	}
}
