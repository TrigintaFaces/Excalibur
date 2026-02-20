// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

namespace Excalibur.Data.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
public sealed class MinHeapComparerShould
{
	private readonly MinHeapComparer _sut = new();

	[Fact]
	public void CompareByLsnFirst()
	{
		// Arrange
		var a = (Lsn: new byte[] { 0, 0, 1 }, TableName: "B");
		var b = (Lsn: new byte[] { 0, 0, 2 }, TableName: "A");

		// Act
		var result = _sut.Compare(a, b);

		// Assert
		result.ShouldBeLessThan(0);
	}

	[Fact]
	public void CompareByTableNameWhenLsnEqual()
	{
		// Arrange
		var a = (Lsn: new byte[] { 0, 0, 1 }, TableName: "Alpha");
		var b = (Lsn: new byte[] { 0, 0, 1 }, TableName: "Beta");

		// Act
		var result = _sut.Compare(a, b);

		// Assert
		result.ShouldBeLessThan(0);
	}

	[Fact]
	public void ReturnZeroForEqualEntries()
	{
		// Arrange
		var a = (Lsn: new byte[] { 0, 0, 1 }, TableName: "Table");
		var b = (Lsn: new byte[] { 0, 0, 1 }, TableName: "Table");

		// Act
		var result = _sut.Compare(a, b);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public void SupportObjectOverload()
	{
		// Arrange
		object a = (new byte[] { 0, 0, 1 }, "A");
		object b = (new byte[] { 0, 0, 2 }, "B");

		// Act
		var result = ((System.Collections.IComparer)_sut).Compare(a, b);

		// Assert
		result.ShouldBeLessThan(0);
	}

	[Fact]
	public void ReturnZeroForObjectOverloadSameReference()
	{
		// Arrange
		object a = (new byte[] { 1 }, "X");

		// Act
		var result = ((System.Collections.IComparer)_sut).Compare(a, a);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public void ReturnNegativeForObjectOverloadNullFirst()
	{
		// Arrange
		object b = (new byte[] { 1 }, "X");

		// Act
		var result = ((System.Collections.IComparer)_sut).Compare(null, b);

		// Assert
		result.ShouldBeLessThan(0);
	}

	[Fact]
	public void ReturnPositiveForObjectOverloadNullSecond()
	{
		// Arrange
		object a = (new byte[] { 1 }, "X");

		// Act
		var result = ((System.Collections.IComparer)_sut).Compare(a, null);

		// Assert
		result.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void ThrowForObjectOverloadNonTupleTypes()
	{
		Should.Throw<ArgumentException>(() =>
			((System.Collections.IComparer)_sut).Compare("not", "tuples"));
	}
}
