// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;
using System.Threading.Channels;

using Excalibur.Cdc.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;

using Polly;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Locks that the CDC change PRODUCER never writes the durable resume position.
/// </summary>
/// <remarks>
/// <para>
/// The producer runs ahead of delivery through a channel. A durable write from the producer can therefore
/// land past changes that have been fetched but not yet delivered, and a crash before they are delivered
/// resumes past them. The reachable form: a stale-position recovery moves every capture instance forward,
/// the producer reads zero rows at the new position and persisted it, and a change still in the channel for
/// that instance was lost. The durable position is written only by the delivery path; that path's own
/// liveness is locked by <c>CdcFailedChangeBarrierSpansTheRunShould.StillCheckpointAnUnrelatedTableAfterAnotherTableFails</c>.
/// </para>
/// <para>
/// Two arms, because they fail differently. The structural arm fails if the producer is ever handed a
/// dependency that can reach the durable writer, whether or not any current code path calls it. The
/// behavioural arm fails if a producer run performs a durable write, on the zero-row path where the write
/// used to happen.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class CdcProducerNeverWritesDurableStateShould : UnitTestBase
{
	private const string CaptureInstance = "dbo_Orders";

	private static readonly byte[] StaleLsn = [0x00, 0x00, 0x00, 0x01];
	private static readonly byte[] MinLsn = [0x00, 0x00, 0x00, 0x05];
	private static readonly byte[] MaxLsn = [0x00, 0x00, 0x00, 0x0A];

	// Every member, on any type in this package, that writes the durable resume position.
	private static readonly string[] DurableWriters =
	[
		"UpdateTableLastProcessedAsync",
		"UpdateLastProcessedPositionAsync",
	];

	/// <summary>
	/// STRUCTURAL: nothing the producer holds, or is constructed from, exposes a durable writer.
	/// </summary>
	[Fact]
	public void HoldNoDependencyThatCanWriteTheDurablePosition()
	{
		const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		var producer = typeof(CdcChangeDetector);

		var fieldTypes = producer.GetFields(Instance).Select(static f => f.FieldType);
		var ctorParameterTypes = producer.GetConstructors(Instance)
			.SelectMany(static c => c.GetParameters())
			.Select(static p => p.ParameterType);

		var offending = fieldTypes.Concat(ctorParameterTypes)
			.Distinct()
			.Where(ExposesADurableWriter)
			.Select(static t => t.FullName)
			.ToList();

		offending.ShouldBeEmpty(
			"the producer runs ahead of delivery, so it must not hold anything that can write the durable resume position");
	}

	/// <summary>
	/// BEHAVIOURAL: a producer run that reads zero rows performs no durable write.
	/// </summary>
	/// <remarks>
	/// The capture instance starts below the retention boundary, so the producer clamps it forward and then
	/// reads zero rows there: the path on which the durable write used to happen.
	/// </remarks>
	[Fact]
	public async Task WriteNoDurablePositionWhenAFetchReturnsZeroRows()
	{
		var cdcRepository = A.Fake<ICdcRepository>();
		var cdcLsnMapping = A.Fake<ICdcRepositoryLsnMapping>();
		var stateStore = A.Fake<ISqlServerCdcStateStore>();
		var logger = NullLogger<CdcProducerNeverWritesDurableStateShould>.Instance;
		var dbConfig = CreateDbConfig();

		A.CallTo(() => cdcRepository.GetMaxPositionAsync(A<CancellationToken>._)).Returns(MaxLsn);
		A.CallTo(() => cdcRepository.GetMinPositionAsync(CaptureInstance, A<CancellationToken>._)).Returns(MinLsn);
		A.CallTo(() => cdcRepository.FetchChangesAsync(
				CaptureInstance, A<int>._, A<byte[]>._, A<byte[]>._, A<byte[]?>._,
				A<CdcOperationCodes>._, A<CancellationToken>._, A<string?>._))
			.Returns(Task.FromResult<IEnumerable<CdcRow>>([]));
		A.CallTo(() => cdcLsnMapping.GetLsnToTimeAsync(A<byte[]>._, A<CancellationToken>._))
			.Returns(Task.FromResult<DateTime?>(DateTime.UtcNow));
		A.CallTo(() => cdcLsnMapping.GetNextLsnAsync(CaptureInstance, A<byte[]>._, A<CancellationToken>._))
			.Returns(Task.FromResult<byte[]?>(null));
		A.CallTo(() => stateStore.UpdateLastProcessedPositionAsync(
				A<string>._, A<string>._, A<string>._, A<byte[]>._, A<byte[]?>._,
				A<DateTime?>._, A<long?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(1));

		var checkpointManager = new CdcCheckpointManager(dbConfig, cdcRepository, stateStore, logger);
		checkpointManager.UpdateLsnTracking(CaptureInstance, StaleLsn, seqVal: null);

		var producer = new CdcChangeDetector(
			cdcRepository, cdcLsnMapping, dbConfig, CreatePolicyFactory(), checkpointManager, logger);

		await producer.ProducerLoopCoreAsync(
				StaleLsn, Channel.CreateUnbounded<DataChangeEvent>().Writer, queueSize: 32, CancellationToken.None)
			.ConfigureAwait(false);

		// CONTROL: the run really reached the zero-row path. Without this, a producer that never fetched
		// would satisfy the assertion below while exercising nothing.
		A.CallTo(() => cdcRepository.FetchChangesAsync(
				CaptureInstance, A<int>._, A<byte[]>._, A<byte[]>._, A<byte[]?>._,
				A<CdcOperationCodes>._, A<CancellationToken>._, A<string?>._))
			.MustHaveHappened();

		A.CallTo(() => stateStore.UpdateLastProcessedPositionAsync(
				A<string>._, A<string>._, A<string>._, A<byte[]>._, A<byte[]?>._,
				A<DateTime?>._, A<long?>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	private static bool ExposesADurableWriter(Type type)
	{
		const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		return type.GetMethods(All)
			.Concat(type.GetInterfaces().SelectMany(static i => i.GetMethods()))
			.Any(static m => DurableWriters.Contains(m.Name, StringComparer.Ordinal));
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
