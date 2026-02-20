// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AzureEventHubsCloudEventOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new AzureEventHubsCloudEventOptions();

		// Assert
		options.UsePartitionKeys.ShouldBeTrue();
		options.PartitionKeyStrategy.ShouldBe(PartitionKeyStrategy.CorrelationId);
		options.MaxBatchSize.ShouldBe(100);
		options.MaxBatchSizeBytes.ShouldBe(1024 * 1024);
		options.EnableCapture.ShouldBeFalse();
		options.CaptureFileNameFormat.ShouldBe(
			"cloudevents/{Namespace}/{EventHub}/{PartitionId}/{Year}/{Month}/{Day}/{Hour}/{Minute}/{Second}");
		options.UseSchemaRegistry.ShouldBeFalse();
		options.SchemaRegistryNamespace.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new AzureEventHubsCloudEventOptions
		{
			UsePartitionKeys = false,
			PartitionKeyStrategy = PartitionKeyStrategy.TenantId,
			MaxBatchSize = 50,
			MaxBatchSizeBytes = 512 * 1024,
			EnableCapture = true,
			CaptureFileNameFormat = "custom/{EventHub}/{PartitionId}",
			UseSchemaRegistry = true,
			SchemaRegistryNamespace = "my-schema-ns",
		};

		// Assert
		options.UsePartitionKeys.ShouldBeFalse();
		options.PartitionKeyStrategy.ShouldBe(PartitionKeyStrategy.TenantId);
		options.MaxBatchSize.ShouldBe(50);
		options.MaxBatchSizeBytes.ShouldBe(512 * 1024);
		options.EnableCapture.ShouldBeTrue();
		options.CaptureFileNameFormat.ShouldBe("custom/{EventHub}/{PartitionId}");
		options.UseSchemaRegistry.ShouldBeTrue();
		options.SchemaRegistryNamespace.ShouldBe("my-schema-ns");
	}

	[Theory]
	[InlineData(PartitionKeyStrategy.CorrelationId, 0)]
	[InlineData(PartitionKeyStrategy.TenantId, 1)]
	[InlineData(PartitionKeyStrategy.UserId, 2)]
	[InlineData(PartitionKeyStrategy.Source, 3)]
	[InlineData(PartitionKeyStrategy.Type, 4)]
	[InlineData(PartitionKeyStrategy.Custom, 5)]
	public void SupportAllPartitionKeyStrategies(PartitionKeyStrategy strategy, int expectedValue)
	{
		// Assert
		((int)strategy).ShouldBe(expectedValue);
	}
}
