// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Transport;

namespace Excalibur.Dispatch.Tests.Options.Transport;

/// <summary>
/// Unit tests for <see cref="SqsOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class SqsOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_QueueUrl_IsNull()
	{
		// Arrange & Act
		var options = new SqsOptions();

		// Assert
		options.QueueUrl.ShouldBeNull();
	}

	[Fact]
	public void Default_MaxNumberOfMessages_Is10()
	{
		// Arrange & Act
		var options = new SqsOptions();

		// Assert
		options.MaxNumberOfMessages.ShouldBe(10);
	}

	[Fact]
	public void Default_VisibilityTimeout_Is30Seconds()
	{
		// Arrange & Act
		var options = new SqsOptions();

		// Assert
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Default_WaitTimeSeconds_Is20()
	{
		// Arrange & Act
		var options = new SqsOptions();

		// Assert
		options.WaitTimeSeconds.ShouldBe(20);
	}

	[Fact]
	public void Default_MaxConcurrency_Is10()
	{
		// Arrange & Act
		var options = new SqsOptions();

		// Assert
		options.MaxConcurrency.ShouldBe(10);
	}

	[Fact]
	public void Default_Region_IsUsEast1()
	{
		// Arrange & Act
		var options = new SqsOptions();

		// Assert
		options.Region.ShouldBe("us-east-1");
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void QueueUrl_CanBeSet()
	{
		// Arrange
		var options = new SqsOptions();

		// Act
		options.QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789012/my-queue");

		// Assert
		_ = options.QueueUrl.ShouldNotBeNull();
		options.QueueUrl.ToString().ShouldContain("my-queue");
	}

	[Fact]
	public void MaxNumberOfMessages_CanBeSet()
	{
		// Arrange
		var options = new SqsOptions();

		// Act
		options.MaxNumberOfMessages = 5;

		// Assert
		options.MaxNumberOfMessages.ShouldBe(5);
	}

	[Fact]
	public void VisibilityTimeout_CanBeSet()
	{
		// Arrange
		var options = new SqsOptions();

		// Act
		options.VisibilityTimeout = TimeSpan.FromMinutes(5);

		// Assert
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void WaitTimeSeconds_CanBeSet()
	{
		// Arrange
		var options = new SqsOptions();

		// Act
		options.WaitTimeSeconds = 10;

		// Assert
		options.WaitTimeSeconds.ShouldBe(10);
	}

	[Fact]
	public void MaxConcurrency_CanBeSet()
	{
		// Arrange
		var options = new SqsOptions();

		// Act
		options.MaxConcurrency = 50;

		// Assert
		options.MaxConcurrency.ShouldBe(50);
	}

	[Fact]
	public void Region_CanBeSet()
	{
		// Arrange
		var options = new SqsOptions();

		// Act
		options.Region = "eu-west-1";

		// Assert
		options.Region.ShouldBe("eu-west-1");
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new SqsOptions
		{
			QueueUrl = new Uri("https://sqs.us-west-2.amazonaws.com/123456789012/test-queue"),
			MaxNumberOfMessages = 5,
			VisibilityTimeout = TimeSpan.FromMinutes(2),
			WaitTimeSeconds = 15,
			MaxConcurrency = 20,
			Region = "us-west-2",
		};

		// Assert
		_ = options.QueueUrl.ShouldNotBeNull();
		options.MaxNumberOfMessages.ShouldBe(5);
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromMinutes(2));
		options.WaitTimeSeconds.ShouldBe(15);
		options.MaxConcurrency.ShouldBe(20);
		options.Region.ShouldBe("us-west-2");
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighThroughput_UsesMaxMessages()
	{
		// Act
		var options = new SqsOptions
		{
			MaxNumberOfMessages = 10,
			MaxConcurrency = 100,
			WaitTimeSeconds = 20,
		};

		// Assert
		options.MaxNumberOfMessages.ShouldBe(10);
		options.MaxConcurrency.ShouldBeGreaterThan(10);
	}

	[Fact]
	public void Options_ForLongRunningTasks_HasHigherVisibilityTimeout()
	{
		// Act
		var options = new SqsOptions
		{
			VisibilityTimeout = TimeSpan.FromMinutes(10),
			MaxConcurrency = 5,
		};

		// Assert
		options.VisibilityTimeout.ShouldBeGreaterThan(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void Options_ForShortPolling_HasLowWaitTime()
	{
		// Act
		var options = new SqsOptions
		{
			WaitTimeSeconds = 0,
		};

		// Assert
		options.WaitTimeSeconds.ShouldBe(0);
	}

	#endregion
}
