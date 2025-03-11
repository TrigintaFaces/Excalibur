namespace Excalibur.DataAccess.SqlServer.Cdc;

public class CdcPosition(byte[] lsn, byte[]? seqVal)
{
	public byte[] Lsn { get; init; } = lsn;

	public byte[]? SequenceValue { get; init; } = seqVal;
}
