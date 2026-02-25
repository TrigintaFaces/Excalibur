// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.LongPolling;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class LongPollingServiceCollectionExtensionsShould
{
	[Fact]
	public void ThrowWhenServicesIsNull_Configuration()
	{
		var config = new LongPollingConfiguration
		{
			QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/test-queue"),
		};

		Should.Throw<ArgumentNullException>(() =>
			LongPollingServiceCollectionExtensions.AddAwsLongPolling(null!, config));
	}

	[Fact]
	public void ThrowWhenConfigurationIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddAwsLongPolling((LongPollingConfiguration)null!));
	}

	[Fact]
	public void ThrowWhenServicesIsNull_Action()
	{
		Should.Throw<ArgumentNullException>(() =>
			LongPollingServiceCollectionExtensions.AddAwsLongPolling(null!, _ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureActionIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddAwsLongPolling((Action<LongPollingConfiguration>)null!));
	}

	[Fact]
	public void RegisterAdaptiveLongPollingServices()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new LongPollingConfiguration
		{
			QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/test-queue"),
			EnableAdaptivePolling = true,
		};

		// Act
		services.AddAwsLongPolling(config);

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(LongPollingConfiguration));
		services.ShouldContain(sd => sd.ServiceType == typeof(ILongPollingStrategy));
		services.ShouldContain(sd => sd.ServiceType == typeof(IPollingMetricsCollector));
		services.ShouldContain(sd => sd.ServiceType == typeof(ILongPollingReceiver));
		services.ShouldContain(sd => sd.ServiceType == typeof(LongPollingOptimizer));
	}

	[Fact]
	public void RegisterFixedPollingWhenAdaptiveDisabled()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new LongPollingConfiguration
		{
			QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/test-queue"),
			EnableAdaptivePolling = false,
		};

		// Act
		services.AddAwsLongPolling(config);

		// Assert
		var strategyDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(ILongPollingStrategy));
		strategyDescriptor.ShouldNotBeNull();
		strategyDescriptor.ImplementationType.ShouldBe(typeof(FixedLongPollingStrategy));
	}

	[Fact]
	public void RegisterWithConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddAwsLongPolling(opts =>
		{
			opts.QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/test-queue");
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(LongPollingConfiguration));
	}

	[Fact]
	public void ReplaceStrategyWithCustomGeneric()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new LongPollingConfiguration
		{
			QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/test-queue"),
		};

		// Act
		services.AddAwsLongPolling<AdaptiveLongPollingStrategy>(config);

		// Assert
		var strategyDescriptor = services.Last(sd => sd.ServiceType == typeof(ILongPollingStrategy));
		strategyDescriptor.ImplementationType.ShouldBe(typeof(AdaptiveLongPollingStrategy));
	}
}
