// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

namespace Excalibur.Data.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
public sealed class ByteArrayEqualityComparerShould
{
	private readonly ByteArrayEqualityComparer _sut = new();

	[Fact]
	public void ReturnTrueForEqualArrays()
	{
		// Arrange
		byte[] a = [1, 2, 3];
		byte[] b = [1, 2, 3];

		// Act & Assert
		_sut.Equals(a, b).ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForDifferentArrays()
	{
		// Arrange
		byte[] a = [1, 2, 3];
		byte[] b = [1, 2, 4];

		// Act & Assert
		_sut.Equals(a, b).ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalseForDifferentLengthArrays()
	{
		// Arrange
		byte[] a = [1, 2, 3];
		byte[] b = [1, 2];

		// Act & Assert
		_sut.Equals(a, b).ShouldBeFalse();
	}

	[Fact]
	public void ReturnTrueForBothNull()
	{
		_sut.Equals(null, null).ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseWhenOneIsNull()
	{
		byte[] a = [1, 2, 3];

		_sut.Equals(a, null).ShouldBeFalse();
		_sut.Equals(null, a).ShouldBeFalse();
	}

	[Fact]
	public void ProduceConsistentHashCodes()
	{
		// Arrange
		byte[] a = [1, 2, 3];
		byte[] b = [1, 2, 3];

		// Act & Assert
		_sut.GetHashCode(a).ShouldBe(_sut.GetHashCode(b));
	}

	[Fact]
	public void ProduceDifferentHashCodesForDifferentArrays()
	{
		// Arrange
		byte[] a = [1, 2, 3];
		byte[] b = [4, 5, 6];

		// Act & Assert - not guaranteed but highly likely
		_sut.GetHashCode(a).ShouldNotBe(_sut.GetHashCode(b));
	}

	[Fact]
	public void ThrowWhenGetHashCodeArgumentIsNull()
	{
		Should.Throw<ArgumentNullException>(() => _sut.GetHashCode((byte[])null!));
	}

	[Fact]
	public void SupportObjectOverloadEquals()
	{
		// Arrange
		object a = new byte[] { 1, 2, 3 };
		object b = new byte[] { 1, 2, 3 };

		// Act & Assert
		((System.Collections.IEqualityComparer)_sut).Equals(a, b).ShouldBeTrue();
	}

	[Fact]
	public void ReturnTrueForObjectOverloadBothNull()
	{
		((System.Collections.IEqualityComparer)_sut).Equals(null, null).ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForObjectOverloadOneNull()
	{
		object a = new byte[] { 1, 2, 3 };

		((System.Collections.IEqualityComparer)_sut).Equals(a, null).ShouldBeFalse();
		((System.Collections.IEqualityComparer)_sut).Equals(null, a).ShouldBeFalse();
	}

	[Fact]
	public void ThrowForObjectOverloadNonByteArray()
	{
		Should.Throw<ArgumentException>(() =>
			((System.Collections.IEqualityComparer)_sut).Equals("not", "bytes"));
	}

	[Fact]
	public void SupportObjectOverloadGetHashCode()
	{
		// Arrange
		object a = new byte[] { 1, 2, 3 };

		// Act & Assert - should not throw
		((System.Collections.IEqualityComparer)_sut).GetHashCode(a).ShouldBeGreaterThan(0);
	}

	[Fact]
	public void ReturnZeroForObjectOverloadGetHashCodeNull()
	{
		((System.Collections.IEqualityComparer)_sut).GetHashCode(null!).ShouldBe(0);
	}

	[Fact]
	public void ThrowForObjectOverloadGetHashCodeNonByteArray()
	{
		Should.Throw<ArgumentException>(() =>
			((System.Collections.IEqualityComparer)_sut).GetHashCode("not-bytes"));
	}
}
