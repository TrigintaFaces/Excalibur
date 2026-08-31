// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.ErrorHandling;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Testing.Conformance;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.DeadLetter;

/// <summary>
/// Runs the shipped dead-letter conformance kit against the REAL Postgres dead-letter store.
/// </summary>
/// <remarks>
/// <para>
/// Until this class and its SQL Server twin existed, every type deriving
/// <see cref="DeadLetterStoreConformanceTestKit"/> bound an in-memory store or a decorator over one, so
/// the whole kit ran against implementations with no SQL in them. <c>PostgresDeadLetterStore</c> had no
/// test of any kind.
/// </para>
/// <para>
/// The table is provisioned from the script the package ships, not from a definition restated here --
/// this store never creates its table at runtime, so the shipped script is the only thing a consumer
/// has, and a suite built on anything else would certify a schema nobody can obtain.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait("Database", "Postgres")]
[Trait("Pattern", "STORE")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class PostgresDeadLetterStoreConformanceTests : DeadLetterStoreConformanceTestKit, IAsyncLifetime
{

	/// <summary>
	/// Exposes the kit's own wiring check to the runner. The check is an arm like any other, so a
	/// suite that omits THIS member disables it silently -- the one gap it cannot report itself.
	/// </summary>
	/// <returns>A completed task when every arm in the kit is wired.</returns>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() =>
		ConformanceSuite_ShouldWireEveryArm();

	private readonly PostgresFixture _fixture;

	/// <summary>Initializes a new instance of the <see cref="PostgresDeadLetterStoreConformanceTests"/> class.</summary>
	/// <param name="fixture">The shared Postgres container.</param>
	public PostgresDeadLetterStoreConformanceTests(PostgresFixture fixture) => _fixture = fixture;

	/// <inheritdoc />
	public async ValueTask InitializeAsync() =>
		await ShippedDeadLetterSchema.ProvisionPostgresAsync(_fixture.ConnectionString, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

	/// <inheritdoc />
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	/// <inheritdoc />
	/// <remarks>
	/// The kit's context is passed straight through, never copied or wrapped: the kit switches the
	/// ambient tenant on one store instance, and substituting a fixed context here would give each
	/// partition its own store, so the isolation arm would pass by instance separation alone.
	/// </remarks>
	protected override IDeadLetterStore CreateStore(ITenantContext ambientTenant) =>
		new PostgresDeadLetterStore(
			Microsoft.Extensions.Options.Options.Create(
				new PostgresDeadLetterOptions { ConnectionString = _fixture.ConnectionString }),
			ambientTenant,
			EnabledTestLogger.Create<PostgresDeadLetterStore>());

	/// <inheritdoc />
	/// <remarks>
	/// Arms share one container and one table, so a row left by an earlier arm is visible to the next.
	/// The count and pagination arms fail against that residue, which is what makes clearing it a
	/// correctness requirement rather than tidiness.
	/// </remarks>
	protected override Task CleanupAsync() =>
		ShippedDeadLetterSchema.TruncatePostgresAsync(_fixture.ConnectionString, TestContext.Current.CancellationToken);

	#region Store

	/// <summary>A stored message is persisted.</summary>
	[Fact]
	public Task StoreAsync_ShouldPersistMessage_Test() => StoreAsync_ShouldPersistMessage();

	/// <summary>A non-empty property bag round-trips intact.</summary>
	[Fact]
	public Task StoreAsync_ShouldRoundTripPropertyBag_Test() => StoreAsync_ShouldRoundTripPropertyBag();

	/// <summary>A null message is rejected.</summary>
	[Fact]
	public Task StoreAsync_WithNullMessage_ShouldThrow_Test() => StoreAsync_WithNullMessage_ShouldThrow();

	/// <summary>Every message of a batch is persisted.</summary>
	[Fact]
	public Task StoreAsync_MultipleMessages_ShouldPersistAll_Test() => StoreAsync_MultipleMessages_ShouldPersistAll();

	#endregion Store

	#region Retrieval

	/// <summary>An empty store returns no messages.</summary>
	[Fact]
	public Task GetMessagesAsync_EmptyStore_ShouldReturnEmpty_Test() => GetMessagesAsync_EmptyStore_ShouldReturnEmpty();

	/// <summary>Messages filter by type.</summary>
	[Fact]
	public Task GetMessagesAsync_FilterByMessageType_ShouldFilter_Test() => GetMessagesAsync_FilterByMessageType_ShouldFilter();

	/// <summary>Paging respects the requested maximum.</summary>
	[Fact]
	public Task GetMessagesAsync_Pagination_ShouldRespectMaxResults_Test() => GetMessagesAsync_Pagination_ShouldRespectMaxResults();

	/// <summary>Lookup is by message id.</summary>
	[Fact]
	public Task GetByIdAsync_ShouldReturnMessageByMessageId_Test() => GetByIdAsync_ShouldReturnMessageByMessageId();

	/// <summary>An unknown id returns null.</summary>
	[Fact]
	public Task GetByIdAsync_NonExistent_ShouldReturnNull_Test() => GetByIdAsync_NonExistent_ShouldReturnNull();

	#endregion Retrieval

	#region Counting

	/// <summary>An empty store counts zero.</summary>
	[Fact]
	public Task GetCountAsync_EmptyStore_ShouldReturnZero_Test() => GetCountAsync_EmptyStore_ShouldReturnZero();

	/// <summary>The count follows the stores.</summary>
	[Fact]
	public Task GetCountAsync_AfterStores_ShouldReturnCorrectCount_Test() => GetCountAsync_AfterStores_ShouldReturnCorrectCount();

	#endregion Counting

	#region Replay

	/// <summary>Replay marks the message replayed.</summary>
	[Fact]
	public Task MarkAsReplayedAsync_ShouldSetIsReplayedTrue_Test() => MarkAsReplayedAsync_ShouldSetIsReplayedTrue();

	/// <summary>Replaying twice is idempotent.</summary>
	[Fact]
	public Task MarkAsReplayedAsync_AlreadyReplayed_ShouldBeIdempotent_Test() => MarkAsReplayedAsync_AlreadyReplayed_ShouldBeIdempotent();

	/// <summary>Replaying an unknown message is idempotent.</summary>
	[Fact]
	public Task MarkAsReplayedAsync_NonExistent_ShouldBeIdempotent_Test() => MarkAsReplayedAsync_NonExistent_ShouldBeIdempotent();

	#endregion Replay

	#region Delete

	/// <summary>Delete removes the message and reports it.</summary>
	[Fact]
	public Task DeleteAsync_ShouldRemoveAndReturnTrue_Test() => DeleteAsync_ShouldRemoveAndReturnTrue();

	/// <summary>Deleting an unknown message reports false.</summary>
	[Fact]
	public Task DeleteAsync_NonExistent_ShouldReturnFalse_Test() => DeleteAsync_NonExistent_ShouldReturnFalse();

	/// <summary>Delete lowers the count.</summary>
	[Fact]
	public Task DeleteAsync_ShouldDecreaseCount_Test() => DeleteAsync_ShouldDecreaseCount();

	#endregion Delete

	#region Retention

	/// <summary>The sweep removes messages past retention.</summary>
	[Fact]
	public Task CleanupOldMessagesAsync_ShouldRemoveOldMessages_Test() => CleanupOldMessagesAsync_ShouldRemoveOldMessages();

	/// <summary>The sweep keeps messages within retention.</summary>
	[Fact]
	public Task CleanupOldMessagesAsync_ShouldRespectRetention_Test() => CleanupOldMessagesAsync_ShouldRespectRetention();

	#endregion Retention

	#region Tenancy

	/// <summary>A tenant reads its own entry.</summary>
	[Fact]
	public Task TenantScopedRead_MustSeeItsOwnEntry_Test() => TenantScopedRead_MustSeeItsOwnEntry();

	/// <summary>A tenant cannot read another tenant's entry.</summary>
	[Fact]
	public Task TenantScopedRead_MustNotSeeAnotherTenantsEntry_Test() => TenantScopedRead_MustNotSeeAnotherTenantsEntry();

	/// <summary>The untenanted partition round-trips its own entry.</summary>
	[Fact]
	public Task UntenantedPartition_MustRoundTripItsOwnEntry_Test() => UntenantedPartition_MustRoundTripItsOwnEntry();

	#endregion Tenancy

	#region Concurrency

	/// <summary>Concurrent delete and store elect exactly one deleter and lose no stored message.</summary>
	[Fact]
	public Task ConcurrentDeleteAndStore_MustElectExactlyOneDeleter_AndLoseNoStoredMessage_Test() =>
		ConcurrentDeleteAndStore_MustElectExactlyOneDeleter_AndLoseNoStoredMessage();

	#endregion Concurrency
}
