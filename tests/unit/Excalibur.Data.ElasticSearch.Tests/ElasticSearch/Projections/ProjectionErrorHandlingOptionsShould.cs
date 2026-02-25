// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ProjectionErrorHandlingOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new ProjectionErrorHandlingOptions();

		sut.StoreErrors.ShouldBeTrue();
		sut.LogDetailedErrors.ShouldBeTrue();
		sut.ErrorIndexName.ShouldBe("projection-errors");
		sut.RetentionPeriod.ShouldBe(TimeSpan.FromDays(30));
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var sut = new ProjectionErrorHandlingOptions
		{
			StoreErrors = false,
			LogDetailedErrors = false,
			ErrorIndexName = "custom-errors",
			RetentionPeriod = TimeSpan.FromDays(7),
		};

		sut.StoreErrors.ShouldBeFalse();
		sut.LogDetailedErrors.ShouldBeFalse();
		sut.ErrorIndexName.ShouldBe("custom-errors");
		sut.RetentionPeriod.ShouldBe(TimeSpan.FromDays(7));
	}
}
