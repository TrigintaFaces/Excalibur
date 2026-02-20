// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Auditing;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class SecurityEventSeverityShould
{
	[Fact]
	public void DefineExpectedValues()
	{
		SecurityEventSeverity.Low.ShouldBe((SecurityEventSeverity)0);
		SecurityEventSeverity.Medium.ShouldBe((SecurityEventSeverity)1);
		SecurityEventSeverity.High.ShouldBe((SecurityEventSeverity)2);
		SecurityEventSeverity.Critical.ShouldBe((SecurityEventSeverity)3);
	}

	[Fact]
	public void HaveExactlyFourMembers()
	{
		Enum.GetValues<SecurityEventSeverity>().Length.ShouldBe(4);
	}
}
