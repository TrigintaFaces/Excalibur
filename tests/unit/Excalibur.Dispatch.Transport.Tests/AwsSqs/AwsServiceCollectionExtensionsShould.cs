// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.Scheduler;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AwsServiceCollectionExtensionsShould
{
	[Fact]
	public void ThrowWhenServicesIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			AwsServiceCollectionExtensions.AddAwsEventBridgeScheduler(null!));
	}

	[Fact]
	public void RegisterSchedulerServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddAwsEventBridgeScheduler(opts => opts.Region = "us-east-1");

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IAmazonScheduler));
		services.ShouldContain(sd => sd.ServiceType == typeof(IMessageScheduler));
	}

	[Fact]
	public void RegisterWithoutConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddAwsEventBridgeScheduler();

		// Assert
		result.ShouldBeSameAs(services);
		services.ShouldContain(sd => sd.ServiceType == typeof(IAmazonScheduler));
	}

	[Fact]
	public void BeIdempotent()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddAwsEventBridgeScheduler(opts => opts.Region = "us-east-1");
		services.AddAwsEventBridgeScheduler(opts => opts.Region = "us-east-1");

		// Assert — TryAddSingleton prevents duplicates
		var schedulerCount = services.Count(sd => sd.ServiceType == typeof(IAmazonScheduler));
		schedulerCount.ShouldBe(1);
	}
}
