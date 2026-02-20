// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests.Timing;

/// <summary>
/// Unit tests for <see cref="TimeoutContext"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Timing")]
[Trait("Priority", "0")]
public sealed class TimeoutContextShould
{
	#region Default Values Tests

	[Fact]
	public void Default_ComplexityIsNormal()
	{
		// Arrange & Act
		var context = new TimeoutContext();

		// Assert
		context.Complexity.ShouldBe(OperationComplexity.Normal);
	}

	[Fact]
	public void Default_MessageTypeIsNull()
	{
		// Arrange & Act
		var context = new TimeoutContext();

		// Assert
		context.MessageType.ShouldBeNull();
	}

	[Fact]
	public void Default_HandlerTypeIsNull()
	{
		// Arrange & Act
		var context = new TimeoutContext();

		// Assert
		context.HandlerType.ShouldBeNull();
	}

	[Fact]
	public void Default_PropertiesIsEmpty()
	{
		// Arrange & Act
		var context = new TimeoutContext();

		// Assert
		_ = context.Properties.ShouldNotBeNull();
		context.Properties.ShouldBeEmpty();
	}

	[Fact]
	public void Default_ExpectedMessageSizeBytesIsNull()
	{
		// Arrange & Act
		var context = new TimeoutContext();

		// Assert
		context.ExpectedMessageSizeBytes.ShouldBeNull();
	}

	[Fact]
	public void Default_IsRetryIsFalse()
	{
		// Arrange & Act
		var context = new TimeoutContext();

		// Assert
		context.IsRetry.ShouldBeFalse();
	}

	[Fact]
	public void Default_RetryCountIsZero()
	{
		// Arrange & Act
		var context = new TimeoutContext();

		// Assert
		context.RetryCount.ShouldBe(0);
	}

	[Fact]
	public void Default_TagsIsEmpty()
	{
		// Arrange & Act
		var context = new TimeoutContext();

		// Assert
		_ = context.Tags.ShouldNotBeNull();
		context.Tags.ShouldBeEmpty();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Complexity_CanBeSet()
	{
		// Arrange
		var context = new TimeoutContext();

		// Act
		context.Complexity = OperationComplexity.Heavy;

		// Assert
		context.Complexity.ShouldBe(OperationComplexity.Heavy);
	}

	[Fact]
	public void MessageType_CanBeSet()
	{
		// Arrange
		var context = new TimeoutContext();

		// Act
		context.MessageType = typeof(string);

		// Assert
		context.MessageType.ShouldBe(typeof(string));
	}

	[Fact]
	public void HandlerType_CanBeSet()
	{
		// Arrange
		var context = new TimeoutContext();

		// Act
		context.HandlerType = typeof(TimeoutContextShould);

		// Assert
		context.HandlerType.ShouldBe(typeof(TimeoutContextShould));
	}

	[Fact]
	public void ExpectedMessageSizeBytes_CanBeSet()
	{
		// Arrange
		var context = new TimeoutContext();

		// Act
		context.ExpectedMessageSizeBytes = 1024;

		// Assert
		context.ExpectedMessageSizeBytes.ShouldBe(1024);
	}

	[Fact]
	public void IsRetry_CanBeSet()
	{
		// Arrange
		var context = new TimeoutContext();

		// Act
		context.IsRetry = true;

		// Assert
		context.IsRetry.ShouldBeTrue();
	}

	[Fact]
	public void RetryCount_CanBeSet()
	{
		// Arrange
		var context = new TimeoutContext();

		// Act
		context.RetryCount = 3;

		// Assert
		context.RetryCount.ShouldBe(3);
	}

	[Fact]
	public void Properties_CanAddEntry()
	{
		// Arrange
		var context = new TimeoutContext();

		// Act
		context.Properties["custom"] = "value";

		// Assert
		context.Properties.ShouldContainKeyAndValue("custom", "value");
	}

	[Fact]
	public void Tags_CanAddEntry()
	{
		// Arrange
		var context = new TimeoutContext();

		// Act
		_ = context.Tags.Add("batch");
		_ = context.Tags.Add("high-priority");

		// Assert
		context.Tags.ShouldContain("batch");
		context.Tags.ShouldContain("high-priority");
	}

	#endregion

	#region ForMessage Factory Tests

	[Fact]
	public void ForMessage_SetsMessageType()
	{
		// Act
		var context = TimeoutContext.ForMessage(typeof(string));

		// Assert
		context.MessageType.ShouldBe(typeof(string));
	}

	[Fact]
	public void ForMessage_SetsDefaultComplexity()
	{
		// Act
		var context = TimeoutContext.ForMessage(typeof(string));

		// Assert
		context.Complexity.ShouldBe(OperationComplexity.Normal);
	}

	[Fact]
	public void ForMessage_LeavesHandlerTypeNull()
	{
		// Act
		var context = TimeoutContext.ForMessage(typeof(string));

		// Assert
		context.HandlerType.ShouldBeNull();
	}

	#endregion

	#region ForHandler Factory Tests

	[Fact]
	public void ForHandler_SetsHandlerType()
	{
		// Act
		var context = TimeoutContext.ForHandler(typeof(TimeoutContextShould));

		// Assert
		context.HandlerType.ShouldBe(typeof(TimeoutContextShould));
	}

	[Fact]
	public void ForHandler_SetsDefaultComplexity()
	{
		// Act
		var context = TimeoutContext.ForHandler(typeof(TimeoutContextShould));

		// Assert
		context.Complexity.ShouldBe(OperationComplexity.Normal);
	}

	[Fact]
	public void ForHandler_LeavesMessageTypeNull()
	{
		// Act
		var context = TimeoutContext.ForHandler(typeof(TimeoutContextShould));

		// Assert
		context.MessageType.ShouldBeNull();
	}

	#endregion

	#region WithComplexity Factory Tests

	[Theory]
	[InlineData(OperationComplexity.Simple)]
	[InlineData(OperationComplexity.Normal)]
	[InlineData(OperationComplexity.Complex)]
	[InlineData(OperationComplexity.Heavy)]
	public void WithComplexity_SetsComplexity(OperationComplexity complexity)
	{
		// Act
		var context = TimeoutContext.WithComplexity(complexity);

		// Assert
		context.Complexity.ShouldBe(complexity);
	}

	[Fact]
	public void WithComplexity_LeavesOtherPropertiesDefault()
	{
		// Act
		var context = TimeoutContext.WithComplexity(OperationComplexity.Complex);

		// Assert
		context.MessageType.ShouldBeNull();
		context.HandlerType.ShouldBeNull();
		context.IsRetry.ShouldBeFalse();
	}

	#endregion

	#region ForRetry Factory Tests

	[Fact]
	public void ForRetry_SetsIsRetryToTrue()
	{
		// Act
		var context = TimeoutContext.ForRetry(1);

		// Assert
		context.IsRetry.ShouldBeTrue();
	}

	[Fact]
	public void ForRetry_SetsRetryCount()
	{
		// Act
		var context = TimeoutContext.ForRetry(3);

		// Assert
		context.RetryCount.ShouldBe(3);
	}

	[Fact]
	public void ForRetry_WithZeroRetryCount_SetsZero()
	{
		// Act
		var context = TimeoutContext.ForRetry(0);

		// Assert
		context.RetryCount.ShouldBe(0);
		context.IsRetry.ShouldBeTrue();
	}

	[Fact]
	public void ForRetry_SetsDefaultComplexity()
	{
		// Act
		var context = TimeoutContext.ForRetry(1);

		// Assert
		context.Complexity.ShouldBe(OperationComplexity.Normal);
	}

	#endregion
}
