// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Scheduling;

/// <summary>
/// Unit tests for <see cref="PastScheduleBehavior"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Delivery")]
[Trait("Priority", "0")]
public sealed class PastScheduleBehaviorShould
{
	#region Value Tests

	[Fact]
	public void Reject_HasValueZero()
	{
		// Assert
		((int)PastScheduleBehavior.Reject).ShouldBe(0);
	}

	[Fact]
	public void ExecuteImmediately_HasValueOne()
	{
		// Assert
		((int)PastScheduleBehavior.ExecuteImmediately).ShouldBe(1);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void HasExpectedMemberCount()
	{
		// Arrange
		var values = Enum.GetValues<PastScheduleBehavior>();

		// Assert
		values.Length.ShouldBe(2);
	}

	[Theory]
	[InlineData(PastScheduleBehavior.Reject)]
	[InlineData(PastScheduleBehavior.ExecuteImmediately)]
	public void AllValues_AreDefined(PastScheduleBehavior behavior)
	{
		// Assert
		Enum.IsDefined(behavior).ShouldBeTrue();
	}

	#endregion

	#region String Conversion Tests

	[Fact]
	public void Reject_ToStringReturnsExpected()
	{
		// Assert
		PastScheduleBehavior.Reject.ToString().ShouldBe("Reject");
	}

	[Fact]
	public void ExecuteImmediately_ToStringReturnsExpected()
	{
		// Assert
		PastScheduleBehavior.ExecuteImmediately.ToString().ShouldBe("ExecuteImmediately");
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("Reject", PastScheduleBehavior.Reject)]
	[InlineData("ExecuteImmediately", PastScheduleBehavior.ExecuteImmediately)]
	public void Parse_ReturnsExpectedValue(string input, PastScheduleBehavior expected)
	{
		// Act
		var result = Enum.Parse<PastScheduleBehavior>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void Parse_WithIgnoreCase_Succeeds()
	{
		// Act
		var result = Enum.Parse<PastScheduleBehavior>("executeimmediately", ignoreCase: true);

		// Assert
		result.ShouldBe(PastScheduleBehavior.ExecuteImmediately);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsReject()
	{
		// Arrange
		var defaultValue = default(PastScheduleBehavior);

		// Assert
		defaultValue.ShouldBe(PastScheduleBehavior.Reject);
	}

	#endregion
}
