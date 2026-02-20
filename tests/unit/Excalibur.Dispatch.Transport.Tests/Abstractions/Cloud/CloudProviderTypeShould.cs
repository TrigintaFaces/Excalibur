// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Cloud;

/// <summary>
/// Unit tests for <see cref="CloudProviderType"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class CloudProviderTypeShould
{
	[Fact]
	public void HaveSevenDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<CloudProviderType>();

		// Assert
		values.Length.ShouldBe(7);
		values.ShouldContain(CloudProviderType.Aws);
		values.ShouldContain(CloudProviderType.Azure);
		values.ShouldContain(CloudProviderType.Google);
		values.ShouldContain(CloudProviderType.Kafka);
		values.ShouldContain(CloudProviderType.RabbitMQ);
		values.ShouldContain(CloudProviderType.Grpc);
		values.ShouldContain(CloudProviderType.Custom);
	}

	[Fact]
	public void Aws_HasExpectedValue()
	{
		// Assert
		((int)CloudProviderType.Aws).ShouldBe(0);
	}

	[Fact]
	public void Azure_HasExpectedValue()
	{
		// Assert
		((int)CloudProviderType.Azure).ShouldBe(1);
	}

	[Fact]
	public void Google_HasExpectedValue()
	{
		// Assert
		((int)CloudProviderType.Google).ShouldBe(2);
	}

	[Fact]
	public void Kafka_HasExpectedValue()
	{
		// Assert
		((int)CloudProviderType.Kafka).ShouldBe(3);
	}

	[Fact]
	public void RabbitMQ_HasExpectedValue()
	{
		// Assert
		((int)CloudProviderType.RabbitMQ).ShouldBe(4);
	}

	[Fact]
	public void Grpc_HasExpectedValue()
	{
		// Assert
		((int)CloudProviderType.Grpc).ShouldBe(5);
	}

	[Fact]
	public void Custom_HasExpectedValue()
	{
		// Assert
		((int)CloudProviderType.Custom).ShouldBe(6);
	}

	[Fact]
	public void Aws_IsDefaultValue()
	{
		// Arrange
		CloudProviderType defaultType = default;

		// Assert
		defaultType.ShouldBe(CloudProviderType.Aws);
	}

	[Theory]
	[InlineData(CloudProviderType.Aws)]
	[InlineData(CloudProviderType.Azure)]
	[InlineData(CloudProviderType.Google)]
	[InlineData(CloudProviderType.Kafka)]
	[InlineData(CloudProviderType.RabbitMQ)]
	[InlineData(CloudProviderType.Grpc)]
	[InlineData(CloudProviderType.Custom)]
	public void BeDefinedForAllValues(CloudProviderType type)
	{
		// Assert
		Enum.IsDefined(type).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, CloudProviderType.Aws)]
	[InlineData(1, CloudProviderType.Azure)]
	[InlineData(2, CloudProviderType.Google)]
	[InlineData(3, CloudProviderType.Kafka)]
	[InlineData(4, CloudProviderType.RabbitMQ)]
	[InlineData(5, CloudProviderType.Grpc)]
	[InlineData(6, CloudProviderType.Custom)]
	public void CastFromInt_ReturnsCorrectValue(int value, CloudProviderType expected)
	{
		// Act
		var type = (CloudProviderType)value;

		// Assert
		type.ShouldBe(expected);
	}

	[Fact]
	public void Custom_IsLastValue()
	{
		// Assert - Custom should be the last value in the enum
		var maxValue = Enum.GetValues<CloudProviderType>().Max();
		maxValue.ShouldBe(CloudProviderType.Custom);
	}
}
