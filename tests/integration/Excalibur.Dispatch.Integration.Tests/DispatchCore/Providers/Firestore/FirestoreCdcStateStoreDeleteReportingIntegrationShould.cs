// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Firestore;
using Excalibur.Dispatch;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Firestore;

/// <summary>
/// Real-Firestore-emulator lock on <see cref="ICdcStateStore.DeletePositionAsync"/> reporting whether a
/// checkpoint was actually there to delete.
/// </summary>
/// <remarks>
/// <para>
/// The Firestore store used to await a void-returning delete and return a hardcoded <see langword="true"/>,
/// so it answered "removed one" for a consumer that never had a checkpoint. Every other provider honours
/// the distinction, and a caller branching on the result got a wrong answer from Firestore only, silently.
/// </para>
/// <para>
/// It has to be an emulator lock rather than a unit one. An unconditional Firestore delete succeeds whether
/// or not the document exists, so the answer is the server's to give: the store now sends
/// <c>Precondition.MustExist</c> and reads the rejection. Only a real backend produces that rejection --
/// a fake would be asserting the shape of a call rather than the behaviour of the store.
/// </para>
/// <para>
/// Both arms are here on purpose. The safety arm alone (absent -&gt; false) is satisfied by a store that
/// answers false for everything, which would tell a caller that a checkpoint it just removed was never
/// there. The liveness arm pins the other answer, and the read-back pins that the delete happened at all.
/// </para>
/// <para>
/// Docker is a hard requirement, never skipped, per the real-infrastructure lock bar.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "CDC")]
[Trait("Database", "Firestore")]
[Trait("SubComponent", "StateStoreDeleteReporting")]
[Collection(FirestoreCdcStateStoreTestCollection.CollectionName)]
#pragma warning disable CA1812 // Instantiated by the xUnit test runner.
public sealed class FirestoreCdcStateStoreDeleteReportingIntegrationShould
{
	private const string CollectionPath = "users/orders";

	private readonly FirestoreCdcStateStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the
	/// <see cref="FirestoreCdcStateStoreDeleteReportingIntegrationShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared Firestore-emulator container fixture.</param>
	public FirestoreCdcStateStoreDeleteReportingIntegrationShould(
		FirestoreCdcStateStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ReportFalse_WhenTheConsumerNeverHadACheckpoint()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Docker/Firestore emulator must be available - the CDC delete-reporting contract is a "
			+ "real-infra lock and must never be skipped.");

		await using var store = CreateStore();
		var consumerId = $"proc-{Guid.NewGuid():N}";

		// Nothing was ever saved for this consumer.
		var deleted = await ((ICdcStateStore)store).DeletePositionAsync(
			consumerId, TestContext.Current.CancellationToken);

		deleted.ShouldBeFalse(
			"the shared contract uses this bool to distinguish 'removed one' from 'there was none'.");
	}

	[Fact]
	public async Task ReportTrue_AndActuallyRemoveTheCheckpoint_WhenOneExists()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Docker/Firestore emulator must be available - the CDC delete-reporting contract is a "
			+ "real-infra lock and must never be skipped.");

		await using var store = CreateStore();
		var consumerId = $"proc-{Guid.NewGuid():N}";

		await store.SavePositionAsync(
			consumerId,
			FirestoreCdcPosition.FromUpdateTime(CollectionPath, DateTimeOffset.UtcNow, "doc-1"),
			TestContext.Current.CancellationToken);

		var deleted = await ((ICdcStateStore)store).DeletePositionAsync(
			consumerId, TestContext.Current.CancellationToken);

		deleted.ShouldBeTrue("a checkpoint was there and was removed.");

		// The report is only worth anything if the delete happened. A store that answered true without
		// deleting would pass the line above.
		var stored = await store.GetPositionAsync(consumerId, TestContext.Current.CancellationToken);
		stored.ShouldBeNull();

		// And a second delete of the same consumer is now the absent case.
		var deletedAgain = await ((ICdcStateStore)store).DeletePositionAsync(
			consumerId, TestContext.Current.CancellationToken);

		deletedAgain.ShouldBeFalse("the checkpoint is gone, so there is nothing left to remove.");
	}

	private FirestoreCdcStateStore CreateStore() =>
		new(_fixture.Db, _fixture.CollectionName, NullLogger<FirestoreCdcStateStore>.Instance);
}
#pragma warning restore CA1812
