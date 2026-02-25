// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Signing;

/// <summary>
/// Unit tests for <see cref="SignatureFormat"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class SignatureFormatShould
{
	[Fact]
	public void HaveBase64AsZero()
	{
		// Assert
		((int)SignatureFormat.Base64).ShouldBe(0);
	}

	[Fact]
	public void HaveHexAsOne()
	{
		// Assert
		((int)SignatureFormat.Hex).ShouldBe(1);
	}

	[Fact]
	public void HaveBinaryAsTwo()
	{
		// Assert
		((int)SignatureFormat.Binary).ShouldBe(2);
	}

	[Fact]
	public void HaveThreeDefinedValues()
	{
		// Arrange
		var values = Enum.GetValues<SignatureFormat>();

		// Assert
		values.Length.ShouldBe(3);
	}

	[Fact]
	public void DefaultToBase64()
	{
		// Arrange & Act
		var defaultValue = default(SignatureFormat);

		// Assert
		defaultValue.ShouldBe(SignatureFormat.Base64);
	}

	[Theory]
	[InlineData(SignatureFormat.Base64, "Base64")]
	[InlineData(SignatureFormat.Hex, "Hex")]
	[InlineData(SignatureFormat.Binary, "Binary")]
	public void HaveCorrectStringRepresentation(SignatureFormat format, string expected)
	{
		// Act
		var result = format.ToString();

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("Base64", SignatureFormat.Base64)]
	[InlineData("Hex", SignatureFormat.Hex)]
	[InlineData("Binary", SignatureFormat.Binary)]
	public void ParseFromString(string value, SignatureFormat expected)
	{
		// Act
		var result = Enum.Parse<SignatureFormat>(value);

		// Assert
		result.ShouldBe(expected);
	}
}
