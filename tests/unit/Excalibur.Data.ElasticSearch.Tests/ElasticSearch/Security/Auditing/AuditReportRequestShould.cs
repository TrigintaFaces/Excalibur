// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Auditing;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class AuditReportRequestShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new AuditReportRequest();

		sut.StartTime.ShouldBe(default);
		sut.EndTime.ShouldBe(default);
		sut.ReportType.ShouldBe(AuditReportType.Comprehensive);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var start = DateTimeOffset.UtcNow.AddDays(-7);
		var end = DateTimeOffset.UtcNow;

		var sut = new AuditReportRequest
		{
			StartTime = start,
			EndTime = end,
			ReportType = AuditReportType.Authentication,
		};

		sut.StartTime.ShouldBe(start);
		sut.EndTime.ShouldBe(end);
		sut.ReportType.ShouldBe(AuditReportType.Authentication);
	}
}
