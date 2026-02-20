// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Routing;

namespace Excalibur.Dispatch.Tests.Messaging.Routing;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RouteResultShould
{
	[Fact]
	public void DefaultConstructor_SetLocalBusName()
	{
		// Act
		var result = new RouteResult();

		// Assert
		result.MessageBusName.ShouldBe("Local");
		result.IsLocal.ShouldBeTrue();
		result.RouteMetadata.ShouldBeNull();
		result.DeliveryStatus.ShouldBe(RouteDeliveryStatus.NotDispatched);
		result.Failure.ShouldBeNull();
	}

	[Fact]
	public void Constructor_WithNullBusName_DefaultToLocal()
	{
		// Act
		var result = new RouteResult(messageBusName: null);

		// Assert
		result.MessageBusName.ShouldBe("Local");
		result.IsLocal.ShouldBeTrue();
	}

	[Fact]
	public void Constructor_WithWhitespaceBusName_DefaultToLocal()
	{
		// Act
		var result = new RouteResult(messageBusName: "   ");

		// Assert
		result.MessageBusName.ShouldBe("Local");
		result.IsLocal.ShouldBeTrue();
	}

	[Fact]
	public void Constructor_WithCustomBusName_SetBusName()
	{
		// Act
		var result = new RouteResult(messageBusName: "kafka-bus");

		// Assert
		result.MessageBusName.ShouldBe("kafka-bus");
		result.IsLocal.ShouldBeFalse();
	}

	[Fact]
	public void IsLocal_CaseInsensitive()
	{
		// Act
		var result = new RouteResult(messageBusName: "LOCAL");

		// Assert
		result.IsLocal.ShouldBeTrue();
	}

	[Fact]
	public void DeliveryStatus_CanBeUpdated()
	{
		// Arrange
		var result = new RouteResult();

		// Act
		result.DeliveryStatus = RouteDeliveryStatus.Succeeded;

		// Assert
		result.DeliveryStatus.ShouldBe(RouteDeliveryStatus.Succeeded);
	}

	[Fact]
	public void Failure_CanBeSet()
	{
		// Arrange
		var result = new RouteResult();
		var failure = new RouteFailure("connection refused");

		// Act
		result.Failure = failure;

		// Assert
		result.Failure.ShouldNotBeNull();
		result.Failure.Message.ShouldBe("connection refused");
	}
}
