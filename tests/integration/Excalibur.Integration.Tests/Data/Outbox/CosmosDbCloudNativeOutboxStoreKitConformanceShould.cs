// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CloudNative;
using Excalibur.Outbox.CosmosDb;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Binds <see cref="CosmosDbOutboxStore"/> to the SHIPPED <see cref="CloudNativeOutboxStoreConformanceTestKit"/>
/// (ax1nj1) against a live Cosmos DB emulator - the first published contract this family of backends has
/// ever been held to (previously: five hand-written, non-portable suites in this same directory covering
/// change-feed round trip, concurrency tokens, retention, staging, and claim atomicity - real coverage, but
/// invisible to <c>conformance-backend-coverage-gate.sh</c> because no kit existed for the contract).
/// </summary>
/// <remarks>
/// Against the real emulator, not a fake: the atomic-claim arms depend on the emulator's own conditional-
/// write (ETag) semantics, which a mocked <c>Container</c> cannot reproduce. Never skip-gated.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Outbox")]
[Trait("Database", "CosmosDb")]
[Trait("Pattern", "STORE")]
public sealed class CosmosDbCloudNativeOutboxStoreKitConformanceShould
	: CloudNativeOutboxStoreConformanceTestKit, IClassFixture<CosmosDbOutboxStoreContainerFixture>, IAsyncLifetime
{
	private readonly CosmosDbOutboxStoreContainerFixture _fixture;
	private readonly List<(CosmosDbOutboxStore Store, string ContainerName)> _created = [];

	public CosmosDbCloudNativeOutboxStoreKitConformanceShould(CosmosDbOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	public ValueTask InitializeAsync() => ValueTask.CompletedTask;

	public async ValueTask DisposeAsync()
	{
		var containers = new HashSet<string>(StringComparer.Ordinal);
		foreach (var (store, containerName) in _created)
		{
			await store.DisposeAsync().ConfigureAwait(false);
			_ = containers.Add(containerName);
		}

		foreach (var containerName in containers)
		{
			await _fixture.CleanupContainerAsync(containerName).ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	protected override async Task<ICloudNativeOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"The Cosmos DB emulator must be available - real-infra conformance is never skipped.");

		var options = new CosmosDbOutboxOptions
		{
			DatabaseName = _fixture.DatabaseName,
			ContainerName = $"outbox_{Guid.NewGuid():N}",
			CreateContainerIfNotExists = true,
			UseDirectMode = false,
			HttpClientFactory = () => _fixture.EmulatorHttpClient,
			LeaseTimeoutSeconds = 300,
			Connection = new CosmosDbOutboxConnectionOptions { ConnectionString = _fixture.ConnectionString },
		};

		var store = new CosmosDbOutboxStore(Options.Create(options), NullLogger<CosmosDbOutboxStore>.Instance);
		await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
		_created.Add((store, options.ContainerName));
		return store;
	}

	#region Core arms

	[Fact]
	public Task AddAsync_NewMessage_ReturnsSuccessResult_Test() => AddAsync_NewMessage_ReturnsSuccessResult();

	[Fact]
	public Task AddAsync_ThenGetPending_ReturnsTheStagedMessage_Test() => AddAsync_ThenGetPending_ReturnsTheStagedMessage();

	[Fact]
	public Task AddAsync_PreservesCanonicalFields_OnRoundTrip_Test() => AddAsync_PreservesCanonicalFields_OnRoundTrip();

	[Fact]
	public Task GetPendingAsync_EmptyPartition_ReturnsEmpty_Test() => GetPendingAsync_EmptyPartition_ReturnsEmpty();

	[Fact]
	public Task GetPendingAsync_ReturnsMessagesInFifoOrder_Test() => GetPendingAsync_ReturnsMessagesInFifoOrder();

	[Fact]
	public Task MarkAsPublishedAsync_ThenGetPending_ExcludesTheMessage_Test() => MarkAsPublishedAsync_ThenGetPending_ExcludesTheMessage();

	[Fact]
	public Task MarkAsPublishedAsync_UnknownMessage_ReturnsFailureResult_Test() => MarkAsPublishedAsync_UnknownMessage_ReturnsFailureResult();

	#endregion

	#region Batch arms

	[Fact]
	public Task AddBatchAsync_AddsAllMessages_AndTheyAreAllPending_Test() => AddBatchAsync_AddsAllMessages_AndTheyAreAllPending();

	[Fact]
	public Task MarkBatchAsPublishedAsync_MarksAllAsPublished_Test() => MarkBatchAsPublishedAsync_MarksAllAsPublished();

	[Fact]
	public Task CleanupOldMessagesAsync_DeletesOnlyPublishedMessagesOlderThanRetention_Test() => CleanupOldMessagesAsync_DeletesOnlyPublishedMessagesOlderThanRetention();

	[Fact]
	public Task IncrementRetryCountAsync_IncrementsRetryCountAndRecordsError_Test() => IncrementRetryCountAsync_IncrementsRetryCountAndRecordsError();

	[Fact]
	public Task IncrementRetryCountAsync_IsMonotonic_AcrossRepeatedFailures_Test() => IncrementRetryCountAsync_IsMonotonic_AcrossRepeatedFailures();

	#endregion

	#region Claim arms

	[Fact]
	public Task ClaimPendingAsync_ReturnsUnclaimedMessages_UpToBatchSize_Test() => ClaimPendingAsync_ReturnsUnclaimedMessages_UpToBatchSize();

	[Fact]
	public Task ClaimPendingAsync_StampsLeaseOwnerAndInstant_Test() => ClaimPendingAsync_StampsLeaseOwnerAndInstant();

	[Fact]
	public Task ClaimPendingAsync_DoesNotReturnAlreadyClaimedMessages_WithinTheLeaseWindow_Test() => ClaimPendingAsync_DoesNotReturnAlreadyClaimedMessages_WithinTheLeaseWindow();

	[Fact]
	public Task ClaimPendingAsync_ConcurrentClaimants_ReceiveDisjointSets_Test() => ClaimPendingAsync_ConcurrentClaimants_ReceiveDisjointSets();

	#endregion

	/// <summary>The harness guard: fails if this suite has left any kit arm unwired.</summary>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();
}
