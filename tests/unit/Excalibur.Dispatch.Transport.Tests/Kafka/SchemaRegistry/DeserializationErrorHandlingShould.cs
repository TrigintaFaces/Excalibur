// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry;

/// <summary>
/// Unit tests for the <see cref="DeserializationErrorHandling"/> enum.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.3): Schema Registry unit tests.
/// Tests verify enum values for deserialization error handling strategies.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Kafka")]
[Trait("Feature", "SchemaRegistry")]
public sealed class DeserializationErrorHandlingShould
{
	#region Enum Value Tests

	[Fact]
	public void DefineSkipAsZero()
	{
		// Assert
		((int)DeserializationErrorHandling.Skip).ShouldBe(0);
	}

	[Fact]
	public void DefineDeadLetterAsOne()
	{
		// Assert
		((int)DeserializationErrorHandling.DeadLetter).ShouldBe(1);
	}

	[Fact]
	public void DefineThrowAsTwo()
	{
		// Assert
		((int)DeserializationErrorHandling.Throw).ShouldBe(2);
	}

	#endregion

	#region Enum Count Tests

	[Fact]
	public void HaveThreeDefinedValues()
	{
		// Act
		var values = Enum.GetValues<DeserializationErrorHandling>();

		// Assert
		values.Length.ShouldBe(3);
	}

	[Fact]
	public void ContainAllExpectedModes()
	{
		// Act
		var values = Enum.GetValues<DeserializationErrorHandling>();

		// Assert
		values.ShouldContain(DeserializationErrorHandling.Skip);
		values.ShouldContain(DeserializationErrorHandling.DeadLetter);
		values.ShouldContain(DeserializationErrorHandling.Throw);
	}

	#endregion

	#region Enum Parse Tests

	[Theory]
	[InlineData("Skip", DeserializationErrorHandling.Skip)]
	[InlineData("DeadLetter", DeserializationErrorHandling.DeadLetter)]
	[InlineData("Throw", DeserializationErrorHandling.Throw)]
	public void ParseFromString_WithValidName(string name, DeserializationErrorHandling expected)
	{
		// Act
		var result = Enum.Parse<DeserializationErrorHandling>(name);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("skip", DeserializationErrorHandling.Skip)]
	[InlineData("DEADLETTER", DeserializationErrorHandling.DeadLetter)]
	[InlineData("throw", DeserializationErrorHandling.Throw)]
	public void ParseFromString_WithCaseInsensitiveMatch(string name, DeserializationErrorHandling expected)
	{
		// Act
		var result = Enum.Parse<DeserializationErrorHandling>(name, ignoreCase: true);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Enum Name Tests

	[Fact]
	public void HaveCorrectNameForSkip()
	{
		// Assert
		DeserializationErrorHandling.Skip.ToString().ShouldBe("Skip");
	}

	[Fact]
	public void HaveCorrectNameForDeadLetter()
	{
		// Assert
		DeserializationErrorHandling.DeadLetter.ToString().ShouldBe("DeadLetter");
	}

	[Fact]
	public void HaveCorrectNameForThrow()
	{
		// Assert
		DeserializationErrorHandling.Throw.ToString().ShouldBe("Throw");
	}

	#endregion

	#region Strictness Ordering Tests

	[Fact]
	public void SkipShouldBeLeastStrict()
	{
		// Assert - Skip = 0 is the least strict (fire-and-forget)
		DeserializationErrorHandling.Skip.ShouldBe(Enum.GetValues<DeserializationErrorHandling>().Min());
	}

	[Fact]
	public void ThrowShouldBeMostStrict()
	{
		// Assert - Throw = 2 is the most strict (stops consumption)
		DeserializationErrorHandling.Throw.ShouldBe(Enum.GetValues<DeserializationErrorHandling>().Max());
	}

	[Fact]
	public void HaveStrictnessValuesInIncreasingOrder()
	{
		// Assert - Verify strictness values increase (Skip < DeadLetter < Throw)
		((int)DeserializationErrorHandling.Skip).ShouldBeLessThan((int)DeserializationErrorHandling.DeadLetter);
		((int)DeserializationErrorHandling.DeadLetter).ShouldBeLessThan((int)DeserializationErrorHandling.Throw);
	}

	#endregion
}
