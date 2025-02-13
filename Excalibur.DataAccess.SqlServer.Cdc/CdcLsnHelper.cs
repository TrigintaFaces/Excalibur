namespace Excalibur.DataAccess.SqlServer.Cdc;

public static class CdcLsnHelper
{
	public static int CompareLsn(this byte[] lsn1, byte[] lsn2)
	{
		ArgumentNullException.ThrowIfNull(lsn1);
		ArgumentNullException.ThrowIfNull(lsn2);

		var minLength = Math.Min(lsn1.Length, lsn2.Length);
		for (var i = 0; i < minLength; i++)
		{
			if (lsn1[i] != lsn2[i])
			{
				return lsn1[i].CompareTo(lsn2[i]);
			}
		}

		return lsn1.Length.CompareTo(lsn2.Length);
	}
}
