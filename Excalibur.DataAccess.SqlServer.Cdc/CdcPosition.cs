namespace Excalibur.DataAccess.SqlServer.Cdc;

public class CdcPosition
{
	public CdcPosition(byte[] lsn, byte[]? seqVal)
	{
		Lsn = lsn;
		SequenceValue = seqVal;
	}

	public byte[] Lsn { get; init; }

	public byte[]? SequenceValue { get; init; }
}
