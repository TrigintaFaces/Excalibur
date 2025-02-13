namespace Excalibur.DataAccess.SqlServer.Cdc;

public static class DatabaseConfigDefaults
{
	public const int CdcDefaultBatchTimeInterval = 5000;
	public const int CdcDefaultQueueSize = 5000;
	public const int CdcDefaultProducerBatchSize = 100;
	public const int CdcDefaultConsumerBatchSize = 10;
	public const bool CdcDefaultStopOnMissingTableHandler = true;

	public static readonly string[] CdcDefaultCaptureInstances = [];
}
