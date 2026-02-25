// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Routing;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RoutingOptionsShould
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var options = new RoutingOptions();

		// Assert
		options.RoutingPolicyPath.ShouldBeNull();
		options.DefaultRemoteBusName.ShouldBeNull();
	}

	[Fact]
	public void AllProperties_AreSettable()
	{
		// Act
		var options = new RoutingOptions
		{
			RoutingPolicyPath = "/policies/routing",
			DefaultRemoteBusName = "azure-service-bus",
		};

		// Assert
		options.RoutingPolicyPath.ShouldBe("/policies/routing");
		options.DefaultRemoteBusName.ShouldBe("azure-service-bus");
	}
}
