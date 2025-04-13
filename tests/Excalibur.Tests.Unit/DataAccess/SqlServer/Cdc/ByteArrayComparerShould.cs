using Excalibur.DataAccess.SqlServer.Cdc;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.SqlServer.Cdc;

public class ByteArrayComparerShould
{
	private readonly ByteArrayComparer _comparer = new();

	[Fact]
	public void CompareShouldReturnZeroWhenSameReference()
	{
		// Arrange
		var bytes = new byte[] { 0x01, 0x02 };

		// Act
		var result = _comparer.Compare(bytes, bytes);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public void CompareShouldReturnNegativeWhenFirstIsNull()
	{
		_comparer.Compare(null, [0x01]).ShouldBeLessThan(0);
	}

	[Fact]
	public void CompareShouldReturnPositiveWhenSecondIsNull()
	{
		_comparer.Compare([0x01], null).ShouldBeGreaterThan(0);
	}

	[Fact]
	public void CompareShouldReturnZeroWhenEqualContent()
	{
		var result = _comparer.Compare([0x01, 0x02], [0x01, 0x02]);

		result.ShouldBe(0);
	}

	[Fact]
	public void CompareShouldReturnNegativeWhenFirstIsLess()
	{
		var result = _comparer.Compare([0x01, 0x02], [0x02, 0x01]);

		result.ShouldBeLessThan(0);
	}

	[Fact]
	public void CompareShouldReturnPositiveWhenFirstIsGreater()
	{
		var result = _comparer.Compare([0x03, 0x02], [0x01, 0xFF]);

		result.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void CompareShouldUseLengthWhenPrefixesMatch()
	{
		var result = _comparer.Compare([0x01, 0x02], [0x01, 0x02, 0x03]);

		result.ShouldBeLessThan(0);
	}
}
