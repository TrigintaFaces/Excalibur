// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Routing;

namespace Excalibur.Dispatch.Tests.Messaging.Routing;

/// <summary>
///     Tests for the <see cref="RouteDeliveryStatus" /> enum.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RouteDeliveryStatusShould
{
	[Fact]
	public void HaveNotDispatchedValue()
	{
		RouteDeliveryStatus.NotDispatched.ShouldBe((RouteDeliveryStatus)0);
	}

	[Fact]
	public void HaveSucceededValue()
	{
		RouteDeliveryStatus.Succeeded.ShouldNotBe(RouteDeliveryStatus.NotDispatched);
	}

	[Fact]
	public void HaveFailedValue()
	{
		RouteDeliveryStatus.Failed.ShouldNotBe(RouteDeliveryStatus.NotDispatched);
		RouteDeliveryStatus.Failed.ShouldNotBe(RouteDeliveryStatus.Succeeded);
	}

	[Fact]
	public void HaveThreeDistinctValues()
	{
		var values = Enum.GetValues<RouteDeliveryStatus>();
		values.Length.ShouldBe(3);
	}
}
