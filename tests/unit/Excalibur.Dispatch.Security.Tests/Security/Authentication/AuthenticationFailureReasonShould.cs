// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Authentication;

/// <summary>
/// Unit tests for <see cref="AuthenticationFailureReason"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class AuthenticationFailureReasonShould
{
	[Fact]
	public void HaveMissingTokenAsZero()
	{
		// Assert
		((int)AuthenticationFailureReason.MissingToken).ShouldBe(0);
	}

	[Fact]
	public void HaveInvalidTokenAsOne()
	{
		// Assert
		((int)AuthenticationFailureReason.InvalidToken).ShouldBe(1);
	}

	[Fact]
	public void HaveTokenExpiredAsTwo()
	{
		// Assert
		((int)AuthenticationFailureReason.TokenExpired).ShouldBe(2);
	}

	[Fact]
	public void HaveValidationErrorAsThree()
	{
		// Assert
		((int)AuthenticationFailureReason.ValidationError).ShouldBe(3);
	}

	[Fact]
	public void HaveUnknownErrorAsFour()
	{
		// Assert
		((int)AuthenticationFailureReason.UnknownError).ShouldBe(4);
	}

	[Fact]
	public void HaveFiveDefinedValues()
	{
		// Arrange
		var values = Enum.GetValues<AuthenticationFailureReason>();

		// Assert
		values.Length.ShouldBe(5);
	}

	[Fact]
	public void DefaultToMissingToken()
	{
		// Arrange & Act
		var defaultValue = default(AuthenticationFailureReason);

		// Assert
		defaultValue.ShouldBe(AuthenticationFailureReason.MissingToken);
	}

	[Theory]
	[InlineData(AuthenticationFailureReason.MissingToken, "MissingToken")]
	[InlineData(AuthenticationFailureReason.InvalidToken, "InvalidToken")]
	[InlineData(AuthenticationFailureReason.TokenExpired, "TokenExpired")]
	[InlineData(AuthenticationFailureReason.ValidationError, "ValidationError")]
	[InlineData(AuthenticationFailureReason.UnknownError, "UnknownError")]
	public void HaveCorrectStringRepresentation(AuthenticationFailureReason reason, string expected)
	{
		// Act
		var result = reason.ToString();

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("MissingToken", AuthenticationFailureReason.MissingToken)]
	[InlineData("InvalidToken", AuthenticationFailureReason.InvalidToken)]
	[InlineData("TokenExpired", AuthenticationFailureReason.TokenExpired)]
	[InlineData("ValidationError", AuthenticationFailureReason.ValidationError)]
	[InlineData("UnknownError", AuthenticationFailureReason.UnknownError)]
	public void ParseFromString(string value, AuthenticationFailureReason expected)
	{
		// Act
		var result = Enum.Parse<AuthenticationFailureReason>(value);

		// Assert
		result.ShouldBe(expected);
	}
}
