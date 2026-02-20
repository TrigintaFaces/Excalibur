// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Abstractions.Tests.Routing;

/// <summary>
/// Unit tests for <see cref="RouteInfo"/> in the Abstractions layer.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RouteInfoAbstractionsShould
{
	#region Constructor tests

	[Fact]
	public void CreateWithRequiredParameters()
	{
		// Arrange & Act
		var routeInfo = new RouteInfo("order-route", "billing-service", 0);

		// Assert
		routeInfo.Name.ShouldBe("order-route");
		routeInfo.Endpoint.ShouldBe("billing-service");
		routeInfo.Priority.ShouldBe(0);
	}

	[Fact]
	public void UseDefaultPriorityZero()
	{
		// Arrange & Act
		var routeInfo = new RouteInfo("route", "endpoint");

		// Assert
		routeInfo.Priority.ShouldBe(0);
	}

	[Fact]
	public void AcceptNegativePriority()
	{
		// Arrange & Act
		var routeInfo = new RouteInfo("route", "endpoint", -10);

		// Assert
		routeInfo.Priority.ShouldBe(-10);
	}

	[Fact]
	public void AcceptHighPriority()
	{
		// Arrange & Act
		var routeInfo = new RouteInfo("route", "endpoint", 100);

		// Assert
		routeInfo.Priority.ShouldBe(100);
	}

	[Fact]
	public void ThrowOnNullName()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(
			() => new RouteInfo(null!, "endpoint"));
	}

	[Fact]
	public void ThrowOnEmptyName()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(
			() => new RouteInfo("", "endpoint"));
	}

	[Fact]
	public void ThrowOnNullEndpoint()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(
			() => new RouteInfo("route", null!));
	}

	[Fact]
	public void ThrowOnEmptyEndpoint()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(
			() => new RouteInfo("route", ""));
	}

	#endregion

	#region BusName property

	[Fact]
	public void DefaultBusNameToNull()
	{
		// Arrange & Act
		var routeInfo = new RouteInfo("route", "endpoint");

		// Assert
		routeInfo.BusName.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingBusName()
	{
		// Arrange
		var routeInfo = new RouteInfo("route", "endpoint");

		// Act
		routeInfo.BusName = "rabbitmq";

		// Assert
		routeInfo.BusName.ShouldBe("rabbitmq");
	}

	#endregion

	#region Metadata property

	[Fact]
	public void InitializeEmptyMetadata()
	{
		// Arrange & Act
		var routeInfo = new RouteInfo("route", "endpoint");

		// Assert
		routeInfo.Metadata.ShouldNotBeNull();
		routeInfo.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void AllowAddingMetadata()
	{
		// Arrange
		var routeInfo = new RouteInfo("route", "endpoint");

		// Act
		routeInfo.Metadata["health"] = "healthy";
		routeInfo.Metadata["region"] = "us-east-1";
		routeInfo.Metadata["rule_type"] = "conditional";

		// Assert
		routeInfo.Metadata.Count.ShouldBe(3);
		routeInfo.Metadata["health"].ShouldBe("healthy");
		routeInfo.Metadata["region"].ShouldBe("us-east-1");
	}

	[Fact]
	public void AllowNullMetadataValues()
	{
		// Arrange
		var routeInfo = new RouteInfo("route", "endpoint");

		// Act
		routeInfo.Metadata["optional"] = null;

		// Assert
		routeInfo.Metadata.ContainsKey("optional").ShouldBeTrue();
		routeInfo.Metadata["optional"].ShouldBeNull();
	}

	[Fact]
	public void SupportMetadataWithInitializer()
	{
		// Arrange & Act
		var routeInfo = new RouteInfo("route", "endpoint")
		{
			Metadata =
			{
				["health"] = "healthy",
				["stop_on_match"] = true,
			},
		};

		// Assert
		routeInfo.Metadata.Count.ShouldBe(2);
		routeInfo.Metadata["stop_on_match"].ShouldBe(true);
	}

	#endregion
}
