// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Unit tests for <see cref="DispatchFeatures"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Features")]
[Trait("Priority", "0")]
public sealed class DispatchFeaturesShould
{
	#region Enum Value Tests

	[Fact]
	public void None_HasExpectedValue()
	{
		// Assert
		((int)DispatchFeatures.None).ShouldBe(0);
	}

	[Fact]
	public void Inbox_HasExpectedValue()
	{
		// Assert
		((int)DispatchFeatures.Inbox).ShouldBe(1);
	}

	[Fact]
	public void Outbox_HasExpectedValue()
	{
		// Assert
		((int)DispatchFeatures.Outbox).ShouldBe(2);
	}

	[Fact]
	public void Tracing_HasExpectedValue()
	{
		// Assert
		((int)DispatchFeatures.Tracing).ShouldBe(4);
	}

	[Fact]
	public void Metrics_HasExpectedValue()
	{
		// Assert
		((int)DispatchFeatures.Metrics).ShouldBe(8);
	}

	[Fact]
	public void Validation_HasExpectedValue()
	{
		// Assert
		((int)DispatchFeatures.Validation).ShouldBe(16);
	}

	[Fact]
	public void Authorization_HasExpectedValue()
	{
		// Assert
		((int)DispatchFeatures.Authorization).ShouldBe(32);
	}

	[Fact]
	public void Transactions_HasExpectedValue()
	{
		// Assert
		((int)DispatchFeatures.Transactions).ShouldBe(64);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void ContainsAllExpectedValues()
	{
		// Arrange
		var values = Enum.GetValues<DispatchFeatures>();

		// Assert
		values.ShouldContain(DispatchFeatures.None);
		values.ShouldContain(DispatchFeatures.Inbox);
		values.ShouldContain(DispatchFeatures.Outbox);
		values.ShouldContain(DispatchFeatures.Tracing);
		values.ShouldContain(DispatchFeatures.Metrics);
		values.ShouldContain(DispatchFeatures.Validation);
		values.ShouldContain(DispatchFeatures.Authorization);
		values.ShouldContain(DispatchFeatures.Transactions);
	}

	[Fact]
	public void HasExactlyEightValues()
	{
		// Arrange
		var values = Enum.GetValues<DispatchFeatures>();

		// Assert
		values.Length.ShouldBe(8);
	}

	#endregion

	#region Flags Attribute Tests

	[Fact]
	public void HasFlagsAttribute()
	{
		// Assert
		typeof(DispatchFeatures).IsDefined(typeof(FlagsAttribute), false).ShouldBeTrue();
	}

	[Fact]
	public void CanCombineInboxAndOutbox()
	{
		// Arrange
		var combined = DispatchFeatures.Inbox | DispatchFeatures.Outbox;

		// Assert
		combined.HasFlag(DispatchFeatures.Inbox).ShouldBeTrue();
		combined.HasFlag(DispatchFeatures.Outbox).ShouldBeTrue();
		combined.HasFlag(DispatchFeatures.Tracing).ShouldBeFalse();
	}

	[Fact]
	public void CanCombineMultipleFlags()
	{
		// Arrange
		var combined = DispatchFeatures.Inbox | DispatchFeatures.Outbox | DispatchFeatures.Validation | DispatchFeatures.Authorization;

		// Assert
		combined.HasFlag(DispatchFeatures.Inbox).ShouldBeTrue();
		combined.HasFlag(DispatchFeatures.Outbox).ShouldBeTrue();
		combined.HasFlag(DispatchFeatures.Validation).ShouldBeTrue();
		combined.HasFlag(DispatchFeatures.Authorization).ShouldBeTrue();
		combined.HasFlag(DispatchFeatures.Tracing).ShouldBeFalse();
		combined.HasFlag(DispatchFeatures.Metrics).ShouldBeFalse();
	}

	[Fact]
	public void CombinedValue_IsCorrect()
	{
		// Arrange
		var combined = DispatchFeatures.Inbox | DispatchFeatures.Outbox;

		// Assert
		((int)combined).ShouldBe(3);
	}

	[Fact]
	public void AllFeatures_CanBeCombined()
	{
		// Arrange
		var allFeatures = DispatchFeatures.Inbox | DispatchFeatures.Outbox |
						  DispatchFeatures.Tracing | DispatchFeatures.Metrics |
						  DispatchFeatures.Validation | DispatchFeatures.Authorization |
						  DispatchFeatures.Transactions;

		// Assert
		((int)allFeatures).ShouldBe(127);
	}

	#endregion

	#region String Conversion Tests

	[Theory]
	[InlineData(DispatchFeatures.None, "None")]
	[InlineData(DispatchFeatures.Inbox, "Inbox")]
	[InlineData(DispatchFeatures.Outbox, "Outbox")]
	[InlineData(DispatchFeatures.Tracing, "Tracing")]
	[InlineData(DispatchFeatures.Metrics, "Metrics")]
	[InlineData(DispatchFeatures.Validation, "Validation")]
	[InlineData(DispatchFeatures.Authorization, "Authorization")]
	[InlineData(DispatchFeatures.Transactions, "Transactions")]
	public void ToString_ReturnsExpectedValue(DispatchFeatures feature, string expected)
	{
		// Act & Assert
		feature.ToString().ShouldBe(expected);
	}

	[Fact]
	public void CombinedFlags_ToString_ReturnsCommaSeparatedNames()
	{
		// Arrange
		var combined = DispatchFeatures.Inbox | DispatchFeatures.Outbox;

		// Act
		var result = combined.ToString();

		// Assert
		result.ShouldContain("Inbox");
		result.ShouldContain("Outbox");
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("None", DispatchFeatures.None)]
	[InlineData("Inbox", DispatchFeatures.Inbox)]
	[InlineData("Outbox", DispatchFeatures.Outbox)]
	[InlineData("Authorization", DispatchFeatures.Authorization)]
	public void Parse_WithValidString_ReturnsExpectedValue(string value, DispatchFeatures expected)
	{
		// Act
		var result = Enum.Parse<DispatchFeatures>(value);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void Parse_CombinedFlags_ReturnsCorrectValue()
	{
		// Act
		var result = Enum.Parse<DispatchFeatures>("Inbox, Outbox");

		// Assert
		result.HasFlag(DispatchFeatures.Inbox).ShouldBeTrue();
		result.HasFlag(DispatchFeatures.Outbox).ShouldBeTrue();
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsNone()
	{
		// Arrange
		DispatchFeatures features = default;

		// Assert
		features.ShouldBe(DispatchFeatures.None);
	}

	#endregion
}
