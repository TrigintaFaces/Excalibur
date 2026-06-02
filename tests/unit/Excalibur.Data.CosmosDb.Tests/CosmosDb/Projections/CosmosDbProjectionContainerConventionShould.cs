// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb.Projections;

namespace Excalibur.Data.CosmosDb.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class CosmosDbProjectionContainerConventionShould : UnitTestBase
{
	#region GetContainerName<TProjection> Tests

	[Fact]
	public void ReturnContainerNameFromOptions_Generic()
	{
		// Arrange
		var options = new CosmosDbProjectionStoreOptions { ContainerName = "my-projections" };

		// Act
		var result = CosmosDbProjectionContainerConvention.GetContainerName<SampleProjection>(options);

		// Assert
		result.ShouldBe("my-projections");
	}

	[Fact]
	public void ReturnDefaultContainerName_Generic()
	{
		// Arrange
		var options = new CosmosDbProjectionStoreOptions();

		// Act
		var result = CosmosDbProjectionContainerConvention.GetContainerName<SampleProjection>(options);

		// Assert
		result.ShouldBe("projections");
	}

	[Fact]
	public void ThrowWhenOptionsIsNull_Generic()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => CosmosDbProjectionContainerConvention.GetContainerName<SampleProjection>(null!));
	}

	#endregion

	#region GetContainerName(options, projectionTypeName) Tests

	[Fact]
	public void ReturnContainerNameFromOptions_ByName()
	{
		// Arrange
		var options = new CosmosDbProjectionStoreOptions { ContainerName = "custom-container" };

		// Act
		var result = CosmosDbProjectionContainerConvention.GetContainerName(options, "OrderSummary");

		// Assert
		result.ShouldBe("custom-container");
	}

	[Fact]
	public void ThrowWhenOptionsIsNull_ByName()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => CosmosDbProjectionContainerConvention.GetContainerName(null!, "OrderSummary"));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowWhenProjectionTypeNameIsNullOrWhitespace(string? projectionTypeName)
	{
		// Arrange
		var options = new CosmosDbProjectionStoreOptions();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => CosmosDbProjectionContainerConvention.GetContainerName(options, projectionTypeName!));
	}

	#endregion

	private sealed class SampleProjection
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
	}
}
