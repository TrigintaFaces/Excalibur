// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Data.SqlServer.Cdc;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;

using Polly;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Behavior tests for core CDC processing internals in <see cref="CdcProcessor"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Feature", "CDC")]
public sealed class CdcProcessorBehaviorShould : UnitTestBase
{
	private static readonly Type ChangeProcessingStateType = typeof(CdcProcessor)
		.GetNestedType("ChangeProcessingState", BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Expected nested ChangeProcessingState type.");

	private static readonly MethodInfo ProcessCdcRecordMethod = typeof(CdcProcessor)
		.GetMethod("ProcessCdcRecord", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException("Expected private ProcessCdcRecord method.");

	private static readonly MethodInfo MatchPendingUpdatesMethod = typeof(CdcProcessor)
		.GetMethod("MatchPendingUpdates", BindingFlags.NonPublic | BindingFlags.Static)
		?? throw new InvalidOperationException("Expected private MatchPendingUpdates method.");

	private static readonly MethodInfo ValidateUnmatchedUpdatesMethod = typeof(CdcProcessor)
		.GetMethod("ValidateUnmatchedUpdates", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException("Expected private ValidateUnmatchedUpdates method.");

	private static readonly MethodInfo UpdateLsnTrackingMethod = typeof(CdcProcessor)
		.GetMethod("UpdateLsnTracking", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException("Expected private UpdateLsnTracking method.");

	private static readonly MethodInfo UpdateLsnAfterProcessingMethod = typeof(CdcProcessor)
		.GetMethod("UpdateLsnAfterProcessing", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException("Expected private UpdateLsnAfterProcessing method.");

	private static readonly MethodInfo GetNextLsnMethod = typeof(CdcProcessor)
		.GetMethod("GetNextLsn", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException("Expected private GetNextLsn method.");

	private static readonly MethodInfo IsEmptyLsnMethod = typeof(CdcProcessor)
		.GetMethod("IsEmptyLsn", BindingFlags.NonPublic | BindingFlags.Static)
		?? throw new InvalidOperationException("Expected private IsEmptyLsn method.");

	private static readonly MethodInfo ByteArrayToHexMethod = typeof(CdcProcessor)
		.GetMethod("ByteArrayToHex", BindingFlags.NonPublic | BindingFlags.Static)
		?? throw new InvalidOperationException("Expected private ByteArrayToHex method.");

	[Fact]
	public void ProcessCdcRecord_CreateInsertDeleteAndMatchedUpdateEvents()
	{
		using var processor = CreateProcessor();
		var state = CreateChangeProcessingState(lsn: [0x10], sequenceValue: [0x01]);
		var events = new List<DataChangeEvent>();

		var insert = CreateRow([0x10], [0x01], CdcOperationCodes.Insert, 1, "Created");
		var delete = CreateRow([0x10], [0x02], CdcOperationCodes.Delete, 2, "Deleted");
		var updateBefore = CreateRow([0x10], [0x03], CdcOperationCodes.UpdateBefore, 3, "Pending");
		var updateAfter = CreateRow([0x10], [0x03], CdcOperationCodes.UpdateAfter, 3, "Completed");

		Invoke(ProcessCdcRecordMethod, processor, insert, events, state);
		Invoke(ProcessCdcRecordMethod, processor, delete, events, state);
		Invoke(ProcessCdcRecordMethod, processor, updateBefore, events, state);
		Invoke(ProcessCdcRecordMethod, processor, updateAfter, events, state);

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
		var state = CreateChangeProcessingState(lsn: [0x20], sequenceValue: [0x07]);
		var events = new List<DataChangeEvent>();

		var updateAfter = CreateRow([0x20], [0x07], CdcOperationCodes.UpdateAfter, 7, "After");
		var updateBefore = CreateRow([0x20], [0x07], CdcOperationCodes.UpdateBefore, 7, "Before");

		Invoke(ProcessCdcRecordMethod, processor, updateAfter, events, state);
		events.ShouldBeEmpty();
		GetPendingUpdateAfter(state).Count.ShouldBe(1);

		Invoke(ProcessCdcRecordMethod, processor, updateBefore, events, state);
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
		var state = CreateChangeProcessingState(lsn: [0x30], sequenceValue: [0x09]);
		var events = new List<DataChangeEvent>();
		var unknown = CreateRow([0x30], [0x09], CdcOperationCodes.Unknown, 9, "Ignored");

		Invoke(ProcessCdcRecordMethod, processor, unknown, events, state);

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
		var state = CreateChangeProcessingState(lsn: [0x50], sequenceValue: [0x0B]);
		var pendingBefore = GetPendingUpdateBefore(state);
		var seq = new byte[] { 0x0B };
		pendingBefore[seq] = CreateRow([0x50], seq, CdcOperationCodes.UpdateBefore, 12, "Unmatched");

		var exception = Should.Throw<UnmatchedUpdateRecordsException>(() =>
			Invoke(ValidateUnmatchedUpdatesMethod, processor, state));

		exception.Lsn.SequenceEqual(new byte[] { 0x50 }).ShouldBeTrue();
	}

	[Fact]
	public void UpdateLsnTracking_MaintainsMinHeapOrderingAndRemoval()
	{
		using var processor = CreateProcessor();
		var tableA = "dbo_orders";
		var tableB = "sales_invoices";
		var lsn1 = new byte[] { 0x01 };
		var lsn2 = new byte[] { 0x02 };
		var lsn3 = new byte[] { 0x03 };

		Invoke(UpdateLsnTrackingMethod, processor, tableA, lsn2, null);
		Invoke(UpdateLsnTrackingMethod, processor, tableB, lsn1, null);
		GetNextLsn(processor).ShouldBe(lsn1);

		// Ensure older LSN does not regress an existing table position.
		Invoke(UpdateLsnTrackingMethod, processor, tableA, lsn1, null);
		GetNextLsn(processor).ShouldBe(lsn1);

		Invoke(UpdateLsnTrackingMethod, processor, tableA, lsn3, null);
		Invoke(UpdateLsnTrackingMethod, processor, tableB, null, null);
		GetNextLsn(processor).ShouldBe(lsn3);
	}

	[Fact]
	public void UpdateLsnAfterProcessing_AdvancesOrRemovesTrackingBasedOnMaxLsn()
	{
		using var processor = CreateProcessor();
		var table = "dbo_shipments";
		var current = new byte[] { 0x10 };
		var next = new byte[] { 0x20 };
		var max = new byte[] { 0x30 };

		Invoke(UpdateLsnTrackingMethod, processor, table, current, null);
		Invoke(UpdateLsnAfterProcessingMethod, processor, table, next, max);
		GetNextLsn(processor).ShouldBe(next);

		Invoke(UpdateLsnAfterProcessingMethod, processor, table, max, max);
		GetNextLsn(processor).ShouldBeNull();
	}

	[Fact]
	public void PrivateHelpers_ConvertLsnAndDetectEmptyValues()
	{
		var allZero = new byte[] { 0x00, 0x00, 0x00 };
		var nonZero = new byte[] { 0x00, 0x01, 0x00 };

		((bool)InvokeStatic(IsEmptyLsnMethod, allZero)!).ShouldBeTrue();
		((bool)InvokeStatic(IsEmptyLsnMethod, nonZero)!).ShouldBeFalse();
		((string)InvokeStatic(ByteArrayToHexMethod, new byte[] { 0x12, 0xAB })!).ShouldBe("0x12AB");
	}

	private static CdcProcessor CreateProcessor()
	{
		var appLifetime = A.Fake<IHostApplicationLifetime>();
		var dbConfig = A.Fake<IDatabaseConfig>();
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
			new SqlConnection("Server=localhost;Database=master;Encrypt=false;TrustServerCertificate=true"),
			new SqlConnection("Server=localhost;Database=master;Encrypt=false;TrustServerCertificate=true"),
			stateStoreOptions: null,
			policyFactory,
			logger);
	}

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

	private static byte[]? GetNextLsn(CdcProcessor processor) =>
		(byte[]?)Invoke(GetNextLsnMethod, processor);

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
