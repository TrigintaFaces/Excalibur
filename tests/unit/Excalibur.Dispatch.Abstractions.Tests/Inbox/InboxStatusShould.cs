// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.Inbox;

/// <summary>
/// Unit tests for <see cref="InboxStatus"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
[Trait("Priority", "0")]
public sealed class InboxStatusShould
{
	#region Enum Value Tests

	[Fact]
	public void Received_HasExpectedValue()
	{
		// Assert
		((int)InboxStatus.Received).ShouldBe(0);
	}

	[Fact]
	public void Processing_HasExpectedValue()
	{
		// Assert
		((int)InboxStatus.Processing).ShouldBe(1);
	}

	[Fact]
	public void Processed_HasExpectedValue()
	{
		// Assert
		((int)InboxStatus.Processed).ShouldBe(2);
	}

	[Fact]
	public void Failed_HasExpectedValue()
	{
		// Assert
		((int)InboxStatus.Failed).ShouldBe(3);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void ContainsAllExpectedValues()
	{
		// Arrange
		var values = Enum.GetValues<InboxStatus>();

		// Assert
		values.ShouldContain(InboxStatus.Received);
		values.ShouldContain(InboxStatus.Processing);
		values.ShouldContain(InboxStatus.Processed);
		values.ShouldContain(InboxStatus.Failed);
	}

	[Fact]
	public void HasExactlyFourValues()
	{
		// Arrange
		var values = Enum.GetValues<InboxStatus>();

		// Assert
		values.Length.ShouldBe(4);
	}

	#endregion

	#region String Conversion Tests

	[Theory]
	[InlineData(InboxStatus.Received, "Received")]
	[InlineData(InboxStatus.Processing, "Processing")]
	[InlineData(InboxStatus.Processed, "Processed")]
	[InlineData(InboxStatus.Failed, "Failed")]
	public void ToString_ReturnsExpectedValue(InboxStatus status, string expected)
	{
		// Act & Assert
		status.ToString().ShouldBe(expected);
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("Received", InboxStatus.Received)]
	[InlineData("Processing", InboxStatus.Processing)]
	[InlineData("Processed", InboxStatus.Processed)]
	[InlineData("Failed", InboxStatus.Failed)]
	public void Parse_WithValidString_ReturnsExpectedValue(string value, InboxStatus expected)
	{
		// Act
		var result = Enum.Parse<InboxStatus>(value);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsReceived()
	{
		// Arrange
		InboxStatus status = default;

		// Assert
		status.ShouldBe(InboxStatus.Received);
	}

	#endregion
}
