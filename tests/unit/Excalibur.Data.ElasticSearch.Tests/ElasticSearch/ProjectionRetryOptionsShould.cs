// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ProjectionRetryOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new ProjectionRetryOptions();

		sut.Enabled.ShouldBeTrue();
		sut.MaxIndexAttempts.ShouldBe(3);
		sut.MaxBulkAttempts.ShouldBe(2);
		sut.BaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
		sut.MaxDelay.ShouldBe(TimeSpan.FromSeconds(30));
		sut.UseExponentialBackoff.ShouldBeTrue();
		sut.JitterFactor.ShouldBe(0.2);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var sut = new ProjectionRetryOptions
		{
			Enabled = false,
			MaxIndexAttempts = 5,
			MaxBulkAttempts = 4,
			BaseDelay = TimeSpan.FromSeconds(2),
			MaxDelay = TimeSpan.FromMinutes(1),
			UseExponentialBackoff = false,
			JitterFactor = 0.5,
		};

		sut.Enabled.ShouldBeFalse();
		sut.MaxIndexAttempts.ShouldBe(5);
		sut.MaxBulkAttempts.ShouldBe(4);
		sut.BaseDelay.ShouldBe(TimeSpan.FromSeconds(2));
		sut.MaxDelay.ShouldBe(TimeSpan.FromMinutes(1));
		sut.UseExponentialBackoff.ShouldBeFalse();
		sut.JitterFactor.ShouldBe(0.5);
	}
}
