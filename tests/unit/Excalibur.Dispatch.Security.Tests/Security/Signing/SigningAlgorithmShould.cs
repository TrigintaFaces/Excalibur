// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Signing;

/// <summary>
/// Unit tests for <see cref="SigningAlgorithm"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class SigningAlgorithmShould
{
	[Fact]
	public void HaveUnknownAsZero()
	{
		// Assert
		((int)SigningAlgorithm.Unknown).ShouldBe(0);
	}

	[Fact]
	public void HaveHMACSHA256AsOne()
	{
		// Assert
		((int)SigningAlgorithm.HMACSHA256).ShouldBe(1);
	}

	[Fact]
	public void HaveHMACSHA512AsTwo()
	{
		// Assert
		((int)SigningAlgorithm.HMACSHA512).ShouldBe(2);
	}

	[Fact]
	public void HaveRSASHA256AsThree()
	{
		// Assert
		((int)SigningAlgorithm.RSASHA256).ShouldBe(3);
	}

	[Fact]
	public void HaveRSAPSSSHA256AsFour()
	{
		// Assert
		((int)SigningAlgorithm.RSAPSSSHA256).ShouldBe(4);
	}

	[Fact]
	public void HaveECDSASHA256AsFive()
	{
		// Assert
		((int)SigningAlgorithm.ECDSASHA256).ShouldBe(5);
	}

	[Fact]
	public void HaveEd25519AsSix()
	{
		// Assert
		((int)SigningAlgorithm.Ed25519).ShouldBe(6);
	}

	[Fact]
	public void HaveSevenDefinedValues()
	{
		// Arrange
		var values = Enum.GetValues<SigningAlgorithm>();

		// Assert
		values.Length.ShouldBe(7);
	}

	[Fact]
	public void DefaultToUnknown()
	{
		// Arrange & Act
		var defaultValue = default(SigningAlgorithm);

		// Assert
		defaultValue.ShouldBe(SigningAlgorithm.Unknown);
	}

	[Theory]
	[InlineData(SigningAlgorithm.Unknown, "Unknown")]
	[InlineData(SigningAlgorithm.HMACSHA256, "HMACSHA256")]
	[InlineData(SigningAlgorithm.HMACSHA512, "HMACSHA512")]
	[InlineData(SigningAlgorithm.RSASHA256, "RSASHA256")]
	[InlineData(SigningAlgorithm.RSAPSSSHA256, "RSAPSSSHA256")]
	[InlineData(SigningAlgorithm.ECDSASHA256, "ECDSASHA256")]
	[InlineData(SigningAlgorithm.Ed25519, "Ed25519")]
	public void HaveCorrectStringRepresentation(SigningAlgorithm algorithm, string expected)
	{
		// Act
		var result = algorithm.ToString();

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("Unknown", SigningAlgorithm.Unknown)]
	[InlineData("HMACSHA256", SigningAlgorithm.HMACSHA256)]
	[InlineData("HMACSHA512", SigningAlgorithm.HMACSHA512)]
	[InlineData("RSASHA256", SigningAlgorithm.RSASHA256)]
	[InlineData("RSAPSSSHA256", SigningAlgorithm.RSAPSSSHA256)]
	[InlineData("ECDSASHA256", SigningAlgorithm.ECDSASHA256)]
	[InlineData("Ed25519", SigningAlgorithm.Ed25519)]
	public void ParseFromString(string value, SigningAlgorithm expected)
	{
		// Act
		var result = Enum.Parse<SigningAlgorithm>(value);

		// Assert
		result.ShouldBe(expected);
	}
}
