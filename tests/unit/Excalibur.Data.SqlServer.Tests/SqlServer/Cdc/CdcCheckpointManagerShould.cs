// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="CdcCheckpointManager"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Feature", "CDC")]
public sealed class CdcCheckpointManagerShould : UnitTestBase
{
	private static CdcCheckpointManager CreateSut(
		IDatabaseOptions? dbConfig = null,
		ICdcRepository? cdcRepository = null,
		ISqlServerCdcStateStore? stateStore = null)
	{
		return new CdcCheckpointManager(
			dbConfig ?? A.Fake<IDatabaseOptions>(),
			cdcRepository ?? A.Fake<ICdcRepository>(),
			stateStore ?? A.Fake<ISqlServerCdcStateStore>(),
			NullLogger.Instance);
	}

	private static void InvokeUpdateLsnTracking(CdcCheckpointManager sut, string tableName, byte[]? lsn, byte[]? seqVal)
	{
		var method = typeof(CdcCheckpointManager).GetMethod(
			"UpdateLsnTracking",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		method!.Invoke(sut, [tableName, lsn, seqVal]);
	}

	[Fact]
	public void TrackingCount_ReturnsZero_WhenNoTablesTracked()
	{
		var sut = CreateSut();

		sut.TrackingCount.ShouldBe(0);
	}

	[Fact]
	public void TrackedTables_ReturnsEmpty_WhenNoTablesTracked()
	{
		var sut = CreateSut();

		sut.TrackedTables.ShouldBeEmpty();
	}

	[Fact]
	public void GetTracking_ReturnsNull_WhenTableNotTracked()
	{
		var sut = CreateSut();

		sut.GetTracking("nonexistent").ShouldBeNull();
	}

	[Fact]
	public void GetNextLsn_ReturnsNull_WhenNoTablesTracked()
	{
		var sut = CreateSut();

		sut.GetNextLsn().ShouldBeNull();
	}

	[Fact]
	public async Task InitializeTrackingAsync_LoadsPositionsFromStateStore()
	{
		var dbConfig = A.Fake<IDatabaseOptions>();
		var cdcRepository = A.Fake<ICdcRepository>();
		var stateStore = A.Fake<ISqlServerCdcStateStore>();
		var sut = CreateSut(dbConfig, cdcRepository, stateStore);

		A.CallTo(() => dbConfig.CaptureInstances).Returns(["dbo_orders"]);
		A.CallTo(() => dbConfig.DatabaseConnectionIdentifier).Returns("test-conn");
		A.CallTo(() => dbConfig.DatabaseName).Returns("test-db");

		var processingStates = new List<CdcProcessingState>
		{
			new()
			{
				TableName = "dbo_orders",
				LastProcessedLsn = new byte[] { 0x01, 0x02, 0x03 },
				LastProcessedSequenceValue = new byte[] { 0x04 }
			}
		};

		A.CallTo(() => stateStore.GetLastProcessedPositionAsync(
				"test-conn", "test-db", A<CancellationToken>._))
			.Returns(processingStates);

		await sut.InitializeTrackingAsync(CancellationToken.None);

		sut.TrackingCount.ShouldBe(1);
		sut.TrackedTables.ShouldContain("dbo_orders");
		var tracking = sut.GetTracking("dbo_orders");
		tracking.ShouldNotBeNull();
		tracking.Lsn.ShouldBe(new byte[] { 0x01, 0x02, 0x03 });
		tracking.SequenceValue.ShouldBe(new byte[] { 0x04 });
	}

	[Fact]
	public async Task InitializeTrackingAsync_FallsBackToMinPosition_WhenNoState()
	{
		var dbConfig = A.Fake<IDatabaseOptions>();
		var cdcRepository = A.Fake<ICdcRepository>();
		var stateStore = A.Fake<ISqlServerCdcStateStore>();
		var sut = CreateSut(dbConfig, cdcRepository, stateStore);

		A.CallTo(() => dbConfig.CaptureInstances).Returns(["dbo_orders"]);
		A.CallTo(() => dbConfig.DatabaseConnectionIdentifier).Returns("conn");
		A.CallTo(() => dbConfig.DatabaseName).Returns("db");
		A.CallTo(() => stateStore.GetLastProcessedPositionAsync(
				A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(new List<CdcProcessingState>());
		A.CallTo(() => cdcRepository.GetMinPositionAsync("dbo_orders", A<CancellationToken>._))
			.Returns(new byte[] { 0xAA });

		await sut.InitializeTrackingAsync(CancellationToken.None);

		sut.TrackingCount.ShouldBe(1);
		var tracking = sut.GetTracking("dbo_orders");
		tracking.ShouldNotBeNull();
		tracking.Lsn.ShouldBe(new byte[] { 0xAA });
	}

	[Fact]
	public async Task InitializeTrackingAsync_FallsBackToMinPosition_WhenLsnIsAllZeros()
	{
		var dbConfig = A.Fake<IDatabaseOptions>();
		var cdcRepository = A.Fake<ICdcRepository>();
		var stateStore = A.Fake<ISqlServerCdcStateStore>();
		var sut = CreateSut(dbConfig, cdcRepository, stateStore);

		A.CallTo(() => dbConfig.CaptureInstances).Returns(["dbo_orders"]);
		A.CallTo(() => dbConfig.DatabaseConnectionIdentifier).Returns("conn");
		A.CallTo(() => dbConfig.DatabaseName).Returns("db");

		var states = new List<CdcProcessingState>
		{
			new()
			{
				TableName = "dbo_orders",
				LastProcessedLsn = new byte[] { 0x00, 0x00, 0x00 },
				LastProcessedSequenceValue = null
			}
		};
		A.CallTo(() => stateStore.GetLastProcessedPositionAsync(
				A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(states);
		A.CallTo(() => cdcRepository.GetMinPositionAsync("dbo_orders", A<CancellationToken>._))
			.Returns(new byte[] { 0xBB });

		await sut.InitializeTrackingAsync(CancellationToken.None);

		var tracking = sut.GetTracking("dbo_orders");
		tracking.ShouldNotBeNull();
		tracking.Lsn.ShouldBe(new byte[] { 0xBB });
	}

	[Fact]
	public void UpdateLsnAfterProcessing_AdvancesToNextLsn_WhenBelowMax()
	{
		var sut = CreateSut();
		InvokeUpdateLsnTracking(sut, "dbo_orders", new byte[] { 0x01 }, null);

		sut.UpdateLsnAfterProcessing("dbo_orders", nextLsn: new byte[] { 0x05 }, maxLsn: new byte[] { 0x10 });

		var tracking = sut.GetTracking("dbo_orders");
		tracking.ShouldNotBeNull();
		tracking.Lsn.ShouldBe(new byte[] { 0x05 });
	}

	[Fact]
	public void UpdateLsnAfterProcessing_RemovesTracking_WhenNextLsnEqualsMax()
	{
		var sut = CreateSut();
		InvokeUpdateLsnTracking(sut, "dbo_orders", new byte[] { 0x01 }, null);

		sut.UpdateLsnAfterProcessing("dbo_orders", nextLsn: new byte[] { 0x10 }, maxLsn: new byte[] { 0x10 });

		sut.GetTracking("dbo_orders").ShouldBeNull();
		sut.GetNextLsn().ShouldBeNull();
	}

	[Fact]
	public void UpdateLsnAfterProcessing_RemovesTracking_WhenNextLsnIsNull()
	{
		var sut = CreateSut();
		InvokeUpdateLsnTracking(sut, "dbo_orders", new byte[] { 0x01 }, null);

		sut.UpdateLsnAfterProcessing("dbo_orders", nextLsn: null, maxLsn: new byte[] { 0x10 });

		sut.GetTracking("dbo_orders").ShouldBeNull();
	}

	[Fact]
	public async Task UpdateTableLastProcessedAsync_DelegatesToStateStore()
	{
		var dbConfig = A.Fake<IDatabaseOptions>();
		var stateStore = A.Fake<ISqlServerCdcStateStore>();
		var sut = CreateSut(dbConfig, stateStore: stateStore);

		A.CallTo(() => dbConfig.DatabaseConnectionIdentifier).Returns("conn");
		A.CallTo(() => dbConfig.DatabaseName).Returns("db");
		var lsn = new byte[] { 0x01 };
		var seqVal = new byte[] { 0x02 };
		var commitTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		A.CallTo(() => stateStore.UpdateLastProcessedPositionAsync(
				A<string>._, A<string>._, A<string>._, A<byte[]>._, A<byte[]>._, A<DateTime?>._, A<CancellationToken>._))
			.Returns(1);

		await sut.UpdateTableLastProcessedAsync("dbo_orders", lsn, seqVal, commitTime, CancellationToken.None);

		A.CallTo(() => stateStore.UpdateLastProcessedPositionAsync(
				"conn", "db", "dbo_orders", lsn, seqVal, commitTime, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Clear_RemovesAllTracking()
	{
		var sut = CreateSut();
		InvokeUpdateLsnTracking(sut, "table1", new byte[] { 0x01 }, null);
		InvokeUpdateLsnTracking(sut, "table2", new byte[] { 0x02 }, null);

		sut.TrackingCount.ShouldBe(2);

		sut.Clear();

		sut.TrackedTables.ShouldBeEmpty();
	}

	[Fact]
	public void GetNextLsn_ReturnsLowestLsn()
	{
		var sut = CreateSut();
		InvokeUpdateLsnTracking(sut, "tableA", new byte[] { 0x05 }, null);
		InvokeUpdateLsnTracking(sut, "tableB", new byte[] { 0x01 }, null);
		InvokeUpdateLsnTracking(sut, "tableC", new byte[] { 0x03 }, null);

		sut.GetNextLsn().ShouldBe(new byte[] { 0x01 });
	}
}
