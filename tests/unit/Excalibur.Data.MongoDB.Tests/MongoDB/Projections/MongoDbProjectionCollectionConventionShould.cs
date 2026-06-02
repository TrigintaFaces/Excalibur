// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Projections;

namespace Excalibur.Data.MongoDB.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class MongoDbProjectionCollectionConventionShould : UnitTestBase
{
	#region GetCollectionName<TProjection> Tests

	[Fact]
	public void ReturnCollectionNameFromOptions_Generic()
	{
		// Arrange
		var options = new MongoDbProjectionStoreOptions { CollectionName = "my-projections" };

		// Act
		var result = MongoDbProjectionCollectionConvention.GetCollectionName<SampleProjection>(options);

		// Assert
		result.ShouldBe("my-projections");
	}

	[Fact]
	public void ReturnDefaultCollectionName_Generic()
	{
		// Arrange
		var options = new MongoDbProjectionStoreOptions();

		// Act
		var result = MongoDbProjectionCollectionConvention.GetCollectionName<SampleProjection>(options);

		// Assert
		result.ShouldBe("projections");
	}

	[Fact]
	public void ThrowWhenOptionsIsNull_Generic()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => MongoDbProjectionCollectionConvention.GetCollectionName<SampleProjection>(null!));
	}

	#endregion

	#region GetCollectionName(options, projectionTypeName) Tests

	[Fact]
	public void ReturnCollectionNameFromOptions_ByName()
	{
		// Arrange
		var options = new MongoDbProjectionStoreOptions { CollectionName = "custom-collection" };

		// Act
		var result = MongoDbProjectionCollectionConvention.GetCollectionName(options, "OrderSummary");

		// Assert
		result.ShouldBe("custom-collection");
	}

	[Fact]
	public void ThrowWhenOptionsIsNull_ByName()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => MongoDbProjectionCollectionConvention.GetCollectionName(null!, "OrderSummary"));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowWhenProjectionTypeNameIsNullOrWhitespace(string? projectionTypeName)
	{
		// Arrange
		var options = new MongoDbProjectionStoreOptions();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => MongoDbProjectionCollectionConvention.GetCollectionName(options, projectionTypeName!));
	}

	#endregion

	private sealed class SampleProjection
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
	}
}
