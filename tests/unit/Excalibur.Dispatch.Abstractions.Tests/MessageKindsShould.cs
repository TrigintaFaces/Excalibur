// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Unit tests for <see cref="MessageKinds"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class MessageKindsShould : UnitTestBase
{
	#region Value Tests

	[Fact]
	public void HaveNoneAsZero()
	{
		// Assert
		((int)MessageKinds.None).ShouldBe(0);
	}

	[Fact]
	public void HaveActionAsBitFlag()
	{
		// Assert
		((int)MessageKinds.Action).ShouldBe(1);
	}

	[Fact]
	public void HaveEventAsBitFlag()
	{
		// Assert
		((int)MessageKinds.Event).ShouldBe(2);
	}

	[Fact]
	public void HaveDocumentAsBitFlag()
	{
		// Assert
		((int)MessageKinds.Document).ShouldBe(4);
	}

	[Fact]
	public void HaveAllAsCombinationOfAllFlags()
	{
		// Assert
		MessageKinds.All.ShouldBe(MessageKinds.Action | MessageKinds.Event | MessageKinds.Document);
		((int)MessageKinds.All).ShouldBe(7); // 1 + 2 + 4
	}

	#endregion Value Tests

	#region Flags Attribute Tests

	[Fact]
	public void BeFlagsEnum()
	{
		// Act
		var hasFlagsAttribute = typeof(MessageKinds).GetCustomAttributes(typeof(FlagsAttribute), false).Any();

		// Assert
		hasFlagsAttribute.ShouldBeTrue();
	}

	[Fact]
	public void SupportBitwiseOr()
	{
		// Arrange & Act
		var combined = MessageKinds.Action | MessageKinds.Event;

		// Assert
		combined.HasFlag(MessageKinds.Action).ShouldBeTrue();
		combined.HasFlag(MessageKinds.Event).ShouldBeTrue();
		combined.HasFlag(MessageKinds.Document).ShouldBeFalse();
	}

	[Fact]
	public void SupportBitwiseAnd()
	{
		// Arrange
		var combined = MessageKinds.Action | MessageKinds.Event | MessageKinds.Document;

		// Act
		var result = combined & MessageKinds.Event;

		// Assert
		result.ShouldBe(MessageKinds.Event);
	}

	[Fact]
	public void SupportBitwiseXor()
	{
		// Arrange
		var combined = MessageKinds.Action | MessageKinds.Event;

		// Act - Toggle Action off
		var result = combined ^ MessageKinds.Action;

		// Assert
		result.ShouldBe(MessageKinds.Event);
	}

	[Fact]
	public void SupportBitwiseNot()
	{
		// Arrange
		var combined = MessageKinds.All;

		// Act - Remove Event
		var result = combined & ~MessageKinds.Event;

		// Assert
		result.HasFlag(MessageKinds.Action).ShouldBeTrue();
		result.HasFlag(MessageKinds.Event).ShouldBeFalse();
		result.HasFlag(MessageKinds.Document).ShouldBeTrue();
	}

	#endregion Flags Attribute Tests

	#region HasFlag Tests

	[Theory]
	[InlineData(MessageKinds.All, MessageKinds.Action, true)]
	[InlineData(MessageKinds.All, MessageKinds.Event, true)]
	[InlineData(MessageKinds.All, MessageKinds.Document, true)]
	[InlineData(MessageKinds.All, MessageKinds.None, true)]
	[InlineData(MessageKinds.Action, MessageKinds.Event, false)]
	[InlineData(MessageKinds.Event, MessageKinds.Action, false)]
	[InlineData(MessageKinds.None, MessageKinds.Action, false)]
	public void CorrectlyCheckHasFlag(MessageKinds value, MessageKinds flag, bool expected)
	{
		// Act
		var result = value.HasFlag(flag);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void CheckMultipleFlagsAtOnce()
	{
		// Arrange
		var combined = MessageKinds.Action | MessageKinds.Event;

		// Act & Assert
		combined.HasFlag(MessageKinds.Action | MessageKinds.Event).ShouldBeTrue();
		combined.HasFlag(MessageKinds.Action | MessageKinds.Document).ShouldBeFalse();
	}

	#endregion HasFlag Tests

	#region Default Value Tests

	[Fact]
	public void DefaultToNone()
	{
		// Arrange
		MessageKinds defaultValue = default;

		// Assert
		defaultValue.ShouldBe(MessageKinds.None);
	}

	[Fact]
	public void BeNoneWhenCastFromZero()
	{
		// Act
		var result = (MessageKinds)0;

		// Assert
		result.ShouldBe(MessageKinds.None);
	}

	#endregion Default Value Tests

	#region Parse Tests

	[Theory]
	[InlineData("None", MessageKinds.None)]
	[InlineData("Action", MessageKinds.Action)]
	[InlineData("Event", MessageKinds.Event)]
	[InlineData("Document", MessageKinds.Document)]
	[InlineData("All", MessageKinds.All)]
	[InlineData("Action, Event", MessageKinds.Action | MessageKinds.Event)]
	public void ParseFromString(string input, MessageKinds expected)
	{
		// Act
		var result = Enum.Parse<MessageKinds>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void ParseCombinedFlagsFromString()
	{
		// Act
		var result = Enum.Parse<MessageKinds>("Action, Document");

		// Assert
		result.ShouldBe(MessageKinds.Action | MessageKinds.Document);
	}

	#endregion Parse Tests

	#region ToString Tests

	[Theory]
	[InlineData(MessageKinds.None, "None")]
	[InlineData(MessageKinds.Action, "Action")]
	[InlineData(MessageKinds.Event, "Event")]
	[InlineData(MessageKinds.Document, "Document")]
	[InlineData(MessageKinds.All, "All")]
	public void ConvertToString(MessageKinds value, string expected)
	{
		// Act
		var result = value.ToString();

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void ToStringCombinedFlags()
	{
		// Arrange
		var combined = MessageKinds.Action | MessageKinds.Event;

		// Act
		var result = combined.ToString();

		// Assert
		result.ShouldBe("Action, Event");
	}

	#endregion ToString Tests

	#region Use Case Scenario Tests

	[Fact]
	public void FilterMiddlewareForActionsOnly()
	{
		// Arrange - Middleware applies only to Actions
		var applicableTo = MessageKinds.Action;
		var messageKind = MessageKinds.Action;

		// Act
		var applies = applicableTo.HasFlag(messageKind);

		// Assert
		applies.ShouldBeTrue();
	}

	[Fact]
	public void FilterMiddlewareForEventsOnly()
	{
		// Arrange - Middleware applies only to Events
		var applicableTo = MessageKinds.Event;
		var messageKind = MessageKinds.Action;

		// Act
		var applies = applicableTo.HasFlag(messageKind);

		// Assert
		applies.ShouldBeFalse();
	}

	[Fact]
	public void FilterMiddlewareForAllMessageTypes()
	{
		// Arrange - Middleware applies to all types
		var applicableTo = MessageKinds.All;

		// Act & Assert - Should apply to any message kind
		applicableTo.HasFlag(MessageKinds.Action).ShouldBeTrue();
		applicableTo.HasFlag(MessageKinds.Event).ShouldBeTrue();
		applicableTo.HasFlag(MessageKinds.Document).ShouldBeTrue();
	}

	[Fact]
	public void FilterMiddlewareForActionsAndEvents()
	{
		// Arrange - Middleware applies to Actions and Events but not Documents
		var applicableTo = MessageKinds.Action | MessageKinds.Event;

		// Assert
		applicableTo.HasFlag(MessageKinds.Action).ShouldBeTrue();
		applicableTo.HasFlag(MessageKinds.Event).ShouldBeTrue();
		applicableTo.HasFlag(MessageKinds.Document).ShouldBeFalse();
	}

	[Fact]
	public void SupportExclusionPatterns()
	{
		// Arrange - All except Documents
		var applicableTo = MessageKinds.All & ~MessageKinds.Document;

		// Assert
		applicableTo.HasFlag(MessageKinds.Action).ShouldBeTrue();
		applicableTo.HasFlag(MessageKinds.Event).ShouldBeTrue();
		applicableTo.HasFlag(MessageKinds.Document).ShouldBeFalse();
	}

	#endregion Use Case Scenario Tests

	#region Enum Completeness Tests

	[Fact]
	public void HaveExactlyFiveValues()
	{
		// Act
		var values = Enum.GetValues<MessageKinds>();

		// Assert - None, Action, Event, Document, All
		values.Length.ShouldBe(5);
	}

	[Fact]
	public void HaveUniqueValuesForSingleFlags()
	{
		// Arrange - Only single-bit flags (excluding None and All)
		var singleFlags = new[] { MessageKinds.Action, MessageKinds.Event, MessageKinds.Document };

		// Act
		var values = singleFlags.Select(f => (int)f).ToArray();

		// Assert - All should be distinct powers of 2
		values.ShouldBeUnique();
		values.All(v => (v & (v - 1)) == 0 && v > 0).ShouldBeTrue(); // All are powers of 2
	}

	#endregion Enum Completeness Tests
}
