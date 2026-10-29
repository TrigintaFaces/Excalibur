// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Subscriptions;

namespace Excalibur.EventSourcing.Tests.Core.Subscriptions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class InMemorySubscriptionCheckpointStoreShould
{
	[Theory]
	[InlineData(null, -1L, "newPosition")]
	[InlineData(-1L, 0L, "expectedPosition")]
	[InlineData(10L, 5L, "newPosition")]
	public async Task RejectInvalidPositionsBeforeRelationalStorageAccess(long? expected, long next, string parameter)
	{
		ISubscriptionCheckpointStore[] stores =
		[
			new global::Excalibur.EventSourcing.SqlServer.SqlServerSubscriptionCheckpointStore(() => throw new InvalidOperationException("Connection factory invoked")),
			new global::Excalibur.EventSourcing.Postgres.PostgresSubscriptionCheckpointStore(() => throw new InvalidOperationException("Connection factory invoked")),
			new global::Excalibur.EventSourcing.Oracle.OracleSubscriptionCheckpointStore(() => throw new InvalidOperationException("Connection factory invoked")),
			new global::Excalibur.EventSourcing.Sqlite.SqliteSubscriptionCheckpointStore("InvalidKeyword=invalid"),
		];
		foreach (var store in stores)
		{
			var exception = await Should.ThrowAsync<ArgumentOutOfRangeException>(
				() => store.AdvanceCheckpointAsync("sub", "Proj.A", Guid.NewGuid(), expected, next, CancellationToken.None));
			exception.ParamName.ShouldBe(parameter);
		}
	}

	[Theory]
	[InlineData(null, -1L, "newPosition")]
	[InlineData(-1L, 0L, "expectedPosition")]
	[InlineData(10L, 5L, "newPosition")]
	[InlineData(20L, 5L, "newPosition")]
	public async Task RejectInvalidPositionsWithoutChangingCheckpoint(long? expected, long next, string parameter)
	{
		var store = new InMemorySubscriptionCheckpointStore();
		await store.AdvanceBoundCheckpointAsync("existing", "Proj.A", null, 10, CancellationToken.None);
		foreach (var name in new[] { "existing", "absent" })
		{
			var exception = await Should.ThrowAsync<ArgumentOutOfRangeException>(
				() => store.AdvanceBoundCheckpointAsync(name, "Proj.A", expected, next, CancellationToken.None));
			exception.ParamName.ShouldBe(parameter);
		}

		(await store.GetCheckpointAsync("existing", "Proj.A", CancellationToken.None)).ShouldBe(10);
		(await store.GetCheckpointAsync("absent", "Proj.A", CancellationToken.None)).ShouldBeNull();
	}

	[Fact]
	public async Task AcceptEqualPositionsOnlyWhileTheExpectationMatches()
	{
		var store = new InMemorySubscriptionCheckpointStore();
		(await store.AdvanceBoundCheckpointAsync("sub", "Proj.A", null, 0, CancellationToken.None)).ShouldBe(CheckpointAdvanceOutcome.Advanced);
		(await store.AdvanceBoundCheckpointAsync("sub", "Proj.A", 0, 0, CancellationToken.None)).ShouldBe(CheckpointAdvanceOutcome.Advanced);
		(await store.AdvanceBoundCheckpointAsync("sub", "Proj.A", 0, long.MaxValue, CancellationToken.None)).ShouldBe(CheckpointAdvanceOutcome.Advanced);
		(await store.AdvanceBoundCheckpointAsync("sub", "Proj.A", 0, 0, CancellationToken.None)).ShouldBe(CheckpointAdvanceOutcome.Superseded);
		(await store.AdvanceBoundCheckpointAsync("sub", "Proj.A", long.MaxValue, long.MaxValue, CancellationToken.None)).ShouldBe(CheckpointAdvanceOutcome.Advanced);
		(await store.GetCheckpointAsync("sub", "Proj.A", CancellationToken.None)).ShouldBe(long.MaxValue);
	}

	[Fact]
	public async Task ReturnNullForUnknownSubscription()
	{
		// Arrange
		var store = new InMemorySubscriptionCheckpointStore();

		// Act
		var result = await store.GetCheckpointAsync("unknown", "Proj.A", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task StoreAndRetrieveCheckpoint()
	{
		// Arrange
		var store = new InMemorySubscriptionCheckpointStore();
		await store.AdvanceBoundCheckpointAsync("sub-1", "Proj.A", expectedPosition: null, 42L, CancellationToken.None);

		// Act
		var result = await store.GetCheckpointAsync("sub-1", "Proj.A", CancellationToken.None);

		// Assert
		result.ShouldBe(42L);
	}

	[Fact]
	public async Task AdvanceAnExistingCheckpointWhenTheExpectedPositionStillMatches()
	{
		// Arrange
		var store = new InMemorySubscriptionCheckpointStore();
		await store.AdvanceBoundCheckpointAsync("sub-1", "Proj.A", expectedPosition: null, 10L, CancellationToken.None);
		await store.AdvanceBoundCheckpointAsync("sub-1", "Proj.A", expectedPosition: 10L, 20L, CancellationToken.None);

		// Act
		var result = await store.GetCheckpointAsync("sub-1", "Proj.A", CancellationToken.None);

		// Assert
		result.ShouldBe(20L);
	}

	[Fact]
	public async Task TrackMultipleSubscriptionsIndependently()
	{
		// Arrange
		var store = new InMemorySubscriptionCheckpointStore();
		await store.AdvanceBoundCheckpointAsync("sub-1", "Proj.A", expectedPosition: null, 100L, CancellationToken.None);
		await store.AdvanceBoundCheckpointAsync("sub-2", "Proj.A", expectedPosition: null, 200L, CancellationToken.None);

		// Act & Assert
		(await store.GetCheckpointAsync("sub-1", "Proj.A", CancellationToken.None)).ShouldBe(100L);
		(await store.GetCheckpointAsync("sub-2", "Proj.A", CancellationToken.None)).ShouldBe(200L);
	}

	[Fact]
	public async Task ThrowOnNullOrEmptySubscriptionNameForGet()
	{
		// Arrange
		var store = new InMemorySubscriptionCheckpointStore();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => store.GetCheckpointAsync(null!, "Proj.A", CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(
			() => store.GetCheckpointAsync("", "Proj.A", CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullOrEmptySubscriptionNameForAdvance()
	{
		// Arrange
		var store = new InMemorySubscriptionCheckpointStore();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => store.AdvanceBoundCheckpointAsync(null!, "Proj.A", null, 1L, CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(
			() => store.AdvanceBoundCheckpointAsync("", "Proj.A", null, 1L, CancellationToken.None));
	}

	[Fact]
	public async Task RefuseAnAdvanceWhoseExpectedPositionIsStale()
	{
		var store = new InMemorySubscriptionCheckpointStore();
		_ = await store.AdvanceBoundCheckpointAsync("sub-1", "Proj.A", expectedPosition: null, 10L, CancellationToken.None);
		_ = await store.AdvanceBoundCheckpointAsync("sub-1", "Proj.A", expectedPosition: 10L, 20L, CancellationToken.None);

		// A second reader still believes the checkpoint is at 10. Under a blind write it would drag the
		// mark back from 20 to 15 and every event in between would be delivered again.
		var outcome = await store.AdvanceBoundCheckpointAsync("sub-1", "Proj.A", expectedPosition: 10L, 15L, CancellationToken.None);

		outcome.ShouldBe(
			CheckpointAdvanceOutcome.Superseded,
			"an advance from a stale expected position must be refused, not applied");

		(await store.GetCheckpointAsync("sub-1", "Proj.A", CancellationToken.None)).ShouldBe(
			20L,
			"a refused advance must leave the stored position untouched -- reporting the refusal is not " +
			"enough if the write happened anyway");
	}

	[Fact]
	public async Task RefuseACreateWhenACheckpointAlreadyExists()
	{
		var store = new InMemorySubscriptionCheckpointStore();
		_ = await store.AdvanceBoundCheckpointAsync("sub-1", "Proj.A", expectedPosition: null, 10L, CancellationToken.None);

		// "No checkpoint yet" is a distinct prior state, so a caller holding that belief must lose to the
		// writer that created one. A store treating null as "overwrite whatever is there" would pass the
		// stale-expected arm above and still clobber here.
		var outcome = await store.AdvanceBoundCheckpointAsync("sub-1", "Proj.A", expectedPosition: null, 5L, CancellationToken.None);

		outcome.ShouldBe(CheckpointAdvanceOutcome.Superseded);
		(await store.GetCheckpointAsync("sub-1", "Proj.A", CancellationToken.None)).ShouldBe(10L);
	}

	[Fact]
	public async Task ReportAdvancedWhenTheExpectedPositionMatches()
	{
		// LIVENESS. Without this, a store that refused EVERY advance would satisfy both arms above.
		var store = new InMemorySubscriptionCheckpointStore();

		var created = await store.AdvanceBoundCheckpointAsync("sub-1", "Proj.A", expectedPosition: null, 10L, CancellationToken.None);
		var moved = await store.AdvanceBoundCheckpointAsync("sub-1", "Proj.A", expectedPosition: 10L, 20L, CancellationToken.None);

		created.ShouldBe(CheckpointAdvanceOutcome.Advanced);
		moved.ShouldBe(CheckpointAdvanceOutcome.Advanced);
		(await store.GetCheckpointAsync("sub-1", "Proj.A", CancellationToken.None)).ShouldBe(20L);
	}


	[Fact]
	public void ImplementISubscriptionCheckpointStore()
	{
		// Arrange & Act
		var store = new InMemorySubscriptionCheckpointStore();

		// Assert
		store.ShouldBeAssignableTo<ISubscriptionCheckpointStore>();
	}
    [Fact]
    public async Task RejectRetiredWorkBeforeAndAfterRebinding()
    {
        var store = new InMemorySubscriptionCheckpointStore();
        var definition = new SubscriptionMember("A", new string('A', 64), "memory/a");
        var first = (await store.BindSubscriptionAsync("stable", [definition], CancellationToken.None)).Members.Single();
        first.BindingId.ShouldNotBe(Guid.Empty);
        (await store.ReleaseSubscriptionMemberAsync("stable", "A", first.BindingId, CancellationToken.None)).ShouldBeTrue();
        (await store.AdvanceCheckpointAsync("stable", "A", first.BindingId, null, 99, CancellationToken.None))
            .ShouldBe(CheckpointAdvanceOutcome.Superseded);
        (await store.ReadSubscriptionAsync("stable", CancellationToken.None)).Members.ShouldBeEmpty();
        var second = (await store.BindSubscriptionAsync("stable", [definition], CancellationToken.None)).Members.Single();
        second.BindingId.ShouldNotBe(first.BindingId);
        (await store.AdvanceCheckpointAsync("stable", "A", first.BindingId, null, 99, CancellationToken.None))
            .ShouldBe(CheckpointAdvanceOutcome.Superseded);
        (await store.ReleaseSubscriptionMemberAsync("stable", "A", first.BindingId, CancellationToken.None)).ShouldBeFalse();
        (await store.AdvanceCheckpointAsync("stable", "A", second.BindingId, null, 5, CancellationToken.None))
            .ShouldBe(CheckpointAdvanceOutcome.Advanced);
        var snapshot = await store.ReadSubscriptionAsync("stable", CancellationToken.None);
        snapshot.Members.Single().BindingId.ShouldBe(second.BindingId);
        snapshot.Members.Single().Position.ShouldBe(5);
        // A returned snapshot must not change as the store advances.
        await store.AdvanceCheckpointAsync("stable", "A", second.BindingId, 5, 6, CancellationToken.None);
        snapshot.Members.Single().Position.ShouldBe(5);
    }

    [Fact]
    public async Task KeepBothGenerationsUnderTheSameSubscriptionName()
    {
        var store = new InMemorySubscriptionCheckpointStore();
        var firstKey = SubscriptionKeys.ProjectionKey(typeof(SecondGeneration), 1);
        var nextKey = SubscriptionKeys.ProjectionKey(typeof(SecondGeneration));
        nextKey.ShouldBe(firstKey + ":v2");
        var first = (await store.BindSubscriptionAsync("stable",
            [new SubscriptionMember(firstKey, new string('A', 64), "memory/a")], CancellationToken.None)).Members.Single();
        await store.AdvanceCheckpointAsync("stable", firstKey, first.BindingId, null, 100, CancellationToken.None);
        var roster = await store.BindSubscriptionAsync("stable",
            [new SubscriptionMember(nextKey, new string('B', 64), "memory/a-v2")], CancellationToken.None);
        roster.Members.Count.ShouldBe(2);
        roster.Members.Single(m => m.ProjectionKey == firstKey).Position.ShouldBe(100);
        roster.Members.Single(m => m.ProjectionKey == nextKey).Position.ShouldBeNull();
    }

    [ProjectionVersion(2)]
    private sealed class SecondGeneration;

}
