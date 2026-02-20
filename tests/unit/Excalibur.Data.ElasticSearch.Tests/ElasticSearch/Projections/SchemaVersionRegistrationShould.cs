// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class SchemaVersionRegistrationShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var schema = new { mappings = new { } };
		var sut = new SchemaVersionRegistration
		{
			ProjectionType = "OrderProjection",
			Version = "2.0",
			Schema = schema,
		};

		sut.ProjectionType.ShouldBe("OrderProjection");
		sut.Version.ShouldBe("2.0");
		sut.Schema.ShouldBeSameAs(schema);
	}

	[Fact]
	public void HaveDefaultRegisteredAtTimestamp()
	{
		var before = DateTimeOffset.UtcNow;
		var sut = new SchemaVersionRegistration
		{
			ProjectionType = "Test",
			Version = "1.0",
			Schema = new object(),
		};
		var after = DateTimeOffset.UtcNow;

		sut.RegisteredAt.ShouldBeGreaterThanOrEqualTo(before);
		sut.RegisteredAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void HaveNullDefaultsForOptionalProperties()
	{
		var sut = new SchemaVersionRegistration
		{
			ProjectionType = "Test",
			Version = "1.0",
			Schema = new object(),
		};

		sut.Description.ShouldBeNull();
		sut.MigrationNotes.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var customTime = DateTimeOffset.UtcNow.AddDays(-7);
		var sut = new SchemaVersionRegistration
		{
			ProjectionType = "OrderProjection",
			Version = "2.0",
			Schema = new object(),
			RegisteredAt = customTime,
			Description = "Added email field",
			MigrationNotes = "Run reindex from v1 to v2",
		};

		sut.RegisteredAt.ShouldBe(customTime);
		sut.Description.ShouldBe("Added email field");
		sut.MigrationNotes.ShouldBe("Run reindex from v1 to v2");
	}
}
