// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Cdc.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;

using Polly;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Behavior tests for core CDC processing internals in <see cref="CdcProcessor"/>.
/// Tests exercise subsystems (<see cref="CdcChangeDetector"/>, <see cref="CdcCheckpointManager"/>)
/// via the composed instances inside CdcProcessor.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class CdcProcessorBehaviorShould : UnitTestBase
{
	private static readonly Type ChangeProcessingStateType = typeof(CdcChangeDetector)
		.GetNestedType("ChangeProcessingState", BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Expected nested ChangeProcessingState type.");

	// Methods on CdcChangeDetector (accessed via _changeDetector field)
	private static readonly MethodInfo ProcessCdcRecordMethod = typeof(CdcChangeDetector)
		.GetMethod("ProcessCdcRecord", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException("Expected private ProcessCdcRecord method.");

	private static readonly MethodInfo MatchPendingUpdatesMethod = typeof(CdcChangeDetector)
		.GetMethod("MatchPendingUpdates", BindingFlags.NonPublic | BindingFlags.Static)
		?? throw new InvalidOperationException("Expected private MatchPendingUpdates method.");

	private static readonly MethodInfo ValidateUnmatchedUpdatesMethod = typeof(CdcChangeDetector)
		.GetMethod("ValidateUnmatchedUpdates", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException("Expected private ValidateUnmatchedUpdates method.");

	// Methods on CdcCheckpointManager (accessed via _checkpointManager field)
	private static readonly MethodInfo UpdateLsnTrackingMethod = typeof(CdcCheckpointManager)
		.GetMethod("UpdateLsnTracking", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException("Expected internal UpdateLsnTracking method.");

	private static readonly MethodInfo UpdateLsnAfterProcessingMethod = typeof(CdcCheckpointManager)
		.GetMethod("UpdateLsnAfterProcessing", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException("Expected internal UpdateLsnAfterProcessing method.");

	private static readonly MethodInfo GetNextLsnMethod = typeof(CdcCheckpointManager)
		.GetMethod("GetNextLsn", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException("Expected internal GetNextLsn method.");

	private static readonly MethodInfo IsEmptyLsnMethod = typeof(CdcCheckpointManager)
		.GetMethod("IsEmptyLsn", BindingFlags.NonPublic | BindingFlags.Static)
		?? throw new InvalidOperationException("Expected private IsEmptyLsn method.");

	// Fields on CdcProcessor to extract composed subsystems
	private static readonly FieldInfo ChangeDetectorField = typeof(CdcProcessor)
		.GetField("_changeDetector", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException("Expected _changeDetector field on CdcProcessor.");

	private static readonly FieldInfo CheckpointManagerField = typeof(CdcProcessor)
		.GetField("_checkpointManager", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException("Expected _checkpointManager field on CdcProcessor.");

	[Fact]
	public void ProcessCdcRecord_CreateInsertDeleteAndMatchedUpdateEvents()
	{
		using var processor = CreateProcessor();
		var detector = GetChangeDetector(processor);
		var state = CreateChangeProcessingState(lsn: [0x10], sequenceValue: [0x01]);
		var events = new List<DataChangeEvent>();

		var insert = CreateRow([0x10], [0x01], CdcOperationCodes.Insert, 1, "Created");
		var delete = CreateRow([0x10], [0x02], CdcOperationCodes.Delete, 2, "Deleted");
		var updateBefore = CreateRow([0x10], [0x03], CdcOperationCodes.UpdateBefore, 3, "Pending");
		var updateAfter = CreateRow([0x10], [0x03], CdcOperationCodes.UpdateAfter, 3, "Completed");

		Invoke(ProcessCdcRecordMethod, detector, insert, events, state);
		Invoke(ProcessCdcRecordMethod, detector, delete, events, state);
		Invoke(ProcessCdcRecordMethod, detector, updateBefore, events, state);
		Invoke(ProcessCdcRecordMethod, detector, updateAfter, events, state);

		events.Count.ShouldBe(3);
		events[0].ChangeType.ShouldBe(DataChangeType.Insert);
		events[1].ChangeType.ShouldBe(DataChangeType.Delete);
		events[2].ChangeType.ShouldBe(DataChangeType.Update);
		var statusChange = events[2].Changes.Single(change => change.ColumnName == "Status");
		statusChange.OldValue.ShouldBe("Pending");
		statusChange.NewValue.ShouldBe("Completed");

		GetPendingUpdateBefore(state).ShouldBeEmpty();
		GetPendingUpdateAfter(state).ShouldBeEmpty();
	}

	[Fact]
	public void ProcessCdcRecord_MatchesUpdateWhenAfterArrivesBeforeBefore()
	{
		using var processor = CreateProcessor();
		var detector = GetChangeDetector(processor);
		var state = CreateChangeProcessingState(lsn: [0x20], sequenceValue: [0x07]);
		var events = new List<DataChangeEvent>();

		var updateAfter = CreateRow([0x20], [0x07], CdcOperationCodes.UpdateAfter, 7, "After");
		var updateBefore = CreateRow([0x20], [0x07], CdcOperationCodes.UpdateBefore, 7, "Before");

		Invoke(ProcessCdcRecordMethod, detector, updateAfter, events, state);
		events.ShouldBeEmpty();
		GetPendingUpdateAfter(state).Count.ShouldBe(1);

		Invoke(ProcessCdcRecordMethod, detector, updateBefore, events, state);
		events.Count.ShouldBe(1);
		events[0].ChangeType.ShouldBe(DataChangeType.Update);
		var statusChange = events[0].Changes.Single(change => change.ColumnName == "Status");
		statusChange.OldValue.ShouldBe("Before");
		statusChange.NewValue.ShouldBe("After");
		GetPendingUpdateBefore(state).ShouldBeEmpty();
		GetPendingUpdateAfter(state).ShouldBeEmpty();
	}

	[Fact]
	public void ProcessCdcRecord_UnknownOperation_DoesNotEmitEvents()
	{
		using var processor = CreateProcessor();
		var detector = GetChangeDetector(processor);
		var state = CreateChangeProcessingState(lsn: [0x30], sequenceValue: [0x09]);
		var events = new List<DataChangeEvent>();
		var unknown = CreateRow([0x30], [0x09], CdcOperationCodes.Unknown, 9, "Ignored");

		Invoke(ProcessCdcRecordMethod, detector, unknown, events, state);

		events.ShouldBeEmpty();
		GetPendingUpdateBefore(state).ShouldBeEmpty();
		GetPendingUpdateAfter(state).ShouldBeEmpty();
	}

	[Fact]
	public void MatchPendingUpdates_ResolvesCrossBatchPairs()
	{
		var state = CreateChangeProcessingState(lsn: [0x40], sequenceValue: [0x0A]);
		var pendingBefore = GetPendingUpdateBefore(state);
		var pendingAfter = GetPendingUpdateAfter(state);
		var seq = new byte[] { 0xAA };
		pendingBefore[seq] = CreateRow([0x40], seq, CdcOperationCodes.UpdateBefore, 11, "Before");
		pendingAfter[seq] = CreateRow([0x40], seq, CdcOperationCodes.UpdateAfter, 11, "After");
		var events = new List<DataChangeEvent>();

		InvokeStatic(MatchPendingUpdatesMethod, events, state);

		events.Count.ShouldBe(1);
		events[0].ChangeType.ShouldBe(DataChangeType.Update);
		pendingBefore.ShouldBeEmpty();
		pendingAfter.ShouldBeEmpty();
	}

	[Fact]
	public void ValidateUnmatchedUpdates_ThrowsWhenPendingUpdatePairsRemain()
	{
		using var processor = CreateProcessor();
		var detector = GetChangeDetector(processor);
		var state = CreateChangeProcessingState(lsn: [0x50], sequenceValue: [0x0B]);
		var pendingBefore = GetPendingUpdateBefore(state);
		var seq = new byte[] { 0x0B };
		pendingBefore[seq] = CreateRow([0x50], seq, CdcOperationCodes.UpdateBefore, 12, "Unmatched");

		var exception = Should.Throw<UnmatchedUpdateRecordsException>(() =>
			Invoke(ValidateUnmatchedUpdatesMethod, detector, state));

		exception.Lsn.SequenceEqual(new byte[] { 0x50 }).ShouldBeTrue();
	}

	[Fact]
	public void UpdateLsnTracking_MaintainsMinHeapOrderingAndRemoval()
	{
		using var processor = CreateProcessor();
		var checkpointMgr = GetCheckpointManager(processor);
		var tableA = "dbo_orders";
		var tableB = "sales_invoices";
		var lsn1 = new byte[] { 0x01 };
		var lsn2 = new byte[] { 0x02 };
		var lsn3 = new byte[] { 0x03 };

		Invoke(UpdateLsnTrackingMethod, checkpointMgr, tableA, lsn2, null);
		Invoke(UpdateLsnTrackingMethod, checkpointMgr, tableB, lsn1, null);
		GetNextLsn(checkpointMgr).ShouldBe(lsn1);

		// Ensure older LSN does not regress an existing table position.
		Invoke(UpdateLsnTrackingMethod, checkpointMgr, tableA, lsn1, null);
		GetNextLsn(checkpointMgr).ShouldBe(lsn1);

		Invoke(UpdateLsnTrackingMethod, checkpointMgr, tableA, lsn3, null);
		Invoke(UpdateLsnTrackingMethod, checkpointMgr, tableB, null, null);
		GetNextLsn(checkpointMgr).ShouldBe(lsn3);
	}

	[Fact]
	public void UpdateLsnAfterProcessing_AdvancesOrRemovesTrackingBasedOnMaxLsn()
	{
		using var processor = CreateProcessor();
		var checkpointMgr = GetCheckpointManager(processor);
		var table = "dbo_shipments";
		var current = new byte[] { 0x10 };
		var next = new byte[] { 0x20 };
		var max = new byte[] { 0x30 };

		Invoke(UpdateLsnTrackingMethod, checkpointMgr, table, current, null);
		Invoke(UpdateLsnAfterProcessingMethod, checkpointMgr, table, next, max);
		GetNextLsn(checkpointMgr).ShouldBe(next);

		Invoke(UpdateLsnAfterProcessingMethod, checkpointMgr, table, max, max);
		GetNextLsn(checkpointMgr).ShouldBeNull();
	}

	[Fact]
	public void PrivateHelpers_ConvertLsnAndDetectEmptyValues()
	{
		var allZero = new byte[] { 0x00, 0x00, 0x00 };
		var nonZero = new byte[] { 0x00, 0x01, 0x00 };

		((bool)InvokeStatic(IsEmptyLsnMethod, allZero)!).ShouldBeTrue();
		((bool)InvokeStatic(IsEmptyLsnMethod, nonZero)!).ShouldBeFalse();
		CdcChangeDetector.ByteArrayToHex([0x12, 0xAB]).ShouldBe("0x12AB");
	}

	private static CdcProcessor CreateProcessor()
	{
		var appLifetime = A.Fake<IHostApplicationLifetime>();
		var dbConfig = A.Fake<IDatabaseOptions>();
		var policyFactory = A.Fake<IDataAccessPolicyFactory>();
		var logger = A.Fake<ILogger<CdcProcessor>>();

		A.CallTo(() => dbConfig.QueueSize).Returns(32);
		A.CallTo(() => dbConfig.ProducerBatchSize).Returns(16);
		A.CallTo(() => dbConfig.ConsumerBatchSize).Returns(8);
		A.CallTo(() => dbConfig.DatabaseConnectionIdentifier).Returns("test-connection");
		A.CallTo(() => dbConfig.DatabaseName).Returns("test-db");
		A.CallTo(() => dbConfig.CaptureInstances).Returns(["dbo_orders"]);

		var noOpPolicy = Policy.NoOpAsync();
		A.CallTo(() => policyFactory.GetComprehensivePolicy()).Returns(noOpPolicy);
		A.CallTo(() => policyFactory.GetRetryPolicy()).Returns(noOpPolicy);
		A.CallTo(() => policyFactory.CreateCircuitBreakerPolicy()).Returns(noOpPolicy);

		return new CdcProcessor(
			appLifetime,
			dbConfig,
			new CdcRepository(new SqlConnection("Server=localhost;Database=master;Encrypt=false;TrustServerCertificate=true")),
			new SqlConnection("Server=localhost;Database=master;Encrypt=false;TrustServerCertificate=true"),
			stateStoreOptions: null,
			policyFactory,
			logger);
	}

	private static object GetChangeDetector(CdcProcessor processor) =>
		ChangeDetectorField.GetValue(processor)
		?? throw new InvalidOperationException("_changeDetector field was null.");

	private static object GetCheckpointManager(CdcProcessor processor) =>
		CheckpointManagerField.GetValue(processor)
		?? throw new InvalidOperationException("_checkpointManager field was null.");

	private static object CreateChangeProcessingState(byte[] lsn, byte[]? sequenceValue)
	{
		var state = Activator.CreateInstance(ChangeProcessingStateType)
					?? throw new InvalidOperationException("Failed to create ChangeProcessingState.");

		SetProperty(state, "TableName", "dbo_orders");
		SetProperty(state, "Lsn", lsn);
		SetProperty(state, "SequenceValue", sequenceValue);
		SetProperty(state, "LastOperation", CdcOperationCodes.Unknown);
		SetProperty(state, "TotalRowsReadInThisLsn", 0);
		SetProperty(state, "PendingUpdateBefore", new Dictionary<byte[], CdcRow>(new ByteArrayEqualityComparer()));
		SetProperty(state, "PendingUpdateAfter", new Dictionary<byte[], CdcRow>(new ByteArrayEqualityComparer()));

		return state;
	}

	private static Dictionary<byte[], CdcRow> GetPendingUpdateBefore(object state) =>
		(Dictionary<byte[], CdcRow>)GetProperty(state, "PendingUpdateBefore");

	private static Dictionary<byte[], CdcRow> GetPendingUpdateAfter(object state) =>
		(Dictionary<byte[], CdcRow>)GetProperty(state, "PendingUpdateAfter");

	private static byte[]? GetNextLsn(object checkpointManager) =>
		(byte[]?)Invoke(GetNextLsnMethod, checkpointManager);

	private static CdcRow CreateRow(byte[] lsn, byte[] seqVal, CdcOperationCodes operationCode, int id, string status) =>
		new()
		{
			TableName = "dbo_orders",
			Lsn = lsn,
			SeqVal = seqVal,
			OperationCode = operationCode,
			CommitTime = DateTime.UtcNow,
			Changes = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["Id"] = id,
				["Status"] = status,
			},
			DataTypes = new Dictionary<string, Type>(StringComparer.Ordinal)
			{
				["Id"] = typeof(int),
				["Status"] = typeof(string),
			},
		};

	private static object? Invoke(MethodInfo method, object target, params object?[] args)
	{
		try
		{
			return method.Invoke(target, args);
		}
		catch (TargetInvocationException ex) when (ex.InnerException is not null)
		{
			throw ex.InnerException;
		}
	}

	private static object? InvokeStatic(MethodInfo method, params object?[] args)
	{
		try
		{
			return method.Invoke(obj: null, args);
		}
		catch (TargetInvocationException ex) when (ex.InnerException is not null)
		{
			throw ex.InnerException;
		}
	}

	private static object GetProperty(object target, string propertyName) =>
		target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(target)
		?? throw new InvalidOperationException($"Property '{propertyName}' was not found.");

	private static void SetProperty(object target, string propertyName, object? value)
	{
		var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
					   ?? throw new InvalidOperationException($"Property '{propertyName}' was not found.");
		property.SetValue(target, value);
	}
}