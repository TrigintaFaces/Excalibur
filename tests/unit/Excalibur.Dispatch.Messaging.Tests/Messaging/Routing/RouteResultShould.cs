// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Routing;

namespace Excalibur.Dispatch.Tests.Messaging.Routing;

/// <summary>
///     Tests for the <see cref="RouteResult" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RouteResultShould
{
	[Fact]
	public void CreateWithDefaultBusName()
	{
		var sut = new RouteResult();
		sut.MessageBusName.ShouldBe("Local");
	}

	[Fact]
	public void CreateWithSpecifiedBusName()
	{
		var sut = new RouteResult("RabbitMQ");
		sut.MessageBusName.ShouldBe("RabbitMQ");
	}

	[Fact]
	public void BeLocalWhenNullBusName()
	{
		var sut = new RouteResult(null);
		sut.IsLocal.ShouldBeTrue();
	}

	[Fact]
	public void BeLocalWhenEmptyBusName()
	{
		var sut = new RouteResult(string.Empty);
		sut.IsLocal.ShouldBeTrue();
	}

	[Fact]
	public void BeLocalWhenWhiteSpaceBusName()
	{
		var sut = new RouteResult("   ");
		sut.IsLocal.ShouldBeTrue();
	}

	[Fact]
	public void NotBeLocalForExternalBus()
	{
		var sut = new RouteResult("RabbitMQ");
		sut.IsLocal.ShouldBeFalse();
	}

	[Fact]
	public void HaveNotDispatchedStatusByDefault()
	{
		var sut = new RouteResult();
		sut.DeliveryStatus.ShouldBe(RouteDeliveryStatus.NotDispatched);
	}

	[Fact]
	public void AllowSettingDeliveryStatus()
	{
		var sut = new RouteResult();
		sut.DeliveryStatus = RouteDeliveryStatus.Succeeded;
		sut.DeliveryStatus.ShouldBe(RouteDeliveryStatus.Succeeded);
	}

	[Fact]
	public void HaveNullFailureByDefault()
	{
		var sut = new RouteResult();
		sut.Failure.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingFailure()
	{
		var failure = new RouteFailure("Test failure");
		var sut = new RouteResult { Failure = failure };
		sut.Failure.ShouldBe(failure);
	}

	[Fact]
	public void AcceptNullRouteMetadata()
	{
		var sut = new RouteResult(routeMetadata: null);
		sut.RouteMetadata.ShouldBeNull();
	}

	[Fact]
	public void ImplementIRouteResult()
	{
		var sut = new RouteResult();
		sut.ShouldBeAssignableTo<IRouteResult>();
	}
}
