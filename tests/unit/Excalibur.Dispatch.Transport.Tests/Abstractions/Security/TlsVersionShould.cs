// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Security;

/// <summary>
/// Unit tests for <see cref="TlsVersion"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class TlsVersionShould
{
	[Fact]
	public void HaveFourDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<TlsVersion>();

		// Assert
		values.Length.ShouldBe(4);
		values.ShouldContain(TlsVersion.Tls10);
		values.ShouldContain(TlsVersion.Tls11);
		values.ShouldContain(TlsVersion.Tls12);
		values.ShouldContain(TlsVersion.Tls13);
	}

	[Fact]
	public void Tls10_HasExpectedValue()
	{
		// Assert
		((int)TlsVersion.Tls10).ShouldBe(0);
	}

	[Fact]
	public void Tls11_HasExpectedValue()
	{
		// Assert
		((int)TlsVersion.Tls11).ShouldBe(1);
	}

	[Fact]
	public void Tls12_HasExpectedValue()
	{
		// Assert
		((int)TlsVersion.Tls12).ShouldBe(2);
	}

	[Fact]
	public void Tls13_HasExpectedValue()
	{
		// Assert
		((int)TlsVersion.Tls13).ShouldBe(3);
	}

	[Fact]
	public void Tls10_IsDefaultValue()
	{
		// Arrange
		TlsVersion defaultVersion = default;

		// Assert
		defaultVersion.ShouldBe(TlsVersion.Tls10);
	}

	[Theory]
	[InlineData(TlsVersion.Tls10)]
	[InlineData(TlsVersion.Tls11)]
	[InlineData(TlsVersion.Tls12)]
	[InlineData(TlsVersion.Tls13)]
	public void BeDefinedForAllValues(TlsVersion version)
	{
		// Assert
		Enum.IsDefined(version).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, TlsVersion.Tls10)]
	[InlineData(1, TlsVersion.Tls11)]
	[InlineData(2, TlsVersion.Tls12)]
	[InlineData(3, TlsVersion.Tls13)]
	public void CastFromInt_ReturnsCorrectValue(int value, TlsVersion expected)
	{
		// Act
		var version = (TlsVersion)value;

		// Assert
		version.ShouldBe(expected);
	}

	[Fact]
	public void HaveVersionsInIncreasingOrder()
	{
		// Assert - versions should increase with security
		((int)TlsVersion.Tls10).ShouldBeLessThan((int)TlsVersion.Tls11);
		((int)TlsVersion.Tls11).ShouldBeLessThan((int)TlsVersion.Tls12);
		((int)TlsVersion.Tls12).ShouldBeLessThan((int)TlsVersion.Tls13);
	}

	[Fact]
	public void Tls13_IsLatestVersion()
	{
		// Assert
		var maxValue = Enum.GetValues<TlsVersion>().Max();
		maxValue.ShouldBe(TlsVersion.Tls13);
	}

	[Fact]
	public void Tls12_IsMinimumRecommendedVersion()
	{
		// Assert - TLS 1.2 is the minimum recommended for compliance
		((int)TlsVersion.Tls12).ShouldBeGreaterThan((int)TlsVersion.Tls11);
	}
}
