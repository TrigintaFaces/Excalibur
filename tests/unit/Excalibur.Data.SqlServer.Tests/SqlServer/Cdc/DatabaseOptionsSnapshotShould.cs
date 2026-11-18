// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections;
using System.Reflection;
using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;
using Excalibur.Data;
using FakeItEasy;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>Verifies stable CDC execution settings and effective capture-instance mappings.</summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Pattern", "Regression")]
public sealed class DatabaseOptionsSnapshotShould
{
	[Fact]
	public void PreserveOpaqueCaseInsensitiveLookupForConfiguredInstances()
	{
		var source = new MutableOptions
		{
			CaptureInstances = ["ACCOUNTS", "unmapped"],
			Map = new OpaqueMap(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["accounts"] = "dbo.Account",
			}),
		};
		var snapshot = DatabaseOptionsSnapshot.Capture(source);
		snapshot.CaptureInstances.ShouldBe(["ACCOUNTS", "unmapped"]);
		snapshot.CaptureInstanceToTableNameMap["ACCOUNTS"].ShouldBe("dbo.Account");
		snapshot.CaptureInstanceToTableNameMap["unmapped"].ShouldBe("unmapped");
		source.MapReads.ShouldBe(1);
	}

	[Fact]
	public void PreserveDistinctOrdinalCaseKeys()
	{
		var source = new MutableOptions
		{
			CaptureInstances = ["Orders", "orders"],
			Map = new OpaqueMap(new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["Orders"] = "upper", ["orders"] = "lower",
			}),
		};
		var snapshot = DatabaseOptionsSnapshot.Capture(source);
		snapshot.CaptureInstanceToTableNameMap["Orders"].ShouldBe("upper");
		snapshot.CaptureInstanceToTableNameMap["orders"].ShouldBe("lower");
	}

	[Fact]
	public void IsolateCapturedArraysMapsScalarsAndRecovery()
	{
		var map = new Dictionary<string, string>(StringComparer.Ordinal) { ["accounts"] = "dbo.Account" };
		var source = new MutableOptions { Map = map };
		var snapshot = DatabaseOptionsSnapshot.Capture(source);
		var recovery = snapshot.RecoveryOptions.ShouldNotBeNull();
		recovery.ShouldNotBeSameAs(source.RecoveryOptions);
		recovery.OnPositionReset.ShouldBeSameAs(source.RecoveryOptions!.OnPositionReset);
		source.CaptureInstances[0] = "changed";
		map["accounts"] = "changed";
		source.DatabaseName = "changed";
		source.DatabaseConnectionIdentifier = "changed";
		source.StateConnectionIdentifier = "changed";
		source.QueueSize = 1000;
		source.ProducerBatchSize = 1000;
		source.ConsumerBatchSize = 1000;
		source.StopOnMissingTableHandler = false;
		source.RecoveryOptions.RecoveryStrategy = StalePositionRecoveryStrategy.Throw;
		source.RecoveryOptions.MaxRecoveryAttempts = 1000;
		source.RecoveryOptions.RecoveryAttemptDelay = TimeSpan.Zero;
		source.RecoveryOptions.EnableStructuredLogging = false;
		source.RecoveryOptions.OnPositionReset = null;
		snapshot.DatabaseName.ShouldBe("db");
		snapshot.DatabaseConnectionIdentifier.ShouldBe("read");
		snapshot.StateConnectionIdentifier.ShouldBe("state");
		snapshot.QueueSize.ShouldBe(17);
		snapshot.ProducerBatchSize.ShouldBe(11);
		snapshot.ConsumerBatchSize.ShouldBe(7);
		snapshot.StopOnMissingTableHandler.ShouldBeTrue();
		snapshot.CaptureInstances.ShouldBe(["accounts"]);
		snapshot.CaptureInstanceToTableNameMap["accounts"].ShouldBe("dbo.Account");
		recovery.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.FallbackToEarliest);
		recovery.MaxRecoveryAttempts.ShouldBe(8);
		recovery.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromSeconds(4));
		recovery.EnableStructuredLogging.ShouldBeTrue();
		recovery.OnPositionReset.ShouldNotBeNull();
	}

	[Fact]
	public void PreserveNullRecoveryOptions() =>
		DatabaseOptionsSnapshot.Capture(new MutableOptions { RecoveryOptions = null }).RecoveryOptions.ShouldBeNull();

	[Fact]
	public void DeriveMutableConfigurationAfreshWhileKeepingAnExistingSnapshotStable()
	{
		var source = new DatabaseOptions("db", "read", "state")
		{
			Tables = [new CdcTableConfig { TableName = "dbo.Account", CaptureInstance = "accounts" }],
		};
		var snapshot = DatabaseOptionsSnapshot.Capture(source);
		source.Tables[0].TableName = "dbo.UpdatedAccount";
		source.Tables[0].CaptureInstance = "accounts_v2";
		source.Tables.Add(new CdcTableConfig { TableName = "dbo.Order", CaptureInstance = "orders" });
		source.CaptureInstances.ShouldBe(["accounts_v2", "orders"]);
		source.CaptureInstanceToTableNameMap["accounts_v2"].ShouldBe("dbo.UpdatedAccount");
		snapshot.CaptureInstances.ShouldBe(["accounts"]);
		snapshot.CaptureInstanceToTableNameMap["accounts"].ShouldBe("dbo.Account");
	}

	[Fact]
	public async Task CaptureTheActualProcessorConfigurationOnceAtConstruction()
	{
		var source = new MutableOptions();
		await using var processor = new SnapshotProbe(source);
		processor.Configuration.ShouldNotBeSameAs(source);
		source.MapReads.ShouldBe(1);
		source.DatabaseName = "changed";
		source.CaptureInstances[0] = "changed";
		source.RecoveryOptions!.MaxRecoveryAttempts = 999;
		processor.Configuration.DatabaseName.ShouldBe("db");
		processor.Configuration.CaptureInstances.ShouldBe(["accounts"]);
		processor.Configuration.RecoveryOptions!.MaxRecoveryAttempts.ShouldBe(8);
	}

	[Theory]
	[InlineData("_checkpointManager")]
	[InlineData("_changeDetector")]
	[InlineData("_changeApplier")]
	public async Task IsolateEveryComposedWorkerFromLaterConfigurationMutation(string workerField)
	{
		var source = new MutableOptions();
		await using var processor = new SnapshotProbe(source);
		source.DatabaseName = "changed";
		source.ProducerBatchSize = 999;
		source.CaptureInstances[0] = "changed";
		source.Map = new Dictionary<string, string>(StringComparer.Ordinal) { ["accounts"] = "wrong" };
		source.RecoveryOptions!.MaxRecoveryAttempts = 999;

		var worker = processor.WorkerConfiguration(workerField);
		worker.DatabaseName.ShouldBe("db");
		worker.ProducerBatchSize.ShouldBe(11);
		worker.CaptureInstances.ShouldBe(["accounts"]);
		worker.CaptureInstanceToTableNameMap["accounts"].ShouldBe("dbo.Account");
		worker.RecoveryOptions!.MaxRecoveryAttempts.ShouldBe(8);
		worker.ShouldBeSameAs(processor.Configuration);
	}

	private sealed class SnapshotProbe(IDatabaseOptions options) : CdcProcessor(
		A.Fake<IHostApplicationLifetime>(), options,
		new CdcRepository(new SqlConnection()), () => new SqlConnection(), null,
		A.Fake<IDataAccessPolicyFactory>(), TimeProvider.System, NullLogger<CdcProcessor>.Instance)
	{
		internal IDatabaseOptions Configuration => _dbConfig;

		internal IDatabaseOptions WorkerConfiguration(string workerField)
		{
			var worker = typeof(CdcProcessor).GetField(workerField, BindingFlags.Instance | BindingFlags.NonPublic)
				?.GetValue(this) ?? throw new InvalidOperationException($"Missing composed worker {workerField}.");
			return worker.GetType().GetField("_dbConfig", BindingFlags.Instance | BindingFlags.NonPublic)
				?.GetValue(worker) as IDatabaseOptions
				?? throw new InvalidOperationException($"Missing configuration on {workerField}.");
		}
	}

	private sealed class MutableOptions : IDatabaseOptions
	{
		public string DatabaseName { get; set; } = "db";
		public string DatabaseConnectionIdentifier { get; set; } = "read";
		public string StateConnectionIdentifier { get; set; } = "state";
		public bool StopOnMissingTableHandler { get; set; } = true;
		public string[] CaptureInstances { get; set; } = ["accounts"];
		public int QueueSize { get; set; } = 17;
		public int ProducerBatchSize { get; set; } = 11;
		public int ConsumerBatchSize { get; set; } = 7;
		public CdcRecoveryOptions? RecoveryOptions { get; set; } = new()
		{
			MaxRecoveryAttempts = 8,
			RecoveryAttemptDelay = TimeSpan.FromSeconds(4),
			OnPositionReset = static (_, _) => Task.CompletedTask,
		};
		internal IReadOnlyDictionary<string, string> Map { get; set; } =
			new Dictionary<string, string>(StringComparer.Ordinal) { ["accounts"] = "dbo.Account" };
		internal int MapReads { get; private set; }
		public IReadOnlyDictionary<string, string> CaptureInstanceToTableNameMap
		{
			get { MapReads++; return Map; }
		}
	}

	// Deliberately exposes no comparer property; enumerating into an ordinal dictionary loses its semantics.
	private sealed class OpaqueMap(IReadOnlyDictionary<string, string> inner) : IReadOnlyDictionary<string, string>
	{
		public string this[string key] => inner[key];
		public IEnumerable<string> Keys => inner.Keys;
		public IEnumerable<string> Values => inner.Values;
		public int Count => inner.Count;
		public bool ContainsKey(string key) => inner.ContainsKey(key);
		public bool TryGetValue(string key, out string value) => inner.TryGetValue(key, out value!);
		public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => inner.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
