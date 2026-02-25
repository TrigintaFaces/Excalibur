// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Auditing;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class SecurityEventTypeShould
{
	[Fact]
	public void DefineExpectedValues()
	{
		SecurityEventType.Authentication.ShouldBe((SecurityEventType)0);
		SecurityEventType.DataAccess.ShouldBe((SecurityEventType)1);
		SecurityEventType.ConfigurationChange.ShouldBe((SecurityEventType)2);
		SecurityEventType.SecurityIncident.ShouldBe((SecurityEventType)3);
		SecurityEventType.AccessControl.ShouldBe((SecurityEventType)4);
		SecurityEventType.Other.ShouldBe((SecurityEventType)5);
	}

	[Fact]
	public void HaveExactlySixMembers()
	{
		Enum.GetValues<SecurityEventType>().Length.ShouldBe(6);
	}
}
