// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class SchemaMigrationResultShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var start = DateTimeOffset.UtcNow;
		var end = start.AddMinutes(15);
		var steps = new List<StepResult>
		{
			new() { StepNumber = 1, Name = "CreateIndex", Success = true, Duration = TimeSpan.FromSeconds(2) },
		};

		var sut = new SchemaMigrationResult
		{
			Success = true,
			PlanId = "plan-1",
			StartTime = start,
			EndTime = end,
			CompletedSteps = steps,
		};

		sut.Success.ShouldBeTrue();
		sut.PlanId.ShouldBe("plan-1");
		sut.StartTime.ShouldBe(start);
		sut.EndTime.ShouldBe(end);
		sut.CompletedSteps.ShouldBeSameAs(steps);
	}

	[Fact]
	public void HaveDefaultsForOptionalProperties()
	{
		var sut = new SchemaMigrationResult
		{
			Success = true,
			PlanId = "plan-1",
			StartTime = DateTimeOffset.UtcNow,
			EndTime = DateTimeOffset.UtcNow,
			CompletedSteps = [],
		};

		sut.DocumentsMigrated.ShouldBe(0);
		sut.DocumentsFailed.ShouldBe(0);
		sut.Errors.ShouldBeNull();
		sut.Warnings.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var errors = new List<string> { "Document doc-5 failed mapping" };
		var warnings = new List<string> { "Slow processing detected" };

		var sut = new SchemaMigrationResult
		{
			Success = false,
			PlanId = "plan-2",
			StartTime = DateTimeOffset.UtcNow,
			EndTime = DateTimeOffset.UtcNow.AddMinutes(30),
			CompletedSteps = [],
			DocumentsMigrated = 9995,
			DocumentsFailed = 5,
			Errors = errors,
			Warnings = warnings,
		};

		sut.DocumentsMigrated.ShouldBe(9995);
		sut.DocumentsFailed.ShouldBe(5);
		sut.Errors.ShouldBeSameAs(errors);
		sut.Warnings.ShouldBeSameAs(warnings);
	}
}
