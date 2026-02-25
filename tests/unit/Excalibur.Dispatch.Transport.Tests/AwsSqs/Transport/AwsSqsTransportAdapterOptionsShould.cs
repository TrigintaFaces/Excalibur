// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Transport;

/// <summary>
/// Unit tests for <see cref="AwsSqsTransportAdapterOptions"/> and <see cref="AwsSqsTransportAdapterOptionsExtensions"/>.
/// Part of S469.5 - Unit Tests for Transport Infrastructure (Sprint 469).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class AwsSqsTransportAdapterOptionsShould : UnitTestBase
{
	#region AwsSqsTransportAdapterOptions Tests

	[Fact]
	public void Constructor_HaveNullNameByDefault()
	{
		// Arrange & Act
		var options = new AwsSqsTransportAdapterOptions();

		// Assert
		options.Name.ShouldBeNull();
	}

	[Fact]
	public void Constructor_HaveNullRegionByDefault()
	{
		// Arrange & Act
		var options = new AwsSqsTransportAdapterOptions();

		// Assert
		options.Region.ShouldBeNull();
	}

	[Fact]
	public void Constructor_HaveNullQueuePrefixByDefault()
	{
		// Arrange & Act
		var options = new AwsSqsTransportAdapterOptions();

		// Assert
		options.QueuePrefix.ShouldBeNull();
	}

	[Fact]
	public void Name_CanBeSetAndRetrieved()
	{
		// Arrange
		var options = new AwsSqsTransportAdapterOptions();

		// Act
		options.Name = "my-transport";

		// Assert
		options.Name.ShouldBe("my-transport");
	}

	[Fact]
	public void Region_CanBeSetAndRetrieved()
	{
		// Arrange
		var options = new AwsSqsTransportAdapterOptions();

		// Act
		options.Region = "us-east-1";

		// Assert
		options.Region.ShouldBe("us-east-1");
	}

	[Fact]
	public void QueuePrefix_CanBeSetAndRetrieved()
	{
		// Arrange
		var options = new AwsSqsTransportAdapterOptions();

		// Act
		options.QueuePrefix = "orders-";

		// Assert
		options.QueuePrefix.ShouldBe("orders-");
	}

	#endregion

	#region Integration Tests - Full Configuration Scenarios

	[Fact]
	public void Options_SupportFullConfigurationScenario()
	{
		// Arrange & Act
		var options = new AwsSqsTransportAdapterOptions
		{
			Name = "orders-transport",
			Region = "us-east-1",
			QueuePrefix = "orders-",
		};

		// Assert
		options.Name.ShouldBe("orders-transport");
		options.Region.ShouldBe("us-east-1");
		options.QueuePrefix.ShouldBe("orders-");
	}

	[Fact]
	public void Options_SupportMinimalConfigurationScenario()
	{
		// Arrange & Act
		var options = new AwsSqsTransportAdapterOptions
		{
			Region = "us-west-2",
		};

		// Assert
		options.Name.ShouldBeNull();
		options.Region.ShouldBe("us-west-2");
		options.QueuePrefix.ShouldBeNull();
	}

	#endregion
}
