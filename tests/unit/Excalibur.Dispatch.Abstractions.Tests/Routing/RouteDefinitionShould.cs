// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Abstractions.Tests.Routing;

/// <summary>
/// Unit tests for <see cref="RouteDefinition"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Routing")]
[Trait("Priority", "0")]
public sealed class RouteDefinitionShould
{
	#region Default Values Tests

	[Fact]
	public void Default_RouteIdIsEmpty()
	{
		// Arrange & Act
		var route = new RouteDefinition();

		// Assert
		route.RouteId.ShouldBe(string.Empty);
	}

	[Fact]
	public void Default_NameIsEmpty()
	{
		// Arrange & Act
		var route = new RouteDefinition();

		// Assert
		route.Name.ShouldBe(string.Empty);
	}

	[Fact]
	public void Default_EndpointIsEmpty()
	{
		// Arrange & Act
		var route = new RouteDefinition();

		// Assert
		route.Endpoint.ShouldBe(string.Empty);
	}

	[Fact]
	public void Default_WeightIs100()
	{
		// Arrange & Act
		var route = new RouteDefinition();

		// Assert
		route.Weight.ShouldBe(100);
	}

	[Fact]
	public void Default_MetadataIsEmpty()
	{
		// Arrange & Act
		var route = new RouteDefinition();

		// Assert
		route.Metadata.ShouldBeEmpty();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void RouteId_CanBeSet()
	{
		// Arrange
		var route = new RouteDefinition();

		// Act
		route.RouteId = "route-123";

		// Assert
		route.RouteId.ShouldBe("route-123");
	}

	[Fact]
	public void Name_CanBeSet()
	{
		// Arrange
		var route = new RouteDefinition();

		// Act
		route.Name = "Primary Route";

		// Assert
		route.Name.ShouldBe("Primary Route");
	}

	[Fact]
	public void Endpoint_CanBeSet()
	{
		// Arrange
		var route = new RouteDefinition();

		// Act
		route.Endpoint = "https://api.example.com/v1";

		// Assert
		route.Endpoint.ShouldBe("https://api.example.com/v1");
	}

	[Fact]
	public void Weight_CanBeSet()
	{
		// Arrange
		var route = new RouteDefinition();

		// Act
		route.Weight = 50;

		// Assert
		route.Weight.ShouldBe(50);
	}

	[Fact]
	public void Weight_CanBeSetToZero()
	{
		// Arrange
		var route = new RouteDefinition();

		// Act
		route.Weight = 0;

		// Assert
		route.Weight.ShouldBe(0);
	}

	[Fact]
	public void Weight_CanBeSetToNegative()
	{
		// Arrange
		var route = new RouteDefinition();

		// Act
		route.Weight = -10;

		// Assert
		route.Weight.ShouldBe(-10);
	}

	#endregion

	#region Metadata Tests

	[Fact]
	public void Metadata_CanAddEntry()
	{
		// Arrange
		var route = new RouteDefinition();

		// Act
		route.Metadata["region"] = "us-east";

		// Assert
		route.Metadata.ShouldContainKeyAndValue("region", "us-east");
	}

	[Fact]
	public void Metadata_CanAddMultipleEntries()
	{
		// Arrange
		var route = new RouteDefinition();

		// Act
		route.Metadata["region"] = "us-east";
		route.Metadata["priority"] = 1;
		route.Metadata["tags"] = new[] { "primary", "production" };

		// Assert
		route.Metadata.Count.ShouldBe(3);
	}

	[Fact]
	public void Metadata_CanUpdateEntry()
	{
		// Arrange
		var route = new RouteDefinition();
		route.Metadata["region"] = "us-east";

		// Act
		route.Metadata["region"] = "us-west";

		// Assert
		route.Metadata["region"].ShouldBe("us-west");
	}

	[Fact]
	public void Metadata_CanRemoveEntry()
	{
		// Arrange
		var route = new RouteDefinition();
		route.Metadata["region"] = "us-east";

		// Act
		_ = route.Metadata.Remove("region");

		// Assert
		route.Metadata.ShouldNotContainKey("region");
	}

	[Fact]
	public void Metadata_CanClear()
	{
		// Arrange
		var route = new RouteDefinition();
		route.Metadata["region"] = "us-east";
		route.Metadata["priority"] = 1;

		// Act
		route.Metadata.Clear();

		// Assert
		route.Metadata.ShouldBeEmpty();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var route = new RouteDefinition
		{
			RouteId = "route-001",
			Name = "Primary API Route",
			Endpoint = "https://api.example.com/v2",
			Weight = 75,
		};

		// Assert
		route.RouteId.ShouldBe("route-001");
		route.Name.ShouldBe("Primary API Route");
		route.Endpoint.ShouldBe("https://api.example.com/v2");
		route.Weight.ShouldBe(75);
	}

	#endregion
}
