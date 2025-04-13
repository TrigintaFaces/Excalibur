namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Represents the current position in the CDC log for a specific table, defined by a Log Sequence Number (LSN) and an optional sequence value.
/// </summary>
public class CdcPosition(byte[] lsn, byte[]? seqVal)
{
	/// <summary>
	///     The Log Sequence Number indicating the current CDC position.
	/// </summary>
	public byte[] Lsn { get; init; } = lsn;

	/// <summary>
	///     An optional sequence value for finer-grained CDC tracking.
	/// </summary>
	public byte[]? SequenceValue { get; init; } = seqVal;
}
