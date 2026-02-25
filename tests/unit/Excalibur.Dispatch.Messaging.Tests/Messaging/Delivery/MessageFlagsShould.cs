// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

/// <summary>
/// Unit tests for <see cref="MessageFlags"/> flags enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class MessageFlagsShould
{
	[Fact]
	public void HaveSixDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<MessageFlags>();

		// Assert
		values.Length.ShouldBe(6);
		values.ShouldContain(MessageFlags.None);
		values.ShouldContain(MessageFlags.Compressed);
		values.ShouldContain(MessageFlags.Encrypted);
		values.ShouldContain(MessageFlags.Persistent);
		values.ShouldContain(MessageFlags.HighPriority);
		values.ShouldContain(MessageFlags.Validated);
	}

	[Fact]
	public void None_HasExpectedValue()
	{
		// Assert
		((byte)MessageFlags.None).ShouldBe((byte)0);
	}

	[Fact]
	public void Compressed_HasExpectedValue()
	{
		// Assert
		((byte)MessageFlags.Compressed).ShouldBe((byte)1);
	}

	[Fact]
	public void Encrypted_HasExpectedValue()
	{
		// Assert
		((byte)MessageFlags.Encrypted).ShouldBe((byte)2);
	}

	[Fact]
	public void Persistent_HasExpectedValue()
	{
		// Assert
		((byte)MessageFlags.Persistent).ShouldBe((byte)4);
	}

	[Fact]
	public void HighPriority_HasExpectedValue()
	{
		// Assert
		((byte)MessageFlags.HighPriority).ShouldBe((byte)8);
	}

	[Fact]
	public void Validated_HasExpectedValue()
	{
		// Assert
		((byte)MessageFlags.Validated).ShouldBe((byte)16);
	}

	[Fact]
	public void None_IsDefaultValue()
	{
		// Arrange
		MessageFlags defaultFlags = default;

		// Assert
		defaultFlags.ShouldBe(MessageFlags.None);
	}

	[Fact]
	public void SupportCombiningFlags()
	{
		// Arrange
		var combined = MessageFlags.Compressed | MessageFlags.Encrypted;

		// Assert
		combined.HasFlag(MessageFlags.Compressed).ShouldBeTrue();
		combined.HasFlag(MessageFlags.Encrypted).ShouldBeTrue();
		combined.HasFlag(MessageFlags.Persistent).ShouldBeFalse();
	}

	[Fact]
	public void SupportAllFlagsCombined()
	{
		// Arrange
		var allFlags = MessageFlags.Compressed | MessageFlags.Encrypted |
					   MessageFlags.Persistent | MessageFlags.HighPriority | MessageFlags.Validated;

		// Assert
		allFlags.HasFlag(MessageFlags.Compressed).ShouldBeTrue();
		allFlags.HasFlag(MessageFlags.Encrypted).ShouldBeTrue();
		allFlags.HasFlag(MessageFlags.Persistent).ShouldBeTrue();
		allFlags.HasFlag(MessageFlags.HighPriority).ShouldBeTrue();
		allFlags.HasFlag(MessageFlags.Validated).ShouldBeTrue();
	}

	[Fact]
	public void SupportRemovingFlags()
	{
		// Arrange
		var flags = MessageFlags.Compressed | MessageFlags.Encrypted;

		// Act
		var result = flags & ~MessageFlags.Compressed;

		// Assert
		result.HasFlag(MessageFlags.Compressed).ShouldBeFalse();
		result.HasFlag(MessageFlags.Encrypted).ShouldBeTrue();
	}

	[Fact]
	public void SupportTogglingFlags()
	{
		// Arrange
		var flags = MessageFlags.Compressed;

		// Act
		var toggled = flags ^ MessageFlags.Encrypted;

		// Assert
		toggled.HasFlag(MessageFlags.Compressed).ShouldBeTrue();
		toggled.HasFlag(MessageFlags.Encrypted).ShouldBeTrue();
	}

	[Theory]
	[InlineData(MessageFlags.None)]
	[InlineData(MessageFlags.Compressed)]
	[InlineData(MessageFlags.Encrypted)]
	[InlineData(MessageFlags.Persistent)]
	[InlineData(MessageFlags.HighPriority)]
	[InlineData(MessageFlags.Validated)]
	public void BeDefinedForAllValues(MessageFlags flag)
	{
		// Assert
		Enum.IsDefined(flag).ShouldBeTrue();
	}

	[Fact]
	public void HaveUniqueBitPositions()
	{
		// Assert - Each flag should have a unique power of 2 value
		var compressed = (byte)MessageFlags.Compressed;
		var encrypted = (byte)MessageFlags.Encrypted;
		var persistent = (byte)MessageFlags.Persistent;
		var highPriority = (byte)MessageFlags.HighPriority;
		var validated = (byte)MessageFlags.Validated;

		// Verify all are powers of 2
		(compressed & (compressed - 1)).ShouldBe((byte)0);
		(encrypted & (encrypted - 1)).ShouldBe((byte)0);
		(persistent & (persistent - 1)).ShouldBe((byte)0);
		(highPriority & (highPriority - 1)).ShouldBe((byte)0);
		(validated & (validated - 1)).ShouldBe((byte)0);

		// Verify no overlap
		(compressed & encrypted).ShouldBe((byte)0);
		(compressed & persistent).ShouldBe((byte)0);
		(compressed & highPriority).ShouldBe((byte)0);
		(compressed & validated).ShouldBe((byte)0);
		(encrypted & persistent).ShouldBe((byte)0);
		(encrypted & highPriority).ShouldBe((byte)0);
		(encrypted & validated).ShouldBe((byte)0);
		(persistent & highPriority).ShouldBe((byte)0);
		(persistent & validated).ShouldBe((byte)0);
		(highPriority & validated).ShouldBe((byte)0);
	}
}
