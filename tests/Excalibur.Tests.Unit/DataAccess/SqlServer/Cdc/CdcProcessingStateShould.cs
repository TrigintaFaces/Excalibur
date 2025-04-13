using Excalibur.DataAccess.SqlServer.Cdc;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.SqlServer.Cdc;

public class CdcProcessingStateShould
{
	[Fact]
	public void InitializeAllPropertiesCorrectly()
	{
		var lsn = new byte[] { 0x01, 0x02, 0x03 };
		var seqVal = new byte[] { 0x05 };
		var commitTime = DateTime.UtcNow;
		var processedAt = DateTimeOffset.UtcNow;
		var dbId = "Connection1";
		var dbName = "MainDb";
		var tableName = "Customers";

		var state = new CdcProcessingState
		{
			LastProcessedLsn = lsn,
			LastProcessedSequenceValue = seqVal,
			LastCommitTime = commitTime,
			ProcessedAt = processedAt,
			DatabaseConnectionIdentifier = dbId,
			DatabaseName = dbName,
			TableName = tableName
		};

		state.LastProcessedLsn.ShouldBe(lsn);
		state.LastProcessedSequenceValue.ShouldBe(seqVal);
		state.LastCommitTime.ShouldBe(commitTime);
		state.ProcessedAt.ShouldBe(processedAt);
		state.DatabaseConnectionIdentifier.ShouldBe(dbId);
		state.DatabaseName.ShouldBe(dbName);
		state.TableName.ShouldBe(tableName);
	}

	[Fact]
	public void DefaultLsnShouldBeTenZeroBytes()
	{
		var state = new CdcProcessingState();
		_ = state.LastProcessedLsn.ShouldNotBeNull();
		state.LastProcessedLsn.Length.ShouldBe(10);
		state.LastProcessedLsn.ShouldAllBe(b => b == 0);
	}
}
