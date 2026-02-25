// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Auditing;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class AuditReportTypeShould
{
	[Fact]
	public void DefineExpectedValues()
	{
		AuditReportType.Comprehensive.ShouldBe((AuditReportType)0);
		AuditReportType.Authentication.ShouldBe((AuditReportType)1);
		AuditReportType.DataAccess.ShouldBe((AuditReportType)2);
		AuditReportType.ConfigurationChanges.ShouldBe((AuditReportType)3);
		AuditReportType.SecurityIncidents.ShouldBe((AuditReportType)4);
		AuditReportType.Compliance.ShouldBe((AuditReportType)5);
	}

	[Fact]
	public void HaveExactlySixMembers()
	{
		Enum.GetValues<AuditReportType>().Length.ShouldBe(6);
	}
}
