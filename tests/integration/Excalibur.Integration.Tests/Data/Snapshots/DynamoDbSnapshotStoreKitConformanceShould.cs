// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb;
using Excalibur.Data.DynamoDb.Snapshots;
using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Runs the <strong>shipped</strong> <see cref="SnapshotStoreConformanceTestKit"/> against a real
/// DynamoDB-on-LocalStack container, through the same <see cref="DynamoDbSnapshotStore"/> a consumer
/// constructs.
/// </summary>
/// <remarks>
/// See <c>PostgresSnapshotStoreKitConformanceShould</c> for why the internal
/// <c>DynamoDbSnapshotStoreConformanceShould</c> in this directory does not exercise the published
/// contract. This suite closes the same gap for DynamoDB, reusing the existing
/// <see cref="DynamoDbSnapshotStoreContainerFixture"/>. The kit's large-payload arm writes 100,000
/// bytes, comfortably inside DynamoDB's 400 KB item cap once keys and encoding overhead are counted, so
/// no payload-size override is needed here (unlike the internal base, which raises its own cap to
/// 200,000 bytes for a different, larger fixture payload).
/// </remarks>
[Collection(DynamoDbSnapshotStoreTestCollection.CollectionName)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "DynamoDb")]
[Trait("Pattern", "STORE")]
public sealed class DynamoDbSnapshotStoreKitConformanceShould : SnapshotStoreConformanceTestKit,
	IClassFixture<DynamoDbSnapshotStoreContainerFixture>, IAsyncLifetime
{
	private readonly DynamoDbSnapshotStoreContainerFixture _fixture;

	/// <summary>Initializes a new instance of the <see cref="DynamoDbSnapshotStoreKitConformanceShould"/> class.</summary>
	/// <param name="fixture">The shared DynamoDB (LocalStack) container.</param>
	public DynamoDbSnapshotStoreKitConformanceShould(DynamoDbSnapshotStoreContainerFixture fixture) => _fixture = fixture;

	/// <summary>Confirms the container is up before any arm resolves the store.</summary>
	public ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"DynamoDB (LocalStack) container must be available - real-infra conformance is never skipped.");
		return ValueTask.CompletedTask;
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	/// <inheritdoc />
	/// <remarks>
	/// Built with the client-injecting constructor so reads/writes hit the LocalStack endpoint through
	/// the fixture's default-configured client. <see cref="ConformanceAmbientTenantContext"/> is the
	/// shipped kit helper: without it the store's <c>CurrentTenantScope</c> collapses to <c>None</c> for
	/// every caller, and the kit's tenancy arms -- which switch tenants via
	/// <see cref="TenantContextHolder.BeginScope"/> around one store instance -- would all collide on the
	/// same untenanted row.
	/// </remarks>
	protected override Task<ISnapshotStore> CreateStoreAsync()
	{
		var options = Options.Create(new DynamoDbSnapshotStoreOptions
		{
			Connection = new DynamoDbConnectionOptions
			{
				ServiceUrl = _fixture.ServiceUrl,
				Region = "us-east-1",
				AccessKey = "test",
				SecretKey = "test",
			},
			TableName = _fixture.TableName,
			CreateTableIfNotExists = true,
			UseConsistentReads = true,
		});

		var store = new DynamoDbSnapshotStore(
			_fixture.Client,
			options,
			NullLogger<DynamoDbSnapshotStore>.Instance,
			new ConformanceAmbientTenantContext());

		return Task.FromResult<ISnapshotStore>(store);
	}

	/// <inheritdoc />
	/// <remarks>Each arm uses a fresh aggregate id, so nothing needs to be cleared between arms.</remarks>
	protected override Task CleanupAsync() => Task.CompletedTask;

	[Fact]
	public Task GetLatestSnapshotAsync_NoSnapshot_ShouldReturnNull_Test() => GetLatestSnapshotAsync_NoSnapshot_ShouldReturnNull();

	[Fact]
	public Task SaveAndGetLatestSnapshot_ShouldRoundTrip_Test() => SaveAndGetLatestSnapshot_ShouldRoundTrip();

	[Fact]
	public Task GetLatestSnapshot_MultipleVersions_ShouldReturnLatest_Test() => GetLatestSnapshot_MultipleVersions_ShouldReturnLatest();

	[Fact]
	public Task SaveSnapshot_ShouldUpdateLatest_Test() => SaveSnapshot_ShouldUpdateLatest();

	[Fact]
	public Task SaveSnapshot_StaleOrEqualVersion_ShouldBeASuccessfulNoOp_Test() => SaveSnapshot_StaleOrEqualVersion_ShouldBeASuccessfulNoOp();

	[Fact]
	public Task DeleteSnapshots_ShouldRemoveAll_Test() => DeleteSnapshots_ShouldRemoveAll();

	[Fact]
	public Task DeleteSnapshotsOlderThan_ShouldPreserveNewer_Test() => DeleteSnapshotsOlderThan_ShouldPreserveNewer();

	[Fact]
	public Task DeleteSnapshots_NonExistent_ShouldNotThrow_Test() => DeleteSnapshots_NonExistent_ShouldNotThrow();

	[Fact]
	public Task Snapshots_ShouldIsolateByAggregateType_Test() => Snapshots_ShouldIsolateByAggregateType();

	[Fact]
	public Task Snapshots_ShouldIsolateByAggregateId_Test() => Snapshots_ShouldIsolateByAggregateId();

	[Fact]
	public Task DeleteSnapshots_ShouldNotAffectOtherAggregates_Test() => DeleteSnapshots_ShouldNotAffectOtherAggregates();

	[Fact]
	public Task SaveAndLoad_ShouldPreserveData_Test() => SaveAndLoad_ShouldPreserveData();

	[Fact]
	public Task Snapshots_HigherVersionFromAnotherTenant_MustNotOverwrite_Test() => Snapshots_HigherVersionFromAnotherTenant_MustNotOverwrite();

	[Fact]
	public Task Snapshots_SaveBehindAnotherTenantsVersion_MustNotBeDiscarded_Test() => Snapshots_SaveBehindAnotherTenantsVersion_MustNotBeDiscarded();

	[Fact]
	public Task Snapshots_EachTenant_MustReadItsOwnForASharedAggregateId_Test() => Snapshots_EachTenant_MustReadItsOwnForASharedAggregateId();

	[Fact]
	public Task SaveSnapshot_ConcurrentRisingVersions_ShouldLeaveTheHighestReadable_Test() => SaveSnapshot_ConcurrentRisingVersions_ShouldLeaveTheHighestReadable();

	[Fact]
	public Task GetLatestSnapshot_ConcurrentReaders_ShouldAllObserveIt_Test() => GetLatestSnapshot_ConcurrentReaders_ShouldAllObserveIt();

	[Fact]
	public Task Store_ShouldNotFaultWhenManyCallersArriveAtOnce_Test() => Store_ShouldNotFaultWhenManyCallersArriveAtOnce();

	[Fact]
	public Task SaveAndLoad_EmptyPayload_ShouldRoundTripAsEmpty_Test() => SaveAndLoad_EmptyPayload_ShouldRoundTripAsEmpty();

	[Fact]
	public Task SaveAndLoad_LargePayload_ShouldRoundTripByteForByte_Test() => SaveAndLoad_LargePayload_ShouldRoundTripByteForByte();

	[Fact]
	public Task SaveAndLoad_AggregateIdWithSeparatorCharacters_ShouldRoundTrip_Test() => SaveAndLoad_AggregateIdWithSeparatorCharacters_ShouldRoundTrip();

	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();
}
