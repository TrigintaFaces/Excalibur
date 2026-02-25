// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class SchemaMigrationRequestShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var schema = new { mappings = new { properties = new { } } };
		var sut = new SchemaMigrationRequest
		{
			ProjectionType = "OrderProjection",
			SourceIndex = "orders-v1",
			TargetIndex = "orders-v2",
			Strategy = MigrationStrategy.Reindex,
			NewSchema = schema,
		};

		sut.ProjectionType.ShouldBe("OrderProjection");
		sut.SourceIndex.ShouldBe("orders-v1");
		sut.TargetIndex.ShouldBe("orders-v2");
		sut.Strategy.ShouldBe(MigrationStrategy.Reindex);
		sut.NewSchema.ShouldBeSameAs(schema);
	}

	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new SchemaMigrationRequest
		{
			ProjectionType = "Test",
			SourceIndex = "src",
			TargetIndex = "tgt",
			Strategy = MigrationStrategy.Reindex,
			NewSchema = new object(),
		};

		sut.FieldMappings.ShouldBeNull();
		sut.TransformationScripts.ShouldBeNull();
		sut.ValidateData.ShouldBeTrue();
		sut.BatchSize.ShouldBe(1000);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var fieldMappings = new Dictionary<string, string> { ["old_name"] = "new_name" };
		var scripts = new Dictionary<string, string> { ["status"] = "ctx._source.status = ctx._source.status.toUpperCase()" };

		var sut = new SchemaMigrationRequest
		{
			ProjectionType = "OrderProjection",
			SourceIndex = "orders-v1",
			TargetIndex = "orders-v2",
			Strategy = MigrationStrategy.UpdateInPlace,
			NewSchema = new object(),
			FieldMappings = fieldMappings,
			TransformationScripts = scripts,
			ValidateData = false,
			BatchSize = 500,
		};

		sut.FieldMappings.ShouldBeSameAs(fieldMappings);
		sut.TransformationScripts.ShouldBeSameAs(scripts);
		sut.ValidateData.ShouldBeFalse();
		sut.BatchSize.ShouldBe(500);
	}
}
