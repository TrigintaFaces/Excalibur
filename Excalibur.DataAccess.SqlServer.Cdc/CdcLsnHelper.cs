namespace Excalibur.DataAccess.SqlServer.Cdc;

public static class CdcLsnHelper
{
	public static int CompareLsn(this byte[] lsn1, byte[] lsn2)
	{
		ArgumentNullException.ThrowIfNull(lsn1);
		ArgumentNullException.ThrowIfNull(lsn2);

		for (var i = lsn1.Length - 1; i >= 0; i--)
		{
			if (lsn1[i] != lsn2[i])
			{
				return lsn1[i].CompareTo(lsn2[i]);
			}
		}

		return 0;
	}
}
