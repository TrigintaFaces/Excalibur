// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Transport.Builders;

/// <summary>
/// Unit tests for <see cref="IAwsSqsBatchBuilder"/> fluent builder pattern.
/// Part of S470.5 - Unit Tests for Fluent Builders (Sprint 470).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class AwsSqsBatchBuilderShould : UnitTestBase
{
	#region SendBatchSize Tests

	[Fact]
	public void SendBatchSize_SetOptionValue()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();
		IAwsSqsBatchBuilder builder = CreateBuilder(options);

		// Act
		_ = builder.SendBatchSize(5);

		// Assert
		options.SendBatchSize.ShouldBe(5);
	}

	[Fact]
	public void SendBatchSize_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();
		IAwsSqsBatchBuilder builder = CreateBuilder(options);

		// Act
		var result = builder.SendBatchSize(5);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region SendBatchWindow Tests

	[Fact]
	public void SendBatchWindow_SetOptionValue()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();
		IAwsSqsBatchBuilder builder = CreateBuilder(options);
		var window = TimeSpan.FromMilliseconds(200);

		// Act
		_ = builder.SendBatchWindow(window);

		// Assert
		options.SendBatchWindow.ShouldBe(window);
	}

	[Fact]
	public void SendBatchWindow_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();
		IAwsSqsBatchBuilder builder = CreateBuilder(options);

		// Act
		var result = builder.SendBatchWindow(TimeSpan.FromMilliseconds(100));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void SendBatchWindow_AcceptZeroForImmediateSending()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();
		IAwsSqsBatchBuilder builder = CreateBuilder(options);

		// Act
		_ = builder.SendBatchWindow(TimeSpan.Zero);

		// Assert
		options.SendBatchWindow.ShouldBe(TimeSpan.Zero);
	}

	#endregion

	#region ReceiveMaxMessages Tests

	[Fact]
	public void ReceiveMaxMessages_SetOptionValue()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();
		IAwsSqsBatchBuilder builder = CreateBuilder(options);

		// Act
		_ = builder.ReceiveMaxMessages(5);

		// Assert
		options.ReceiveMaxMessages.ShouldBe(5);
	}

	[Fact]
	public void ReceiveMaxMessages_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();
		IAwsSqsBatchBuilder builder = CreateBuilder(options);

		// Act
		var result = builder.ReceiveMaxMessages(10);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Full Configuration Tests

	[Fact]
	public void Builder_SupportFullConfigurationChain()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();
		IAwsSqsBatchBuilder builder = CreateBuilder(options);

		// Act
		_ = builder
			.SendBatchSize(8)
			.SendBatchWindow(TimeSpan.FromMilliseconds(250))
			.ReceiveMaxMessages(6);

		// Assert
		options.SendBatchSize.ShouldBe(8);
		options.SendBatchWindow.ShouldBe(TimeSpan.FromMilliseconds(250));
		options.ReceiveMaxMessages.ShouldBe(6);
	}

	[Fact]
	public void Builder_SupportHighThroughputConfiguration()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();
		IAwsSqsBatchBuilder builder = CreateBuilder(options);

		// Act - Max batch sizes with batching enabled
		_ = builder
			.SendBatchSize(10)
			.SendBatchWindow(TimeSpan.FromSeconds(1))
			.ReceiveMaxMessages(10);

		// Assert
		options.SendBatchSize.ShouldBe(10);
		options.SendBatchWindow.ShouldBe(TimeSpan.FromSeconds(1));
		options.ReceiveMaxMessages.ShouldBe(10);
	}

	[Fact]
	public void Builder_SupportLowLatencyConfiguration()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();
		IAwsSqsBatchBuilder builder = CreateBuilder(options);

		// Act - No batching for lowest latency
		_ = builder
			.SendBatchSize(1)
			.SendBatchWindow(TimeSpan.Zero)
			.ReceiveMaxMessages(1);

		// Assert
		options.SendBatchSize.ShouldBe(1);
		options.SendBatchWindow.ShouldBe(TimeSpan.Zero);
		options.ReceiveMaxMessages.ShouldBe(1);
	}

	[Fact]
	public void Builder_AllowOverwritingValues()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();
		IAwsSqsBatchBuilder builder = CreateBuilder(options);

		// Act
		_ = builder
			.SendBatchSize(5)
			.SendBatchSize(10);

		// Assert
		options.SendBatchSize.ShouldBe(10);
	}

	#endregion

	/// <summary>
	/// Creates a builder instance for testing.
	/// Uses reflection to instantiate the internal AwsSqsBatchBuilder.
	/// </summary>
	private static IAwsSqsBatchBuilder CreateBuilder(AwsSqsBatchOptions options)
	{
		var builderType = typeof(AwsSqsBatchOptions).Assembly.GetType("Excalibur.Dispatch.Transport.Aws.AwsSqsBatchBuilder");
		_ = builderType.ShouldNotBeNull("AwsSqsBatchBuilder type should exist");

		var builder = Activator.CreateInstance(builderType, options);
		_ = builder.ShouldNotBeNull("Builder instance should be created");

		return (IAwsSqsBatchBuilder)builder;
	}
}
