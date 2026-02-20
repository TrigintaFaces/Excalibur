// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class SecurityAlertingOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new SecurityAlertingOptions();

		sut.Enabled.ShouldBeTrue();
		sut.MinimumSeverity.ShouldBe(SecurityEventSeverity.Medium);
		sut.NotificationChannels.ShouldNotBeNull();
		sut.NotificationChannels.ShouldBeEmpty();
		sut.EscalationTimeout.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var sut = new SecurityAlertingOptions
		{
			Enabled = false,
			MinimumSeverity = SecurityEventSeverity.Critical,
			NotificationChannels = ["email", "slack"],
			EscalationTimeout = TimeSpan.FromHours(1),
		};

		sut.Enabled.ShouldBeFalse();
		sut.MinimumSeverity.ShouldBe(SecurityEventSeverity.Critical);
		sut.NotificationChannels.Count.ShouldBe(2);
		sut.EscalationTimeout.ShouldBe(TimeSpan.FromHours(1));
	}
}
