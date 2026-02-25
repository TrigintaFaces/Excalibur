// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class CredentialRotationOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new CredentialRotationOptions();

		sut.Enabled.ShouldBeFalse();
		sut.RotationInterval.ShouldBe(TimeSpan.FromDays(30));
		sut.WarningThreshold.ShouldBe(TimeSpan.FromDays(7));
		sut.MaintenanceWindowOnly.ShouldBeTrue();
		sut.MaxRetries.ShouldBe(3);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var sut = new CredentialRotationOptions
		{
			Enabled = true,
			RotationInterval = TimeSpan.FromDays(14),
			WarningThreshold = TimeSpan.FromDays(3),
			MaintenanceWindowOnly = false,
			MaxRetries = 5,
		};

		sut.Enabled.ShouldBeTrue();
		sut.RotationInterval.ShouldBe(TimeSpan.FromDays(14));
		sut.WarningThreshold.ShouldBe(TimeSpan.FromDays(3));
		sut.MaintenanceWindowOnly.ShouldBeFalse();
		sut.MaxRetries.ShouldBe(5);
	}
}
