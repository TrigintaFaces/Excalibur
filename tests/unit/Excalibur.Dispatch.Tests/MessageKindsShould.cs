// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests;

/// <summary>
/// Unit tests for <see cref="MessageKinds"/> flags enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class MessageKindsShould
{
	[Fact]
	public void None_HasExpectedValue()
	{
		// Assert
		((int)MessageKinds.None).ShouldBe(0);
	}

	[Fact]
	public void Action_HasExpectedValue()
	{
		// Assert
		((int)MessageKinds.Action).ShouldBe(1);
	}

	[Fact]
	public void Event_HasExpectedValue()
	{
		// Assert
		((int)MessageKinds.Event).ShouldBe(2);
	}

	[Fact]
	public void Document_HasExpectedValue()
	{
		// Assert
		((int)MessageKinds.Document).ShouldBe(4);
	}

	[Fact]
	public void All_CombinesAllKinds()
	{
		// Assert
		MessageKinds.All.ShouldBe(MessageKinds.Action | MessageKinds.Event | MessageKinds.Document);
		((int)MessageKinds.All).ShouldBe(7);
	}

	[Fact]
	public void None_IsDefaultValue()
	{
		// Arrange
		MessageKinds defaultKinds = default;

		// Assert
		defaultKinds.ShouldBe(MessageKinds.None);
	}

	[Fact]
	public void HasFlagsAttribute()
	{
		// Assert
		typeof(MessageKinds).GetCustomAttributes(typeof(FlagsAttribute), false)
			.Length.ShouldBe(1);
	}

	#region Flag Combination Tests

	[Fact]
	public void CanCombineActionAndEvent()
	{
		// Arrange
		var combined = MessageKinds.Action | MessageKinds.Event;

		// Assert
		combined.HasFlag(MessageKinds.Action).ShouldBeTrue();
		combined.HasFlag(MessageKinds.Event).ShouldBeTrue();
		combined.HasFlag(MessageKinds.Document).ShouldBeFalse();
	}

	[Fact]
	public void CanCombineActionAndDocument()
	{
		// Arrange
		var combined = MessageKinds.Action | MessageKinds.Document;

		// Assert
		combined.HasFlag(MessageKinds.Action).ShouldBeTrue();
		combined.HasFlag(MessageKinds.Document).ShouldBeTrue();
		combined.HasFlag(MessageKinds.Event).ShouldBeFalse();
	}

	[Fact]
	public void CanCombineEventAndDocument()
	{
		// Arrange
		var combined = MessageKinds.Event | MessageKinds.Document;

		// Assert
		combined.HasFlag(MessageKinds.Event).ShouldBeTrue();
		combined.HasFlag(MessageKinds.Document).ShouldBeTrue();
		combined.HasFlag(MessageKinds.Action).ShouldBeFalse();
	}

	[Fact]
	public void All_ContainsAllKinds()
	{
		// Assert
		MessageKinds.All.HasFlag(MessageKinds.Action).ShouldBeTrue();
		MessageKinds.All.HasFlag(MessageKinds.Event).ShouldBeTrue();
		MessageKinds.All.HasFlag(MessageKinds.Document).ShouldBeTrue();
	}

	[Fact]
	public void All_ContainsNone()
	{
		// Assert - All should include None (0) since it's the absence of flags
		(MessageKinds.All & MessageKinds.None).ShouldBe(MessageKinds.None);
	}

	#endregion

	#region Bitwise Operation Tests

	[Theory]
	[InlineData(MessageKinds.Action, MessageKinds.All, true)]
	[InlineData(MessageKinds.Event, MessageKinds.All, true)]
	[InlineData(MessageKinds.Document, MessageKinds.All, true)]
	[InlineData(MessageKinds.Action, MessageKinds.Event, false)]
	[InlineData(MessageKinds.None, MessageKinds.All, true)]
	public void HasFlag_ReturnsExpectedResult(MessageKinds flag, MessageKinds source, bool expected)
	{
		// Act
		var result = (source & flag) == flag;

		// Assert
		result.ShouldBe(expected);
	}

	#endregion
}
