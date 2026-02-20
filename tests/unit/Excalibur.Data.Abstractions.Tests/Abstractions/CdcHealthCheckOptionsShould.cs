// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.CloudNative;

namespace Excalibur.Data.Tests.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
public sealed class CdcHealthCheckOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var options = new CdcHealthCheckOptions();

		options.DegradedLagThreshold.ShouldBe(1000);
		options.UnhealthyLagThreshold.ShouldBe(10000);
		options.UnhealthyInactivityTimeout.ShouldBe(TimeSpan.FromMinutes(10));
		options.DegradedInactivityTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var options = new CdcHealthCheckOptions
		{
			DegradedLagThreshold = 500,
			UnhealthyLagThreshold = 5000,
			UnhealthyInactivityTimeout = TimeSpan.FromMinutes(20),
			DegradedInactivityTimeout = TimeSpan.FromMinutes(2)
		};

		options.DegradedLagThreshold.ShouldBe(500);
		options.UnhealthyLagThreshold.ShouldBe(5000);
		options.UnhealthyInactivityTimeout.ShouldBe(TimeSpan.FromMinutes(20));
		options.DegradedInactivityTimeout.ShouldBe(TimeSpan.FromMinutes(2));
	}
}
