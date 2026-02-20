// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Auditing;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class AuditArchiveRequestShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new AuditArchiveRequest();

		sut.CutoffDate.ShouldBe(default);
		sut.ArchiveLocation.ShouldBe(string.Empty);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var cutoff = DateTimeOffset.UtcNow.AddMonths(-6);

		var sut = new AuditArchiveRequest
		{
			CutoffDate = cutoff,
			ArchiveLocation = "/archives/2025-q1",
		};

		sut.CutoffDate.ShouldBe(cutoff);
		sut.ArchiveLocation.ShouldBe("/archives/2025-q1");
	}
}
