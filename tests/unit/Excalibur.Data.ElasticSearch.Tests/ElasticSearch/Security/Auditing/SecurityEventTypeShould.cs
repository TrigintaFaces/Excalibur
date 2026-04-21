// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Auditing;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class SecurityEventTypeShould
{
	[Fact]
	public void DefineExpectedValues()
	{
		SecurityAuditEventType.Authentication.ShouldBe((SecurityAuditEventType)0);
		SecurityAuditEventType.DataAccess.ShouldBe((SecurityAuditEventType)1);
		SecurityAuditEventType.ConfigurationChange.ShouldBe((SecurityAuditEventType)2);
		SecurityAuditEventType.SecurityIncident.ShouldBe((SecurityAuditEventType)3);
		SecurityAuditEventType.AccessControl.ShouldBe((SecurityAuditEventType)4);
		SecurityAuditEventType.Other.ShouldBe((SecurityAuditEventType)5);
	}

	[Fact]
	public void HaveExactlySixMembers()
	{
		Enum.GetValues<SecurityAuditEventType>().Length.ShouldBe(6);
	}
}
