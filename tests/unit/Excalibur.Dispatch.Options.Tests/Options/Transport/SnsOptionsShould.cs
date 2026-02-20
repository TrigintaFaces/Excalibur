// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Transport;

namespace Excalibur.Dispatch.Tests.Options.Transport;

/// <summary>
/// Unit tests for <see cref="SnsOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class SnsOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_TopicArn_IsEmpty()
	{
		// Arrange & Act
		var options = new SnsOptions();

		// Assert
		options.TopicArn.ShouldBe(string.Empty);
	}

	[Fact]
	public void Default_Region_IsUsEast1()
	{
		// Arrange & Act
		var options = new SnsOptions();

		// Assert
		options.Region.ShouldBe("us-east-1");
	}

	[Fact]
	public void Default_EnableDeduplication_IsFalse()
	{
		// Arrange & Act
		var options = new SnsOptions();

		// Assert
		options.EnableDeduplication.ShouldBeFalse();
	}

	[Fact]
	public void Default_UseFifo_IsFalse()
	{
		// Arrange & Act
		var options = new SnsOptions();

		// Assert
		options.UseFifo.ShouldBeFalse();
	}

	[Fact]
	public void Default_MessageGroupId_IsDefault()
	{
		// Arrange & Act
		var options = new SnsOptions();

		// Assert
		options.MessageGroupId.ShouldBe("default");
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void TopicArn_CanBeSet()
	{
		// Arrange
		var options = new SnsOptions();

		// Act
		options.TopicArn = "arn:aws:sns:us-east-1:123456789012:my-topic";

		// Assert
		options.TopicArn.ShouldContain("my-topic");
	}

	[Fact]
	public void Region_CanBeSet()
	{
		// Arrange
		var options = new SnsOptions();

		// Act
		options.Region = "eu-west-1";

		// Assert
		options.Region.ShouldBe("eu-west-1");
	}

	[Fact]
	public void EnableDeduplication_CanBeSet()
	{
		// Arrange
		var options = new SnsOptions();

		// Act
		options.EnableDeduplication = true;

		// Assert
		options.EnableDeduplication.ShouldBeTrue();
	}

	[Fact]
	public void UseFifo_CanBeSet()
	{
		// Arrange
		var options = new SnsOptions();

		// Act
		options.UseFifo = true;

		// Assert
		options.UseFifo.ShouldBeTrue();
	}

	[Fact]
	public void MessageGroupId_CanBeSet()
	{
		// Arrange
		var options = new SnsOptions();

		// Act
		options.MessageGroupId = "orders-group";

		// Assert
		options.MessageGroupId.ShouldBe("orders-group");
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new SnsOptions
		{
			TopicArn = "arn:aws:sns:us-west-2:123456789012:test-topic.fifo",
			Region = "us-west-2",
			EnableDeduplication = true,
			UseFifo = true,
			MessageGroupId = "test-group",
		};

		// Assert
		options.TopicArn.ShouldContain("test-topic");
		options.Region.ShouldBe("us-west-2");
		options.EnableDeduplication.ShouldBeTrue();
		options.UseFifo.ShouldBeTrue();
		options.MessageGroupId.ShouldBe("test-group");
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForFifoTopic_EnablesFifoAndDeduplication()
	{
		// Act
		var options = new SnsOptions
		{
			TopicArn = "arn:aws:sns:us-east-1:123456789012:my-topic.fifo",
			UseFifo = true,
			EnableDeduplication = true,
			MessageGroupId = "orders",
		};

		// Assert
		options.UseFifo.ShouldBeTrue();
		options.EnableDeduplication.ShouldBeTrue();
		options.TopicArn.ShouldContain(".fifo");
	}

	[Fact]
	public void Options_ForStandardTopic_DisablesFifo()
	{
		// Act
		var options = new SnsOptions
		{
			TopicArn = "arn:aws:sns:us-east-1:123456789012:my-topic",
			UseFifo = false,
			EnableDeduplication = false,
		};

		// Assert
		options.UseFifo.ShouldBeFalse();
		options.EnableDeduplication.ShouldBeFalse();
	}

	[Fact]
	public void Options_ForEuropeRegion_SetsCorrectRegion()
	{
		// Act
		var options = new SnsOptions
		{
			Region = "eu-central-1",
		};

		// Assert
		options.Region.ShouldBe("eu-central-1");
	}

	#endregion
}
