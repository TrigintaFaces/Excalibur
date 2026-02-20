// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Auditing;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class AuditReportShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new AuditReport();

		sut.ReportId.ShouldBe(Guid.Empty);
		sut.StartTime.ShouldBe(default);
		sut.EndTime.ShouldBe(default);
		sut.GeneratedAt.ShouldBe(default);
		sut.ReportType.ShouldBe(AuditReportType.Comprehensive);
		sut.TotalEvents.ShouldBe(0);
		sut.EventBreakdown.ShouldNotBeNull();
		sut.EventBreakdown.ShouldBeEmpty();
		sut.SeverityBreakdown.ShouldNotBeNull();
		sut.SeverityBreakdown.ShouldBeEmpty();
		sut.UniqueUsers.ShouldBe(0);
		sut.UniqueSourceIps.ShouldBe(0);
		sut.ComplianceStatus.ShouldNotBeNull();
		sut.ComplianceStatus.ShouldBeEmpty();
		sut.Recommendations.ShouldNotBeNull();
		sut.Recommendations.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var id = Guid.NewGuid();
		var start = DateTimeOffset.UtcNow.AddDays(-30);
		var end = DateTimeOffset.UtcNow;
		var generated = DateTimeOffset.UtcNow;
		var eventBreakdown = new Dictionary<string, int> { ["Authentication"] = 100 };
		var severityBreakdown = new Dictionary<string, int> { ["High"] = 5 };
		var compliance = new Dictionary<string, object> { ["SOC2"] = "Compliant" };
		var recommendations = new List<string> { "Enable MFA" };

		var sut = new AuditReport
		{
			ReportId = id,
			StartTime = start,
			EndTime = end,
			GeneratedAt = generated,
			ReportType = AuditReportType.Compliance,
			TotalEvents = 500,
			EventBreakdown = eventBreakdown,
			SeverityBreakdown = severityBreakdown,
			UniqueUsers = 50,
			UniqueSourceIps = 30,
			ComplianceStatus = compliance,
			Recommendations = recommendations,
		};

		sut.ReportId.ShouldBe(id);
		sut.StartTime.ShouldBe(start);
		sut.EndTime.ShouldBe(end);
		sut.GeneratedAt.ShouldBe(generated);
		sut.ReportType.ShouldBe(AuditReportType.Compliance);
		sut.TotalEvents.ShouldBe(500);
		sut.EventBreakdown.ShouldBeSameAs(eventBreakdown);
		sut.SeverityBreakdown.ShouldBeSameAs(severityBreakdown);
		sut.UniqueUsers.ShouldBe(50);
		sut.UniqueSourceIps.ShouldBe(30);
		sut.ComplianceStatus.ShouldBeSameAs(compliance);
		sut.Recommendations.ShouldBeSameAs(recommendations);
	}
}
