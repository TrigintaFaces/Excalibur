// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Auditing;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class AuditSearchCriteriaShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new AuditSearchCriteria();

		sut.StartTime.ShouldBeNull();
		sut.EndTime.ShouldBeNull();
		sut.UserId.ShouldBeNull();
		sut.EventType.ShouldBeNull();
		sut.Severity.ShouldBeNull();
		sut.SourceIpAddress.ShouldBeNull();
		sut.MaxResults.ShouldBe(100);
		sut.Skip.ShouldBe(0);
		sut.SortDescending.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var start = DateTimeOffset.UtcNow.AddDays(-7);
		var end = DateTimeOffset.UtcNow;

		var sut = new AuditSearchCriteria
		{
			StartTime = start,
			EndTime = end,
			UserId = "admin",
			EventType = "Authentication",
			Severity = "Critical",
			SourceIpAddress = "10.0.0.1",
			MaxResults = 50,
			Skip = 10,
			SortDescending = false,
		};

		sut.StartTime.ShouldBe(start);
		sut.EndTime.ShouldBe(end);
		sut.UserId.ShouldBe("admin");
		sut.EventType.ShouldBe("Authentication");
		sut.Severity.ShouldBe("Critical");
		sut.SourceIpAddress.ShouldBe("10.0.0.1");
		sut.MaxResults.ShouldBe(50);
		sut.Skip.ShouldBe(10);
		sut.SortDescending.ShouldBeFalse();
	}
}
