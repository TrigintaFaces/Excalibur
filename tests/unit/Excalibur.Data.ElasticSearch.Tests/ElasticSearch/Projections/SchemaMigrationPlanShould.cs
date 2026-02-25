// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class SchemaMigrationPlanShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var steps = new List<MigrationStep>
		{
			new()
			{
				StepNumber = 1,
				Name = "CreateTargetIndex",
				Description = "Create the target index with new mapping",
				OperationType = StepOperationType.CreateIndex,
			},
		};

		var sut = new SchemaMigrationPlan
		{
			PlanId = "plan-1",
			ProjectionType = "OrderProjection",
			Strategy = MigrationStrategy.Reindex,
			Steps = steps,
		};

		sut.PlanId.ShouldBe("plan-1");
		sut.ProjectionType.ShouldBe("OrderProjection");
		sut.Strategy.ShouldBe(MigrationStrategy.Reindex);
		sut.Steps.ShouldBeSameAs(steps);
	}

	[Fact]
	public void HaveDefaultsForOptionalProperties()
	{
		var sut = new SchemaMigrationPlan
		{
			PlanId = "plan-1",
			ProjectionType = "Test",
			Strategy = MigrationStrategy.Reindex,
			Steps = [],
		};

		sut.EstimatedDuration.ShouldBeNull();
		sut.EstimatedDocuments.ShouldBeNull();
		sut.IsReversible.ShouldBeFalse();
		sut.RollbackSteps.ShouldBeNull();
		sut.ValidationChecks.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var rollback = new List<MigrationStep>
		{
			new()
			{
				StepNumber = 1,
				Name = "DeleteTargetIndex",
				Description = "Remove the target index",
				OperationType = StepOperationType.DeleteIndex,
			},
		};
		var checks = new List<string> { "Verify document count", "Verify mapping" };

		var sut = new SchemaMigrationPlan
		{
			PlanId = "plan-2",
			ProjectionType = "OrderProjection",
			Strategy = MigrationStrategy.UpdateInPlace,
			Steps = [],
			EstimatedDuration = TimeSpan.FromHours(1),
			EstimatedDocuments = 500000,
			IsReversible = true,
			RollbackSteps = rollback,
			ValidationChecks = checks,
		};

		sut.EstimatedDuration.ShouldBe(TimeSpan.FromHours(1));
		sut.EstimatedDocuments.ShouldBe(500000);
		sut.IsReversible.ShouldBeTrue();
		sut.RollbackSteps.ShouldBeSameAs(rollback);
		sut.ValidationChecks.ShouldBeSameAs(checks);
	}
}
