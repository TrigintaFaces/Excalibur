// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Outbox.Tests.Core;

/// <summary>
/// Unit tests for <see cref="OutboxMessageType"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxMessageTypeShould : UnitTestBase
{
	#region Enum Value Tests

	[Fact]
	public void HaveUnknownAsDefaultValue()
	{
		// Assert
		((int)OutboxMessageType.Unknown).ShouldBe(0);
	}

	[Fact]
	public void HaveCommandValue()
	{
		// Assert
		((int)OutboxMessageType.Command).ShouldBe(1);
	}

	[Fact]
	public void HaveEventValue()
	{
		// Assert
		((int)OutboxMessageType.Event).ShouldBe(2);
	}

	[Fact]
	public void HaveDocumentValue()
	{
		// Assert
		((int)OutboxMessageType.Document).ShouldBe(3);
	}

	[Fact]
	public void HaveScheduledValue()
	{
		// Assert
		((int)OutboxMessageType.Scheduled).ShouldBe(4);
	}

	#endregion Enum Value Tests

	#region Default Initialization Tests

	[Fact]
	public void DefaultToUnknownWhenUninitialized()
	{
		// Arrange
		OutboxMessageType type = default;

		// Assert
		type.ShouldBe(OutboxMessageType.Unknown);
	}

	[Fact]
	public void BeUnknownWhenCastFromZero()
	{
		// Arrange & Act
		var type = (OutboxMessageType)0;

		// Assert
		type.ShouldBe(OutboxMessageType.Unknown);
	}

	#endregion Default Initialization Tests

	#region Parse Tests

	[Theory]
	[InlineData("Unknown", OutboxMessageType.Unknown)]
	[InlineData("Command", OutboxMessageType.Command)]
	[InlineData("Event", OutboxMessageType.Event)]
	[InlineData("Document", OutboxMessageType.Document)]
	[InlineData("Scheduled", OutboxMessageType.Scheduled)]
	public void ParseFromString(string input, OutboxMessageType expected)
	{
		// Act
		var result = Enum.Parse<OutboxMessageType>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void ParseCaseInsensitively()
	{
		// Act
		var result = Enum.Parse<OutboxMessageType>("command", ignoreCase: true);

		// Assert
		result.ShouldBe(OutboxMessageType.Command);
	}

	#endregion Parse Tests

	#region TryParse Tests

	[Theory]
	[InlineData("Unknown", true, OutboxMessageType.Unknown)]
	[InlineData("Command", true, OutboxMessageType.Command)]
	[InlineData("Invalid", false, OutboxMessageType.Unknown)]
	[InlineData("", false, OutboxMessageType.Unknown)]
	public void TryParseFromString(string input, bool expectedSuccess, OutboxMessageType expectedValue)
	{
		// Act
		var success = Enum.TryParse<OutboxMessageType>(input, out var result);

		// Assert
		success.ShouldBe(expectedSuccess);
		if (expectedSuccess)
		{
			result.ShouldBe(expectedValue);
		}
	}

	#endregion TryParse Tests

	#region IsDefined Tests

	[Theory]
	[InlineData(OutboxMessageType.Unknown, true)]
	[InlineData(OutboxMessageType.Command, true)]
	[InlineData(OutboxMessageType.Event, true)]
	[InlineData(OutboxMessageType.Document, true)]
	[InlineData(OutboxMessageType.Scheduled, true)]
	[InlineData((OutboxMessageType)100, false)]
	[InlineData((OutboxMessageType)(-1), false)]
	public void ValidateIsDefined(OutboxMessageType value, bool expected)
	{
		// Act
		var isDefined = Enum.IsDefined(value);

		// Assert
		isDefined.ShouldBe(expected);
	}

	#endregion IsDefined Tests

	#region ToString Tests

	[Theory]
	[InlineData(OutboxMessageType.Unknown, "Unknown")]
	[InlineData(OutboxMessageType.Command, "Command")]
	[InlineData(OutboxMessageType.Event, "Event")]
	[InlineData(OutboxMessageType.Document, "Document")]
	[InlineData(OutboxMessageType.Scheduled, "Scheduled")]
	public void ConvertToString(OutboxMessageType value, string expected)
	{
		// Act
		var result = value.ToString();

		// Assert
		result.ShouldBe(expected);
	}

	#endregion ToString Tests

	#region GetValues Tests

	[Fact]
	public void HaveExactlyFiveValues()
	{
		// Act
		var values = Enum.GetValues<OutboxMessageType>();

		// Assert
		values.Length.ShouldBe(5);
	}

	[Fact]
	public void ContainAllExpectedValues()
	{
		// Act
		var values = Enum.GetValues<OutboxMessageType>();

		// Assert
		values.ShouldContain(OutboxMessageType.Unknown);
		values.ShouldContain(OutboxMessageType.Command);
		values.ShouldContain(OutboxMessageType.Event);
		values.ShouldContain(OutboxMessageType.Document);
		values.ShouldContain(OutboxMessageType.Scheduled);
	}

	#endregion GetValues Tests

	#region Use Case Scenario Tests

	[Fact]
	public void DistinguishCommandFromEvent()
	{
		// Arrange
		var command = OutboxMessageType.Command;
		var evt = OutboxMessageType.Event;

		// Assert
		command.ShouldNotBe(evt);
	}

	[Fact]
	public void SupportSwitchExpressions()
	{
		// Arrange
		var type = OutboxMessageType.Command;

		// Act
		var description = type switch
		{
			OutboxMessageType.Unknown => "Unknown",
			OutboxMessageType.Command => "Action request",
			OutboxMessageType.Event => "State change notification",
			OutboxMessageType.Document => "Data payload",
			OutboxMessageType.Scheduled => "Time-based",
			_ => "Unsupported",
		};

		// Assert
		description.ShouldBe("Action request");
	}

	[Theory]
	[InlineData(OutboxMessageType.Command, true)]
	[InlineData(OutboxMessageType.Event, true)]
	[InlineData(OutboxMessageType.Document, true)]
	[InlineData(OutboxMessageType.Scheduled, true)]
	[InlineData(OutboxMessageType.Unknown, false)]
	public void IdentifyKnownMessageTypes(OutboxMessageType type, bool isKnown)
	{
		// Act
		var result = type != OutboxMessageType.Unknown;

		// Assert
		result.ShouldBe(isKnown);
	}

	#endregion Use Case Scenario Tests
}
