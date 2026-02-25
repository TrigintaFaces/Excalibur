// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.Outbox;

/// <summary>
/// Unit tests for <see cref="OutboxStatus"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Priority", "0")]
public sealed class OutboxStatusShould
{
	#region Enum Value Tests

	[Fact]
	public void Staged_HasExpectedValue()
	{
		// Assert
		((int)OutboxStatus.Staged).ShouldBe(0);
	}

	[Fact]
	public void Sending_HasExpectedValue()
	{
		// Assert
		((int)OutboxStatus.Sending).ShouldBe(1);
	}

	[Fact]
	public void Sent_HasExpectedValue()
	{
		// Assert
		((int)OutboxStatus.Sent).ShouldBe(2);
	}

	[Fact]
	public void Failed_HasExpectedValue()
	{
		// Assert
		((int)OutboxStatus.Failed).ShouldBe(3);
	}

	[Fact]
	public void PartiallyFailed_HasExpectedValue()
	{
		// Assert
		((int)OutboxStatus.PartiallyFailed).ShouldBe(4);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void ContainsAllExpectedValues()
	{
		// Arrange
		var values = Enum.GetValues<OutboxStatus>();

		// Assert
		values.ShouldContain(OutboxStatus.Staged);
		values.ShouldContain(OutboxStatus.Sending);
		values.ShouldContain(OutboxStatus.Sent);
		values.ShouldContain(OutboxStatus.Failed);
		values.ShouldContain(OutboxStatus.PartiallyFailed);
	}

	[Fact]
	public void HasExactlyFiveValues()
	{
		// Arrange
		var values = Enum.GetValues<OutboxStatus>();

		// Assert
		values.Length.ShouldBe(5);
	}

	#endregion

	#region String Conversion Tests

	[Theory]
	[InlineData(OutboxStatus.Staged, "Staged")]
	[InlineData(OutboxStatus.Sending, "Sending")]
	[InlineData(OutboxStatus.Sent, "Sent")]
	[InlineData(OutboxStatus.Failed, "Failed")]
	[InlineData(OutboxStatus.PartiallyFailed, "PartiallyFailed")]
	public void ToString_ReturnsExpectedValue(OutboxStatus status, string expected)
	{
		// Act & Assert
		status.ToString().ShouldBe(expected);
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("Staged", OutboxStatus.Staged)]
	[InlineData("Sending", OutboxStatus.Sending)]
	[InlineData("Sent", OutboxStatus.Sent)]
	[InlineData("Failed", OutboxStatus.Failed)]
	[InlineData("PartiallyFailed", OutboxStatus.PartiallyFailed)]
	public void Parse_WithValidString_ReturnsExpectedValue(string value, OutboxStatus expected)
	{
		// Act
		var result = Enum.Parse<OutboxStatus>(value);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsStaged()
	{
		// Arrange
		OutboxStatus status = default;

		// Assert
		status.ShouldBe(OutboxStatus.Staged);
	}

	#endregion
}
