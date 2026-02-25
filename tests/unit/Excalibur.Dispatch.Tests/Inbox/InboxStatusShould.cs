// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests.Inbox;

/// <summary>
/// Unit tests for <see cref="InboxStatus"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class InboxStatusShould
{
	[Fact]
	public void HaveFourDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<InboxStatus>();

		// Assert
		values.Length.ShouldBe(4);
		values.ShouldContain(InboxStatus.Received);
		values.ShouldContain(InboxStatus.Processing);
		values.ShouldContain(InboxStatus.Processed);
		values.ShouldContain(InboxStatus.Failed);
	}

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

	[Fact]
	public void Received_IsDefaultValue()
	{
		// Arrange
		InboxStatus defaultStatus = default;

		// Assert
		defaultStatus.ShouldBe(InboxStatus.Received);
	}

	[Theory]
	[InlineData(InboxStatus.Received)]
	[InlineData(InboxStatus.Processing)]
	[InlineData(InboxStatus.Processed)]
	[InlineData(InboxStatus.Failed)]
	public void BeDefinedForAllValues(InboxStatus status)
	{
		// Assert
		Enum.IsDefined(status).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, InboxStatus.Received)]
	[InlineData(1, InboxStatus.Processing)]
	[InlineData(2, InboxStatus.Processed)]
	[InlineData(3, InboxStatus.Failed)]
	public void CastFromInt_ReturnsCorrectValue(int value, InboxStatus expected)
	{
		// Act
		var status = (InboxStatus)value;

		// Assert
		status.ShouldBe(expected);
	}
}
