using Excalibur.DataAccess.SqlServer.Cdc;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.SqlServer.Cdc;

public class MinHeapComparerShould
{
	private readonly MinHeapComparer _comparer = new();

	[Fact]
	public void CompareShouldOrderByLsnWhenLsnsDiffer()
	{
		// Arrange
		var lower = (Lsn: new byte[] { 0x01 }, TableName: "A");
		var higher = (Lsn: new byte[] { 0x02 }, TableName: "A");

		// Act
		var result = _comparer.Compare(lower, higher);

		// Assert
		result.ShouldBeLessThan(0);
	}

	[Fact]
	public void CompareShouldOrderByTableNameWhenLsnsAreEqual()
	{
		// Arrange
		var first = (Lsn: new byte[] { 0x01 }, TableName: "A");
		var second = (Lsn: new byte[] { 0x01 }, TableName: "B");

		// Act
		var result = _comparer.Compare(first, second);

		// Assert
		result.ShouldBeLessThan(0);
	}

	[Fact]
	public void CompareShouldReturnZeroWhenBothAreEqual()
	{
		// Arrange
		var first = (Lsn: new byte[] { 0x01, 0x02 }, TableName: "Table1");
		var second = (Lsn: new byte[] { 0x01, 0x02 }, TableName: "Table1");

		// Act
		var result = _comparer.Compare(first, second);

		// Assert
		result.ShouldBe(0);
	}
}
