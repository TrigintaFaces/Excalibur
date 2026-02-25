// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class StepResultShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var duration = TimeSpan.FromSeconds(5);
		var sut = new StepResult
		{
			StepNumber = 1,
			Name = "CreateIndex",
			Success = true,
			Duration = duration,
		};

		sut.StepNumber.ShouldBe(1);
		sut.Name.ShouldBe("CreateIndex");
		sut.Success.ShouldBeTrue();
		sut.Duration.ShouldBe(duration);
	}

	[Fact]
	public void HaveNullDefaultForErrorMessage()
	{
		var sut = new StepResult
		{
			StepNumber = 1,
			Name = "Test",
			Success = true,
			Duration = TimeSpan.FromSeconds(1),
		};

		sut.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingErrorMessage()
	{
		var sut = new StepResult
		{
			StepNumber = 3,
			Name = "ValidateData",
			Success = false,
			Duration = TimeSpan.FromMinutes(2),
			ErrorMessage = "15 documents failed validation",
		};

		sut.ErrorMessage.ShouldBe("15 documents failed validation");
	}
}
