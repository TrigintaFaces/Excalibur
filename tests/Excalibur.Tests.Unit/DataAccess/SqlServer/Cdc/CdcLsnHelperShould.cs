using Excalibur.DataAccess.SqlServer.Cdc;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.SqlServer.Cdc;

public class CdcLsnHelperShould
{
	[Fact]
	public void CompareLsnShouldReturnZeroWhenArraysAreEqual()
	{
		// Arrange
		var lsn1 = new byte[] { 0x01, 0x02, 0x03 };
		var lsn2 = new byte[] { 0x01, 0x02, 0x03 };

		// Act
		var result = lsn1.CompareLsn(lsn2);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public void CompareLsnShouldReturnNegativeWhenFirstIsLess()
	{
		// Arrange
		var lsn1 = new byte[] { 0x01, 0x01, 0x01 };
		var lsn2 = new byte[] { 0x01, 0x02, 0x01 };

		// Act
		var result = lsn1.CompareLsn(lsn2);

		// Assert
		result.ShouldBeLessThan(0);
	}

	[Fact]
	public void CompareLsnShouldReturnPositiveWhenFirstIsGreater()
	{
		// Arrange
		var lsn1 = new byte[] { 0x01, 0x03, 0x01 };
		var lsn2 = new byte[] { 0x01, 0x02, 0x01 };

		// Act
		var result = lsn1.CompareLsn(lsn2);

		// Assert
		result.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void ByteArrayToHexShouldReturnCorrectHexFormat()
	{
		// Arrange
		var bytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

		// Act
		var result = bytes.ByteArrayToHex();

		// Assert
		result.ShouldBe("0xDEADBEEF");
	}
}
