// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

using Excalibur.Data.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="ByteArrayComparer"/>.
/// Tests lexicographic byte array comparison for LSN ordering.
/// </summary>
/// <remarks>
/// Sprint 201 - Unit Test Coverage Epic.
/// Excalibur.Dispatch-7dm: CDC Unit Tests.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "ByteArrayComparer")]
public sealed class ByteArrayComparerShould : UnitTestBase
{
	private readonly ByteArrayComparer _comparer = new();

	[Fact]
	public void Compare_ReturnZero_WhenBothArraysAreNull()
	{
		// Act
		var result = _comparer.Compare(null, null);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public void Compare_ReturnNegative_WhenFirstArrayIsNull()
	{
		// Arrange
		byte[] second = [0x01, 0x02, 0x03];

		// Act
		var result = _comparer.Compare(null, second);

		// Assert
		result.ShouldBeLessThan(0);
	}

	[Fact]
	public void Compare_ReturnPositive_WhenSecondArrayIsNull()
	{
		// Arrange
		byte[] first = [0x01, 0x02, 0x03];

		// Act
		var result = _comparer.Compare(first, null);

		// Assert
		result.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Compare_ReturnZero_WhenArraysAreEqual()
	{
		// Arrange
		byte[] first = [0x01, 0x02, 0x03];
		byte[] second = [0x01, 0x02, 0x03];

		// Act
		var result = _comparer.Compare(first, second);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public void Compare_ReturnNegative_WhenFirstArrayIsLexicographicallySmaller()
	{
		// Arrange
		byte[] first = [0x01, 0x02, 0x03];
		byte[] second = [0x01, 0x02, 0x04];

		// Act
		var result = _comparer.Compare(first, second);

		// Assert
		result.ShouldBeLessThan(0);
	}

	[Fact]
	public void Compare_ReturnPositive_WhenFirstArrayIsLexicographicallyLarger()
	{
		// Arrange
		byte[] first = [0x01, 0x02, 0xFF];
		byte[] second = [0x01, 0x02, 0x00];

		// Act
		var result = _comparer.Compare(first, second);

		// Assert
		result.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Compare_ReturnNegative_WhenFirstArrayIsShorter()
	{
		// Arrange
		byte[] first = [0x01, 0x02];
		byte[] second = [0x01, 0x02, 0x03];

		// Act
		var result = _comparer.Compare(first, second);

		// Assert
		result.ShouldBeLessThan(0);
	}

	[Fact]
	public void Compare_ReturnPositive_WhenFirstArrayIsLonger()
	{
		// Arrange
		byte[] first = [0x01, 0x02, 0x03, 0x04];
		byte[] second = [0x01, 0x02, 0x03];

		// Act
		var result = _comparer.Compare(first, second);

		// Assert
		result.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void CompareObject_DelegateToTypedCompare()
	{
		// Arrange
		object first = new byte[] { 0x01, 0x02, 0x03 };
		object second = new byte[] { 0x01, 0x02, 0x04 };

		// Act
		var result = _comparer.Compare(first, second);

		// Assert
		result.ShouldBeLessThan(0);
	}

	[Fact]
	public void CompareObject_ThrowArgumentException_WhenObjectsAreNotByteArrays()
	{
		// Arrange
		object first = "not a byte array";
		object second = new byte[] { 0x01, 0x02, 0x03 };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => _comparer.Compare(first, second));
	}
}
