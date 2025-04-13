using Excalibur.DataAccess.SqlServer.Cdc;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.SqlServer.Cdc;

public class CdcPositionShould
{
	[Fact]
	public void InitializeWithLsnAndSequenceValue()
	{
		var lsn = new byte[] { 0x01, 0x02 };
		var seq = new byte[] { 0x03, 0x04 };

		var position = new CdcPosition(lsn, seq);

		position.Lsn.ShouldBe(lsn);
		position.SequenceValue.ShouldBe(seq);
	}

	[Fact]
	public void InitializeWithLsnAndNullSequenceValue()
	{
		var lsn = new byte[] { 0x0A, 0x0B };

		var position = new CdcPosition(lsn, null);

		position.Lsn.ShouldBe(lsn);
		position.SequenceValue.ShouldBeNull();
	}

	[Fact]
	public void AllowLsnToBeRetrievedCorrectly()
	{
		var expected = new byte[] { 0x42, 0x43 };
		var position = new CdcPosition(expected, null);

		position.Lsn.ShouldBe(expected);
	}
}
