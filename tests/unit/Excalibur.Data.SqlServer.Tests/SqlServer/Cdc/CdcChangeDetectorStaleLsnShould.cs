// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading.Channels;

using Excalibur.Cdc.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;

using Polly;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Tests for <see cref="CdcChangeDetector"/> defensive stale LSN check.
/// Verifies that when a table's checkpoint LSN predates the CDC cleanup boundary,
/// the detector resets the checkpoint to the minimum valid LSN and skips fetching
/// changes for that cycle — preventing SQL Error 313 from the CDC TVF.
/// </summary>
/// <remarks>
/// Sprint 825 — bd-5e0iou: Per-table LSN reset defense-in-depth.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class CdcChangeDetectorStaleLsnShould : UnitTestBase
{
	// Stale LSN is below the cleanup boundary
	private static readonly byte[] StaleLsn = [0x00, 0x00, 0x00, 0x01];
	// Min LSN returned by GetMinPositionAsync (cleanup boundary)
	private static readonly byte[] MinLsn = [0x00, 0x00, 0x00, 0x05];
	// Max LSN returned by GetMaxPositionAsync
	private static readonly byte[] MaxLsn = [0x00, 0x00, 0x00, 0x0A];
	// Next LSN after MinLsn (returned after processing)
	private static readonly byte[] NextLsn = [0x00, 0x00, 0x00, 0x06];

	private const string CaptureInstance = "dbo_Orders";

	[Fact]
	public async Task SkipFetchAndResetCheckpoint_WhenTableLsnPredatesCleanupBoundary()
	{
		// Arrange
		var cdcRepository = A.Fake<ICdcRepository>();
		var cdcLsnMapping = A.Fake<ICdcRepositoryLsnMapping>();
		var dbConfig = CreateDbConfig();
		var policyFactory = CreatePolicyFactory();
		var stateStore = A.Fake<ISqlServerCdcStateStore>();
		var logger = NullLogger<CdcChangeDetectorStaleLsnShould>.Instance;

		// Repository returns MaxLsn for the global max position
		A.CallTo(() => cdcRepository.GetMaxPositionAsync(A<CancellationToken>._))
			.Returns(MaxLsn);

		// Repository returns MinLsn as the cleanup boundary for this table
		A.CallTo(() => cdcRepository.GetMinPositionAsync(CaptureInstance, A<CancellationToken>._))
			.Returns(MinLsn);

		// After reset: when the table is processed at MinLsn, return empty changes
		A.CallTo(() => cdcRepository.FetchChangesAsync(
				CaptureInstance,
				A<int>._,
				A<byte[]>._,
				A<byte[]>._,
				A<byte[]?>._,
				A<CdcOperationCodes>._,
				A<CancellationToken>._,
				A<string?>._))
			.Returns(Task.FromResult<IEnumerable<CdcRow>>([]));

		// After empty fetch, checkpoint manager needs commit time and next LSN
		A.CallTo(() => cdcLsnMapping.GetLsnToTimeAsync(A<byte[]>._, A<CancellationToken>._))
			.Returns(Task.FromResult<DateTime?>(DateTime.UtcNow));
		A.CallTo(() => cdcLsnMapping.GetNextLsnAsync(CaptureInstance, A<byte[]>._, A<CancellationToken>._))
			.Returns(Task.FromResult<byte[]?>(null)); // No more LSNs

		// State store returns empty (fresh start) then succeeds on update
		A.CallTo(() => stateStore.GetLastProcessedPositionAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IEnumerable<CdcProcessingState>>([]));
		A.CallTo(() => stateStore.UpdateLastProcessedPositionAsync(
				A<string>._, A<string>._, A<string>._, A<byte[]>._, A<byte[]?>._, A<DateTime?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(1));

		// Create checkpoint manager and set up stale tracking position
		var checkpointManager = new CdcCheckpointManager(
			dbConfig, cdcRepository, stateStore, logger);
		checkpointManager.UpdateLsnTracking(CaptureInstance, StaleLsn, seqVal: null);

		// Create detector under test
		var detector = new CdcChangeDetector(
			cdcRepository, cdcLsnMapping, dbConfig, policyFactory, checkpointManager, logger);

		var channel = Channel.CreateUnbounded<DataChangeEvent>();

		// Act
		await detector.ProducerLoopCoreAsync(StaleLsn, channel.Writer, queueSize: 32, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert — FetchChangesAsync should NOT have been called with StaleLsn
		// The defensive check resets the checkpoint, so any fetch call uses MinLsn or later
		A.CallTo(() => cdcRepository.FetchChangesAsync(
				CaptureInstance,
				A<int>._,
				A<byte[]>.That.Matches(lsn => lsn.SequenceEqual(StaleLsn)),
				A<byte[]>._,
				A<byte[]?>._,
				A<CdcOperationCodes>._,
				A<CancellationToken>._,
				A<string?>._))
			.MustNotHaveHappened();

		// Assert — GetMinPositionAsync was called to check the cleanup boundary
		A.CallTo(() => cdcRepository.GetMinPositionAsync(CaptureInstance, A<CancellationToken>._))
			.MustHaveHappenedOnceOrMore();
	}

	[Fact]
	public async Task ProceedNormally_WhenTableLsnIsAtOrAboveCleanupBoundary()
	{
		// Arrange
		var cdcRepository = A.Fake<ICdcRepository>();
		var cdcLsnMapping = A.Fake<ICdcRepositoryLsnMapping>();
		var dbConfig = CreateDbConfig();
		var policyFactory = CreatePolicyFactory();
		var stateStore = A.Fake<ISqlServerCdcStateStore>();
		var logger = NullLogger<CdcChangeDetectorStaleLsnShould>.Instance;

		// Table LSN equals MinLsn (not stale)
		var validLsn = MinLsn;

		A.CallTo(() => cdcRepository.GetMaxPositionAsync(A<CancellationToken>._))
			.Returns(MaxLsn);
		A.CallTo(() => cdcRepository.GetMinPositionAsync(CaptureInstance, A<CancellationToken>._))
			.Returns(MinLsn);

		// Return empty changes so the loop exits cleanly
		A.CallTo(() => cdcRepository.FetchChangesAsync(
				CaptureInstance,
				A<int>._,
				A<byte[]>._,
				A<byte[]>._,
				A<byte[]?>._,
				A<CdcOperationCodes>._,
				A<CancellationToken>._,
				A<string?>._))
			.Returns(Task.FromResult<IEnumerable<CdcRow>>([]));

		A.CallTo(() => cdcLsnMapping.GetLsnToTimeAsync(A<byte[]>._, A<CancellationToken>._))
			.Returns(Task.FromResult<DateTime?>(DateTime.UtcNow));
		A.CallTo(() => cdcLsnMapping.GetNextLsnAsync(CaptureInstance, A<byte[]>._, A<CancellationToken>._))
			.Returns(Task.FromResult<byte[]?>(null));

		A.CallTo(() => stateStore.GetLastProcessedPositionAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IEnumerable<CdcProcessingState>>([]));
		A.CallTo(() => stateStore.UpdateLastProcessedPositionAsync(
				A<string>._, A<string>._, A<string>._, A<byte[]>._, A<byte[]?>._, A<DateTime?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(1));

		var checkpointManager = new CdcCheckpointManager(
			dbConfig, cdcRepository, stateStore, logger);
		checkpointManager.UpdateLsnTracking(CaptureInstance, validLsn, seqVal: null);

		var detector = new CdcChangeDetector(
			cdcRepository, cdcLsnMapping, dbConfig, policyFactory, checkpointManager, logger);

		var channel = Channel.CreateUnbounded<DataChangeEvent>();

		// Act
		await detector.ProducerLoopCoreAsync(validLsn, channel.Writer, queueSize: 32, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert — FetchChangesAsync SHOULD have been called (LSN is valid)
		A.CallTo(() => cdcRepository.FetchChangesAsync(
				CaptureInstance,
				A<int>._,
				A<byte[]>._,
				A<byte[]>._,
				A<byte[]?>._,
				A<CdcOperationCodes>._,
				A<CancellationToken>._,
				A<string?>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ResetOnlyAffectedTable_WhenOneTableIsStaleAndOtherIsValid()
	{
		// Arrange
		const string staleTable = "dbo_Orders";
		const string validTable = "dbo_Customers";

		var cdcRepository = A.Fake<ICdcRepository>();
		var cdcLsnMapping = A.Fake<ICdcRepositoryLsnMapping>();
		var stateStore = A.Fake<ISqlServerCdcStateStore>();
		var logger = NullLogger<CdcChangeDetectorStaleLsnShould>.Instance;

		var dbConfig = A.Fake<IDatabaseOptions>();
		A.CallTo(() => dbConfig.QueueSize).Returns(32);
		A.CallTo(() => dbConfig.ProducerBatchSize).Returns(16);
		A.CallTo(() => dbConfig.ConsumerBatchSize).Returns(8);
		A.CallTo(() => dbConfig.DatabaseConnectionIdentifier).Returns("test-conn");
		A.CallTo(() => dbConfig.DatabaseName).Returns("test-db");
		A.CallTo(() => dbConfig.CaptureInstances).Returns([staleTable, validTable]);
		A.CallTo(() => dbConfig.CaptureInstanceToTableNameMap).Returns(
			new Dictionary<string, string>
			{
				[staleTable] = staleTable,
				[validTable] = validTable,
			});

		var policyFactory = CreatePolicyFactory();

		// Both tables start at the same LSN
		var startLsn = StaleLsn;

		A.CallTo(() => cdcRepository.GetMaxPositionAsync(A<CancellationToken>._))
			.Returns(MaxLsn);

		// Stale table: min LSN is above the start position
		A.CallTo(() => cdcRepository.GetMinPositionAsync(staleTable, A<CancellationToken>._))
			.Returns(MinLsn);

		// Valid table: min LSN is at or below the start position
		A.CallTo(() => cdcRepository.GetMinPositionAsync(validTable, A<CancellationToken>._))
			.Returns(StaleLsn); // Same as start, so not stale

		// Return empty changes for the valid table
		A.CallTo(() => cdcRepository.FetchChangesAsync(
				A<string>._,
				A<int>._,
				A<byte[]>._,
				A<byte[]>._,
				A<byte[]?>._,
				A<CdcOperationCodes>._,
				A<CancellationToken>._,
				A<string?>._))
			.Returns(Task.FromResult<IEnumerable<CdcRow>>([]));

		A.CallTo(() => cdcLsnMapping.GetLsnToTimeAsync(A<byte[]>._, A<CancellationToken>._))
			.Returns(Task.FromResult<DateTime?>(DateTime.UtcNow));
		A.CallTo(() => cdcLsnMapping.GetNextLsnAsync(A<string>._, A<byte[]>._, A<CancellationToken>._))
			.Returns(Task.FromResult<byte[]?>(null));

		A.CallTo(() => stateStore.GetLastProcessedPositionAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IEnumerable<CdcProcessingState>>([]));
		A.CallTo(() => stateStore.UpdateLastProcessedPositionAsync(
				A<string>._, A<string>._, A<string>._, A<byte[]>._, A<byte[]?>._, A<DateTime?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(1));

		var checkpointManager = new CdcCheckpointManager(
			dbConfig, cdcRepository, stateStore, logger);
		checkpointManager.UpdateLsnTracking(staleTable, startLsn, seqVal: null);
		checkpointManager.UpdateLsnTracking(validTable, startLsn, seqVal: null);

		var detector = new CdcChangeDetector(
			cdcRepository, cdcLsnMapping, dbConfig, policyFactory, checkpointManager, logger);

		var channel = Channel.CreateUnbounded<DataChangeEvent>();

		// Act
		await detector.ProducerLoopCoreAsync(startLsn, channel.Writer, queueSize: 32, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert — FetchChangesAsync should have been called for the valid table
		// but NOT for the stale table at the stale LSN
		A.CallTo(() => cdcRepository.FetchChangesAsync(
				validTable,
				A<int>._,
				A<byte[]>._,
				A<byte[]>._,
				A<byte[]?>._,
				A<CdcOperationCodes>._,
				A<CancellationToken>._,
				A<string?>._))
			.MustHaveHappened();

		// GetMinPositionAsync must have been called for both tables
		A.CallTo(() => cdcRepository.GetMinPositionAsync(staleTable, A<CancellationToken>._))
			.MustHaveHappened();
		A.CallTo(() => cdcRepository.GetMinPositionAsync(validTable, A<CancellationToken>._))
			.MustHaveHappened();
	}

	private static IDatabaseOptions CreateDbConfig()
	{
		var dbConfig = A.Fake<IDatabaseOptions>();
		A.CallTo(() => dbConfig.QueueSize).Returns(32);
		A.CallTo(() => dbConfig.ProducerBatchSize).Returns(16);
		A.CallTo(() => dbConfig.ConsumerBatchSize).Returns(8);
		A.CallTo(() => dbConfig.DatabaseConnectionIdentifier).Returns("test-conn");
		A.CallTo(() => dbConfig.DatabaseName).Returns("test-db");
		A.CallTo(() => dbConfig.CaptureInstances).Returns([CaptureInstance]);
		A.CallTo(() => dbConfig.CaptureInstanceToTableNameMap).Returns(
			new Dictionary<string, string> { [CaptureInstance] = CaptureInstance });
		return dbConfig;
	}

	private static IDataAccessPolicyFactory CreatePolicyFactory()
	{
		var policyFactory = A.Fake<IDataAccessPolicyFactory>();
		var noOpPolicy = Policy.NoOpAsync();
		A.CallTo(() => policyFactory.GetComprehensivePolicy()).Returns(noOpPolicy);
		A.CallTo(() => policyFactory.GetRetryPolicy()).Returns(noOpPolicy);
		A.CallTo(() => policyFactory.CreateCircuitBreakerPolicy()).Returns(noOpPolicy);
		return policyFactory;
	}
}
