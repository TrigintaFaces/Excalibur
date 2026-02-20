// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ProjectionRebuildRequestShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new ProjectionRebuildRequest
		{
			ProjectionType = "OrderProjection",
			TargetIndexName = "orders-v2",
		};

		sut.ProjectionType.ShouldBe("OrderProjection");
		sut.TargetIndexName.ShouldBe("orders-v2");
		sut.SourceIndexName.ShouldBeNull();
		sut.FromTimestamp.ShouldBeNull();
		sut.ToTimestamp.ShouldBeNull();
		sut.BatchSize.ShouldBe(1000);
		sut.CreateNewIndex.ShouldBeTrue();
		sut.UseAliasing.ShouldBeTrue();
		sut.MaxDegreeOfParallelism.ShouldBe(4);
		sut.Metadata.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var to = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
		var metadata = new Dictionary<string, object> { ["reason"] = "schema change" };

		var sut = new ProjectionRebuildRequest
		{
			ProjectionType = "OrderProjection",
			TargetIndexName = "orders-v2",
			SourceIndexName = "orders-v1",
			FromTimestamp = from,
			ToTimestamp = to,
			BatchSize = 500,
			CreateNewIndex = false,
			UseAliasing = false,
			MaxDegreeOfParallelism = 8,
			Metadata = metadata,
		};

		sut.SourceIndexName.ShouldBe("orders-v1");
		sut.FromTimestamp.ShouldBe(from);
		sut.ToTimestamp.ShouldBe(to);
		sut.BatchSize.ShouldBe(500);
		sut.CreateNewIndex.ShouldBeFalse();
		sut.UseAliasing.ShouldBeFalse();
		sut.MaxDegreeOfParallelism.ShouldBe(8);
		sut.Metadata.ShouldBeSameAs(metadata);
	}
}
