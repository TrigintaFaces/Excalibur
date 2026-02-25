// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class MigrationStepShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var sut = new MigrationStep
		{
			StepNumber = 1,
			Name = "CreateTargetIndex",
			Description = "Create the target index with the new schema mapping",
			OperationType = StepOperationType.CreateIndex,
		};

		sut.StepNumber.ShouldBe(1);
		sut.Name.ShouldBe("CreateTargetIndex");
		sut.Description.ShouldBe("Create the target index with the new schema mapping");
		sut.OperationType.ShouldBe(StepOperationType.CreateIndex);
	}

	[Fact]
	public void HaveDefaultsForOptionalProperties()
	{
		var sut = new MigrationStep
		{
			StepNumber = 1,
			Name = "Test",
			Description = "Test step",
			OperationType = StepOperationType.CreateIndex,
		};

		sut.IsCritical.ShouldBeFalse();
		sut.EstimatedDuration.ShouldBeNull();
		sut.Parameters.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var parameters = new Dictionary<string, object>
		{
			["batchSize"] = 1000,
			["timeout"] = "5m",
		};

		var sut = new MigrationStep
		{
			StepNumber = 2,
			Name = "ReindexData",
			Description = "Reindex data from source to target",
			OperationType = StepOperationType.Reindex,
			IsCritical = true,
			EstimatedDuration = TimeSpan.FromMinutes(30),
			Parameters = parameters,
		};

		sut.IsCritical.ShouldBeTrue();
		sut.EstimatedDuration.ShouldBe(TimeSpan.FromMinutes(30));
		sut.Parameters.ShouldBeSameAs(parameters);
	}
}
