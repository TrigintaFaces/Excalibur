// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Routing;

namespace Excalibur.Dispatch.Tests.Options.Routing;

/// <summary>
/// Unit tests for <see cref="RoutingOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class RoutingOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_RoutingPolicyPath_IsNull()
	{
		// Arrange & Act
		var options = new RoutingOptions();

		// Assert
		options.RoutingPolicyPath.ShouldBeNull();
	}

	[Fact]
	public void Default_DefaultRemoteBusName_IsNull()
	{
		// Arrange & Act
		var options = new RoutingOptions();

		// Assert
		options.DefaultRemoteBusName.ShouldBeNull();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void RoutingPolicyPath_CanBeSet()
	{
		// Arrange
		var options = new RoutingOptions();

		// Act
		options.RoutingPolicyPath = "/etc/dispatch/routing.wasm";

		// Assert
		options.RoutingPolicyPath.ShouldBe("/etc/dispatch/routing.wasm");
	}

	[Fact]
	public void DefaultRemoteBusName_CanBeSet()
	{
		// Arrange
		var options = new RoutingOptions();

		// Act
		options.DefaultRemoteBusName = "rabbitmq-cluster";

		// Assert
		options.DefaultRemoteBusName.ShouldBe("rabbitmq-cluster");
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new RoutingOptions
		{
			RoutingPolicyPath = "/config/routing.policy",
			DefaultRemoteBusName = "kafka-cluster",
		};

		// Assert
		options.RoutingPolicyPath.ShouldBe("/config/routing.policy");
		options.DefaultRemoteBusName.ShouldBe("kafka-cluster");
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForRemoteBus_SetsDefaultBus()
	{
		// Act
		var options = new RoutingOptions
		{
			DefaultRemoteBusName = "azure-servicebus",
		};

		// Assert
		_ = options.DefaultRemoteBusName.ShouldNotBeNull();
	}

	#endregion
}
