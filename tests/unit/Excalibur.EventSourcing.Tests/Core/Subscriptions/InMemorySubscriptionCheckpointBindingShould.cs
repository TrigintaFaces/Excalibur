// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Subscriptions;

namespace Excalibur.EventSourcing.Tests.Core.Subscriptions;

[Trait("Category", "Unit")]
[Trait("Component", "EventStore")]
[Trait("Pattern", "Conformance")]
public sealed class InMemorySubscriptionCheckpointBindingShould
{
	[Fact]
	public async Task BindAndJoinWithoutCreatingACheckpoint()
	{
		var store = new InMemorySubscriptionCheckpointStore();
		await store.BindSubscriptionAsync("sub", new string('A', 64), CancellationToken.None);
		await store.BindSubscriptionAsync("sub", new string('A', 64), CancellationToken.None);
		(await store.GetCheckpointAsync("sub", CancellationToken.None)).ShouldBeNull();
		(await store.EnumerateCheckpointsAsync(CancellationToken.None)).ShouldBeEmpty();
	}

	[Fact]
	public async Task RefuseADifferentDefinitionAndKeepTheOriginal()
	{
		var store = new InMemorySubscriptionCheckpointStore();
		await store.BindSubscriptionAsync("sub", new string('A', 64), CancellationToken.None);
		await store.AdvanceCheckpointAsync("sub", null, 7, CancellationToken.None);
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => store.BindSubscriptionAsync("sub", new string('B', 64), CancellationToken.None));
		exception.Message.ShouldContain("sub");
		await store.BindSubscriptionAsync("sub", new string('A', 64), CancellationToken.None);
		(await store.GetCheckpointAsync("sub", CancellationToken.None)).ShouldBe(7);
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	[InlineData("A")]
	[InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
	[InlineData("GGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGG")]
	public async Task RejectInvalidHashesBeforeStorageAccess(string hash)
	{
		ISubscriptionCheckpointStore[] stores =
		[
			new InMemorySubscriptionCheckpointStore(),
			new global::Excalibur.EventSourcing.SqlServer.SqlServerSubscriptionCheckpointStore(() => throw new InvalidOperationException("Storage accessed")),
			new global::Excalibur.EventSourcing.Postgres.PostgresSubscriptionCheckpointStore(() => throw new InvalidOperationException("Storage accessed")),
			new global::Excalibur.EventSourcing.Oracle.OracleSubscriptionCheckpointStore(() => throw new InvalidOperationException("Storage accessed")),
			new global::Excalibur.EventSourcing.Sqlite.SqliteSubscriptionCheckpointStore("InvalidKeyword=invalid"),
		];
		foreach (var store in stores)
		{
			await Should.ThrowAsync<ArgumentException>(() => store.BindSubscriptionAsync("sub", hash, CancellationToken.None));
		}
	}
}
