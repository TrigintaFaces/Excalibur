// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Transport.Builders;

/// <summary>
/// Unit tests for <see cref="IAwsSqsDeadLetterBuilder"/> fluent builder pattern.
/// Part of S470.5 - Unit Tests for Fluent Builders (Sprint 470).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class AwsSqsDeadLetterBuilderShould : UnitTestBase
{
	private const string ValidQueueArn = "arn:aws:sqs:us-east-1:123456789012:my-dlq";

	#region QueueArn Tests

	[Fact]
	public void QueueArn_SetOptionValue()
	{
		// Arrange
		var options = new AwsSqsDeadLetterOptions();
		IAwsSqsDeadLetterBuilder builder = CreateBuilder(options);

		// Act
		_ = builder.QueueArn(ValidQueueArn);

		// Assert
		options.QueueArn.ShouldBe(ValidQueueArn);
	}

	[Fact]
	public void QueueArn_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AwsSqsDeadLetterOptions();
		IAwsSqsDeadLetterBuilder builder = CreateBuilder(options);

		// Act
		var result = builder.QueueArn(ValidQueueArn);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void QueueArn_ThrowWhenArnIsNull()
	{
		// Arrange
		var options = new AwsSqsDeadLetterOptions();
		IAwsSqsDeadLetterBuilder builder = CreateBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			builder.QueueArn(null!));
	}

	[Fact]
	public void QueueArn_ThrowWhenArnIsEmpty()
	{
		// Arrange
		var options = new AwsSqsDeadLetterOptions();
		IAwsSqsDeadLetterBuilder builder = CreateBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			builder.QueueArn(""));
	}

	[Fact]
	public void QueueArn_ThrowWhenArnIsWhitespace()
	{
		// Arrange
		var options = new AwsSqsDeadLetterOptions();
		IAwsSqsDeadLetterBuilder builder = CreateBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			builder.QueueArn("   "));
	}

	#endregion

	#region MaxReceiveCount Tests

	[Fact]
	public void MaxReceiveCount_SetOptionValue()
	{
		// Arrange
		var options = new AwsSqsDeadLetterOptions();
		IAwsSqsDeadLetterBuilder builder = CreateBuilder(options);

		// Act
		_ = builder.MaxReceiveCount(10);

		// Assert
		options.MaxReceiveCount.ShouldBe(10);
	}

	[Fact]
	public void MaxReceiveCount_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AwsSqsDeadLetterOptions();
		IAwsSqsDeadLetterBuilder builder = CreateBuilder(options);

		// Act
		var result = builder.MaxReceiveCount(5);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Full Configuration Tests

	[Fact]
	public void Builder_SupportFluentChaining()
	{
		// Arrange
		var options = new AwsSqsDeadLetterOptions();
		IAwsSqsDeadLetterBuilder builder = CreateBuilder(options);

		// Act
		_ = builder
			.QueueArn(ValidQueueArn)
			.MaxReceiveCount(3);

		// Assert
		options.QueueArn.ShouldBe(ValidQueueArn);
		options.MaxReceiveCount.ShouldBe(3);
	}

	[Fact]
	public void Builder_AllowOverwritingValues()
	{
		// Arrange
		var options = new AwsSqsDeadLetterOptions();
		IAwsSqsDeadLetterBuilder builder = CreateBuilder(options);

		// Act
		_ = builder
			.MaxReceiveCount(5)
			.MaxReceiveCount(10);

		// Assert
		options.MaxReceiveCount.ShouldBe(10);
	}

	#endregion

	/// <summary>
	/// Creates a builder instance for testing.
	/// Uses reflection to instantiate the internal AwsSqsDeadLetterBuilder.
	/// </summary>
	private static IAwsSqsDeadLetterBuilder CreateBuilder(AwsSqsDeadLetterOptions options)
	{
		var builderType = typeof(AwsSqsDeadLetterOptions).Assembly.GetType("Excalibur.Dispatch.Transport.Aws.AwsSqsDeadLetterBuilder");
		_ = builderType.ShouldNotBeNull("AwsSqsDeadLetterBuilder type should exist");

		var builder = Activator.CreateInstance(builderType, options);
		_ = builder.ShouldNotBeNull("Builder instance should be created");

		return (IAwsSqsDeadLetterBuilder)builder;
	}
}
