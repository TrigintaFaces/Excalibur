// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Routing;
using Excalibur.Dispatch.Routing.LoadBalancing;

namespace Excalibur.Dispatch.Tests.Routing;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class LoadBalancerModelsShould
{
	// --- RouteDefinition ---

	[Fact]
	public void RouteDefinition_DefaultValues_AreCorrect()
	{
		// Act
		var route = new RouteDefinition();

		// Assert
		route.RouteId.ShouldBe(string.Empty);
		route.Name.ShouldBe(string.Empty);
		route.Endpoint.ShouldBe(string.Empty);
		route.Weight.ShouldBe(100);
		route.Metadata.ShouldNotBeNull();
		route.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void RouteDefinition_AllProperties_AreSettable()
	{
		// Act
		var route = new RouteDefinition
		{
			RouteId = "route-1",
			Name = "Primary",
			Endpoint = "https://api.example.com",
			Weight = 200,
		};

		// Assert
		route.RouteId.ShouldBe("route-1");
		route.Name.ShouldBe("Primary");
		route.Endpoint.ShouldBe("https://api.example.com");
		route.Weight.ShouldBe(200);
	}

	[Fact]
	public void RouteDefinition_Metadata_CanAddEntries()
	{
		// Arrange
		var route = new RouteDefinition();

		// Act
		route.Metadata["region"] = "us-east-1";

		// Assert
		route.Metadata.Count.ShouldBe(1);
		route.Metadata["region"].ShouldBe("us-east-1");
	}

	// --- RoutingContext ---

	[Fact]
	public void RoutingContext_DefaultValues_AreCorrect()
	{
		// Act
		var context = new RoutingContext();

		// Assert
		context.Timestamp.ShouldNotBe(default);
		context.CancellationToken.ShouldBe(CancellationToken.None);
		context.Source.ShouldBeNull();
		context.SourceEndpoint.ShouldBeNull();
		context.MessageType.ShouldBeNull();
		context.CorrelationId.ShouldBeNull();
		context.Properties.ShouldNotBeNull();
		context.Properties.ShouldBeEmpty();
	}

	[Fact]
	public void RoutingContext_AllProperties_AreSettable()
	{
		// Arrange
		using var cts = new CancellationTokenSource();

		// Act
		var context = new RoutingContext
		{
			Timestamp = DateTimeOffset.UnixEpoch,
			CancellationToken = cts.Token,
			Source = "service-a",
			MessageType = "OrderCreated",
			CorrelationId = "corr-123",
		};

		// Assert
		context.Timestamp.ShouldBe(DateTimeOffset.UnixEpoch);
		context.CancellationToken.ShouldBe(cts.Token);
		context.Source.ShouldBe("service-a");
		context.MessageType.ShouldBe("OrderCreated");
		context.CorrelationId.ShouldBe("corr-123");
	}

	[Fact]
	public void RoutingContext_SourceEndpoint_IsAliasForSource()
	{
		// Arrange
		var context = new RoutingContext();

		// Act - set via SourceEndpoint
		context.SourceEndpoint = "my-endpoint";

		// Assert - read via Source
		context.Source.ShouldBe("my-endpoint");

		// Act - set via Source
		context.Source = "other-source";

		// Assert - read via SourceEndpoint
		context.SourceEndpoint.ShouldBe("other-source");
	}

	[Fact]
	public void RoutingContext_Properties_CanAddEntries()
	{
		// Arrange
		var context = new RoutingContext();

		// Act
		context.Properties["priority"] = "high";
		context.Properties["version"] = 2;

		// Assert
		context.Properties.Count.ShouldBe(2);
		context.Properties["priority"].ShouldBe("high");
		context.Properties["version"].ShouldBe(2);
	}
}
