// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.EventBridge.Model;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS.Model;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AwsCloudEventsServiceCollectionExtensionsShould
{
	[Fact]
	public void ThrowWhenServicesIsNullForUseCloudEvents()
	{
		Should.Throw<ArgumentNullException>(() =>
			AwsCloudEventsServiceCollectionExtensions.UseCloudEvents(null!));
	}

	[Fact]
	public void RegisterCloudEventAdapters()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		AwsCloudEventsServiceCollectionExtensions.UseCloudEvents(services);

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(ICloudEventMapper<SendMessageRequest>));
		services.ShouldContain(sd => sd.ServiceType == typeof(ICloudEventMapper<PublishRequest>));
		services.ShouldContain(sd => sd.ServiceType == typeof(ICloudEventMapper<PutEventsRequestEntry>));
	}

	[Fact]
	public void RegisterWithCustomOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		AwsCloudEventsServiceCollectionExtensions.UseCloudEvents(services, opts =>
		{
			opts.DefaultMode = CloudEventMode.Binary;
		});

		// Assert
		services.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void ThrowWhenServicesIsNullForUseCloudEventsForSqs()
	{
		Should.Throw<ArgumentNullException>(() =>
			AwsCloudEventsServiceCollectionExtensions.UseCloudEventsForSqs(null!));
	}

	[Fact]
	public void RegisterSqsSpecificCloudEvents()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.UseCloudEventsForSqs(
			configureSqs: opts => opts.MaxBatchSize = 5);

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(ICloudEventMapper<SendMessageRequest>));
	}

	[Fact]
	public void ThrowWhenServicesIsNullForUseCloudEventsForSns()
	{
		Should.Throw<ArgumentNullException>(() =>
			AwsCloudEventsServiceCollectionExtensions.UseCloudEventsForSns(null!));
	}

	[Fact]
	public void RegisterSnsSpecificCloudEvents()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.UseCloudEventsForSns(
			configureSns: opts => opts.DefaultSubject = "test-subject");

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(ICloudEventMapper<PublishRequest>));
	}

	[Fact]
	public void ThrowWhenServicesIsNullForUseCloudEventsForEventBridge()
	{
		Should.Throw<ArgumentNullException>(() =>
			AwsCloudEventsServiceCollectionExtensions.UseCloudEventsForEventBridge(null!));
	}

	[Fact]
	public void RegisterEventBridgeSpecificCloudEvents()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.UseCloudEventsForEventBridge(
			configureEventBridge: opts => opts.EventBusName = "test-bus");

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(ICloudEventMapper<PutEventsRequestEntry>));
	}

	[Fact]
	public void ThrowWhenServicesIsNullForAddValidation()
	{
		Should.Throw<ArgumentNullException>(() =>
			AwsCloudEventsServiceCollectionExtensions.AddAwsCloudEventValidation(null!));
	}

	[Fact]
	public void RegisterValidationWithDoDCompliance()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddAwsCloudEventValidation(enableDoDCompliance: true);

		// Assert
		services.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void RegisterValidationWithoutDoDCompliance()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddAwsCloudEventValidation(enableDoDCompliance: false);

		// Assert — should still register without error
		services.Count.ShouldBeGreaterThanOrEqualTo(0);
	}

	[Fact]
	public void ThrowWhenServicesIsNullForAddTransformation()
	{
		Should.Throw<ArgumentNullException>(() =>
			AwsCloudEventsServiceCollectionExtensions.AddAwsCloudEventTransformation(
				null!, (_, _, _, _) => Task.CompletedTask));
	}

	[Fact]
	public void ThrowWhenTransformerIsNull()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() =>
			services.AddAwsCloudEventTransformation(null!));
	}

	[Fact]
	public void RegisterTransformation()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddAwsCloudEventTransformation((_, _, _, _) => Task.CompletedTask);

		// Assert
		services.Count.ShouldBeGreaterThan(0);
	}
}
