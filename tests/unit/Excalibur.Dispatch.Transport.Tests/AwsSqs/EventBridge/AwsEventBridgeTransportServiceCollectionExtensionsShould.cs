// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.EventBridge;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AwsEventBridgeTransportServiceCollectionExtensionsShould
{
	[Fact]
	public void ThrowWhenServicesIsNullForNamedTransport()
	{
		Should.Throw<ArgumentNullException>(() =>
			AwsEventBridgeTransportServiceCollectionExtensions.AddAwsEventBridgeTransport(
				null!, "test", _ => { }));
	}

	[Fact]
	public void ThrowWhenNameIsNullOrEmpty()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentException>(() =>
			services.AddAwsEventBridgeTransport("", _ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddAwsEventBridgeTransport("test", null!));
	}

	[Fact]
	public void RegisterEventBridgeTransportWithDefaultName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddAwsEventBridgeTransport(eb =>
		{
			eb.EventBusName("test-bus")
			  .DefaultSource("com.test");
		});

		// Assert
		result.ShouldBeSameAs(services);
		services.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void RegisterEventBridgeTransportWithCustomName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddAwsEventBridgeTransport("my-eb", eb =>
		{
			eb.EventBusName("my-event-bus")
			  .Region("us-west-2")
			  .DefaultSource("com.test.app")
			  .DefaultDetailType("MyEvent")
			  .EnableArchiving(retentionDays: 30, archiveName: "my-archive");
		});

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void ExposeDefaultTransportName()
	{
		AwsEventBridgeTransportServiceCollectionExtensions.DefaultTransportName
			.ShouldBe("aws-eventbridge");
	}
}
