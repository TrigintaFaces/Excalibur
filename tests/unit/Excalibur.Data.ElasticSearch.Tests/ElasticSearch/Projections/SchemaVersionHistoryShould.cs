// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class SchemaVersionHistoryShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var versions = new List<SchemaVersionRegistration>
		{
			new() { ProjectionType = "Order", Version = "1.0", Schema = new object() },
		};

		var sut = new SchemaVersionHistory
		{
			ProjectionType = "OrderProjection",
			CurrentVersion = "1.0",
			Versions = versions,
		};

		sut.ProjectionType.ShouldBe("OrderProjection");
		sut.CurrentVersion.ShouldBe("1.0");
		sut.Versions.ShouldBeSameAs(versions);
	}

	[Fact]
	public void HaveNullDefaultForMigrationHistory()
	{
		var sut = new SchemaVersionHistory
		{
			ProjectionType = "Test",
			CurrentVersion = "1.0",
			Versions = [],
		};

		sut.MigrationHistory.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingMigrationHistory()
	{
		var history = new List<SchemaMigrationResult>
		{
			new()
			{
				Success = true,
				PlanId = "plan-1",
				StartTime = DateTimeOffset.UtcNow.AddDays(-1),
				EndTime = DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(5),
				CompletedSteps = [],
			},
		};

		var sut = new SchemaVersionHistory
		{
			ProjectionType = "OrderProjection",
			CurrentVersion = "2.0",
			Versions = [],
			MigrationHistory = history,
		};

		sut.MigrationHistory.ShouldBeSameAs(history);
	}
}
