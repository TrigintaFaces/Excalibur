// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Routing;

namespace Excalibur.Dispatch.Tests.Messaging.Routing;

/// <summary>
/// Unit tests for <see cref="RouteInfo"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Routing")]
[Trait("Priority", "0")]
public sealed class RouteInfoShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_SetsNameProperty()
	{
		// Act
		var route = new RouteInfo(name: "test-route", endpoint: "queue://test");

		// Assert
		route.Name.ShouldBe("test-route");
	}

	[Fact]
	public void Constructor_SetsEndpointProperty()
	{
		// Act
		var route = new RouteInfo(name: "test-route", endpoint: "queue://test");

		// Assert
		route.Endpoint.ShouldBe("queue://test");
	}

	[Fact]
	public void Constructor_SetsPriorityPropertyWithDefault()
	{
		// Act
		var route = new RouteInfo(name: "test-route", endpoint: "queue://test");

		// Assert
		route.Priority.ShouldBe(0);
	}

	[Fact]
	public void Constructor_SetsPriorityPropertyWithValue()
	{
		// Act
		var route = new RouteInfo(name: "test-route", endpoint: "queue://test", priority: 100);

		// Assert
		route.Priority.ShouldBe(100);
	}

	[Fact]
	public void Constructor_WithNullName_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new RouteInfo(name: null!, endpoint: "queue://test"));
	}

	[Fact]
	public void Constructor_WithEmptyName_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new RouteInfo(name: string.Empty, endpoint: "queue://test"));
	}

	[Fact]
	public void Constructor_WithNullEndpoint_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new RouteInfo(name: "test-route", endpoint: null!));
	}

	[Fact]
	public void Constructor_WithEmptyEndpoint_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new RouteInfo(name: "test-route", endpoint: string.Empty));
	}

	#endregion

	#region BusName Tests

	[Fact]
	public void BusName_DefaultIsNull()
	{
		// Act
		var route = new RouteInfo(name: "test-route", endpoint: "queue://test");

		// Assert
		route.BusName.ShouldBeNull();
	}

	[Fact]
	public void BusName_CanBeSet()
	{
		// Arrange
		var route = new RouteInfo(name: "test-route", endpoint: "queue://test");

		// Act
		route.BusName = "kafka-bus";

		// Assert
		route.BusName.ShouldBe("kafka-bus");
	}

	#endregion

	#region Metadata Tests

	[Fact]
	public void Metadata_DefaultIsEmptyDictionary()
	{
		// Act
		var route = new RouteInfo(name: "test-route", endpoint: "queue://test");

		// Assert
		route.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void Metadata_CanAddEntries()
	{
		// Arrange
		var route = new RouteInfo(name: "test-route", endpoint: "queue://test");

		// Act
		route.Metadata["region"] = "us-east-1";
		route.Metadata["version"] = "v2";

		// Assert
		route.Metadata["region"].ShouldBe("us-east-1");
		route.Metadata["version"].ShouldBe("v2");
	}

	[Fact]
	public void Metadata_CanUseInitSyntax()
	{
		// Act
		var route = new RouteInfo(name: "test-route", endpoint: "queue://test")
		{
			Metadata =
			{
				["health"] = "healthy",
				["capacity"] = 1000,
			},
		};

		// Assert
		route.Metadata["health"].ShouldBe("healthy");
		route.Metadata["capacity"].ShouldBe(1000);
	}

	[Fact]
	public void Metadata_CanStoreNullValues()
	{
		// Arrange
		var route = new RouteInfo(name: "test-route", endpoint: "queue://test");

		// Act
		route.Metadata["nullable-key"] = null;

		// Assert
		route.Metadata.ContainsKey("nullable-key").ShouldBeTrue();
		route.Metadata["nullable-key"].ShouldBeNull();
	}

	#endregion

	#region Priority Tests

	[Fact]
	public void Priority_CanBeNegative()
	{
		// Act
		var route = new RouteInfo(name: "test-route", endpoint: "queue://test", priority: -1);

		// Assert
		route.Priority.ShouldBe(-1);
	}

	[Fact]
	public void Priority_CanBeLargePositive()
	{
		// Act
		var route = new RouteInfo(name: "test-route", endpoint: "queue://test", priority: int.MaxValue);

		// Assert
		route.Priority.ShouldBe(int.MaxValue);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Route_ForPrimaryEndpoint_HasHighPriority()
	{
		// Act
		var route = new RouteInfo(name: "primary-orders", endpoint: "orders-queue", priority: 100)
		{
			BusName = "rabbitmq",
			Metadata =
			{
				["health"] = "healthy",
				["region"] = "primary",
			},
		};

		// Assert
		route.Priority.ShouldBeGreaterThan(50);
		route.Metadata["health"].ShouldBe("healthy");
	}

	[Fact]
	public void Route_ForFallbackEndpoint_HasLowerPriority()
	{
		// Act
		var route = new RouteInfo(name: "fallback-orders", endpoint: "orders-backup-queue", priority: 10)
		{
			BusName = "kafka",
			Metadata =
			{
				["health"] = "healthy",
				["region"] = "fallback",
			},
		};

		// Assert
		route.Priority.ShouldBeLessThan(50);
	}

	#endregion
}
