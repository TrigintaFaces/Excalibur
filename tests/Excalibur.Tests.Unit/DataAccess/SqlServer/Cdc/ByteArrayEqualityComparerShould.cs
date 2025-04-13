using Excalibur.DataAccess.SqlServer.Cdc;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.SqlServer.Cdc;

public class ByteArrayEqualityComparerShould
{
	private readonly ByteArrayEqualityComparer _comparer = new();

	[Fact]
	public void EqualsShouldReturnTrueWhenBothNull()
	{
		_comparer.Equals(null, null).ShouldBeTrue();
	}

	[Fact]
	public void EqualsShouldReturnFalseWhenOneIsNull()
	{
		_comparer.Equals([0x01], null).ShouldBeFalse();
		_comparer.Equals(null, [0x01]).ShouldBeFalse();
	}

	[Fact]
	public void EqualsShouldReturnTrueWhenArraysAreEqual()
	{
		var result = _comparer.Equals([0x01, 0x02], [0x01, 0x02]);

		result.ShouldBeTrue();
	}

	[Fact]
	public void EqualsShouldReturnFalseWhenArraysAreDifferent()
	{
		var result = _comparer.Equals([0x01, 0x02], [0x01, 0x03]);

		result.ShouldBeFalse();
	}

	[Fact]
	public void GetHashCodeShouldBeConsistentForEqualArrays()
	{
		var bytes1 = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
		var bytes2 = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

		var hash1 = _comparer.GetHashCode(bytes1);
		var hash2 = _comparer.GetHashCode(bytes2);

		hash1.ShouldBe(hash2);
	}

	[Fact]
	public void GetHashCodeShouldThrowWhenInputIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() => _comparer.GetHashCode(null!));
	}
}
