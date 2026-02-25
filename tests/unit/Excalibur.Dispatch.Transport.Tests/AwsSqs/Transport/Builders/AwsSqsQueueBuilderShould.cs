// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Transport.Builders;

/// <summary>
/// Unit tests for <see cref="IAwsSqsQueueBuilder"/> fluent builder pattern.
/// Part of S470.5 - Unit Tests for Fluent Builders (Sprint 470).
/// </summary>
/// <remarks>
/// Tests verify the fluent builder behavior by configuring options through the builder
/// interface and checking the resulting options state.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class AwsSqsQueueBuilderShould : UnitTestBase
{
	#region VisibilityTimeout Tests

	[Fact]
	public void VisibilityTimeout_SetOptionValue()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();
		IAwsSqsQueueBuilder builder = CreateBuilder(options);
		var timeout = TimeSpan.FromMinutes(5);

		// Act
		_ = builder.VisibilityTimeout(timeout);

		// Assert
		options.VisibilityTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void VisibilityTimeout_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();
		IAwsSqsQueueBuilder builder = CreateBuilder(options);

		// Act
		var result = builder.VisibilityTimeout(TimeSpan.FromMinutes(5));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void VisibilityTimeout_SupportFluentChaining()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();
		IAwsSqsQueueBuilder builder = CreateBuilder(options);

		// Act
		_ = builder
			.VisibilityTimeout(TimeSpan.FromMinutes(5))
			.MessageRetentionPeriod(TimeSpan.FromDays(7));

		// Assert
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromMinutes(5));
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(7));
	}

	#endregion

	#region MessageRetentionPeriod Tests

	[Fact]
	public void MessageRetentionPeriod_SetOptionValue()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();
		IAwsSqsQueueBuilder builder = CreateBuilder(options);
		var period = TimeSpan.FromDays(7);

		// Act
		_ = builder.MessageRetentionPeriod(period);

		// Assert
		options.MessageRetentionPeriod.ShouldBe(period);
	}

	[Fact]
	public void MessageRetentionPeriod_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();
		IAwsSqsQueueBuilder builder = CreateBuilder(options);

		// Act
		var result = builder.MessageRetentionPeriod(TimeSpan.FromDays(7));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region ReceiveWaitTimeSeconds Tests

	[Fact]
	public void ReceiveWaitTimeSeconds_SetOptionValue()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();
		IAwsSqsQueueBuilder builder = CreateBuilder(options);

		// Act
		_ = builder.ReceiveWaitTimeSeconds(20);

		// Assert
		options.ReceiveWaitTimeSeconds.ShouldBe(20);
	}

	[Fact]
	public void ReceiveWaitTimeSeconds_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();
		IAwsSqsQueueBuilder builder = CreateBuilder(options);

		// Act
		var result = builder.ReceiveWaitTimeSeconds(20);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region DelaySeconds Tests

	[Fact]
	public void DelaySeconds_SetOptionValue()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();
		IAwsSqsQueueBuilder builder = CreateBuilder(options);

		// Act
		_ = builder.DelaySeconds(300);

		// Assert
		options.DelaySeconds.ShouldBe(300);
	}

	[Fact]
	public void DelaySeconds_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();
		IAwsSqsQueueBuilder builder = CreateBuilder(options);

		// Act
		var result = builder.DelaySeconds(300);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region DeadLetterQueue Tests

	[Fact]
	public void DeadLetterQueue_CreateDeadLetterOptions()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();
		IAwsSqsQueueBuilder builder = CreateBuilder(options);

		// Act
		_ = builder.DeadLetterQueue(_ => { });

		// Assert
		_ = options.DeadLetterQueue.ShouldNotBeNull();
		options.HasDeadLetterQueue.ShouldBeTrue();
	}

	[Fact]
	public void DeadLetterQueue_ConfigureDlqOptions()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();
		IAwsSqsQueueBuilder builder = CreateBuilder(options);
		const string dlqArn = "arn:aws:sqs:us-east-1:123456789012:my-dlq";

		// Act
		_ = builder.DeadLetterQueue(dlq =>
		{
			_ = dlq.QueueArn(dlqArn);
			_ = dlq.MaxReceiveCount(3);
		});

		// Assert
		options.DeadLetterQueue.QueueArn.ShouldBe(dlqArn);
		options.DeadLetterQueue.MaxReceiveCount.ShouldBe(3);
	}

	[Fact]
	public void DeadLetterQueue_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();
		IAwsSqsQueueBuilder builder = CreateBuilder(options);

		// Act
		var result = builder.DeadLetterQueue(_ => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void DeadLetterQueue_ThrowWhenConfigureIsNull()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();
		IAwsSqsQueueBuilder builder = CreateBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.DeadLetterQueue(null!));
	}

	#endregion

	#region Full Configuration Tests

	[Fact]
	public void Builder_SupportFullConfigurationChain()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();
		IAwsSqsQueueBuilder builder = CreateBuilder(options);

		// Act
		_ = builder
			.VisibilityTimeout(TimeSpan.FromMinutes(5))
			.MessageRetentionPeriod(TimeSpan.FromDays(7))
			.ReceiveWaitTimeSeconds(20)
			.DelaySeconds(60)
			.DeadLetterQueue(dlq =>
			{
				_ = dlq.QueueArn("arn:aws:sqs:us-east-1:123456789012:my-dlq")
				   .MaxReceiveCount(5);
			});

		// Assert
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromMinutes(5));
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(7));
		options.ReceiveWaitTimeSeconds.ShouldBe(20);
		options.DelaySeconds.ShouldBe(60);
		options.HasDeadLetterQueue.ShouldBeTrue();
		options.DeadLetterQueue.QueueArn.ShouldBe("arn:aws:sqs:us-east-1:123456789012:my-dlq");
		options.DeadLetterQueue.MaxReceiveCount.ShouldBe(5);
	}

	[Fact]
	public void Builder_AllowMultipleDlqConfigurations()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();
		IAwsSqsQueueBuilder builder = CreateBuilder(options);

		// Act - Configure twice, last configuration wins
		_ = builder
			.DeadLetterQueue(dlq => dlq.MaxReceiveCount(3))
			.DeadLetterQueue(dlq => dlq.MaxReceiveCount(10));

		// Assert
		options.DeadLetterQueue.MaxReceiveCount.ShouldBe(10);
	}

	#endregion

	/// <summary>
	/// Creates a builder instance for testing.
	/// Uses reflection to instantiate the internal AwsSqsQueueBuilder.
	/// </summary>
	private static IAwsSqsQueueBuilder CreateBuilder(AwsSqsQueueOptions options)
	{
		// The internal builder is package-visible, so we use reflection
		var builderType = typeof(AwsSqsQueueOptions).Assembly.GetType("Excalibur.Dispatch.Transport.Aws.AwsSqsQueueBuilder");
		_ = builderType.ShouldNotBeNull("AwsSqsQueueBuilder type should exist");

		var builder = Activator.CreateInstance(builderType, options);
		_ = builder.ShouldNotBeNull("Builder instance should be created");

		return (IAwsSqsQueueBuilder)builder;
	}
}
