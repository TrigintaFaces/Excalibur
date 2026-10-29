// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Subscriptions;

namespace Excalibur.EventSourcing.Tests.Core.Subscriptions;

[Trait("Category", "Unit")]
[Trait("Component", "EventStore")]
[Trait("Pattern", "Conformance")]
public sealed class InMemorySubscriptionCheckpointBindingShould
{
	private static SubscriptionMember Member(string key, char fill, string destination = "dest-a") =>
		new(key, new string(fill, 64), destination);

	[Fact]
	public async Task BindAndJoinWithoutCreatingACheckpoint()
	{
		var store = new InMemorySubscriptionCheckpointStore();
		_ = await store.BindSubscriptionAsync("sub", [Member("Proj.A", 'A')], CancellationToken.None);
		_ = await store.BindSubscriptionAsync("sub", [Member("Proj.A", 'A')], CancellationToken.None);

		(await store.GetCheckpointAsync("sub", "Proj.A", CancellationToken.None)).ShouldBeNull();
		(await store.EnumerateCheckpointsAsync(CancellationToken.None)).ShouldBeEmpty();
	}

	[Fact]
	public async Task RefuseAChangedMemberAndKeepTheOriginal()
	{
		var store = new InMemorySubscriptionCheckpointStore();
		_ = await store.BindSubscriptionAsync("sub", [Member("Proj.A", 'A')], CancellationToken.None);
		_ = await store.AdvanceBoundCheckpointAsync("sub", "Proj.A", null, 7, CancellationToken.None);

		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => store.BindSubscriptionAsync("sub", [Member("Proj.A", 'B', "dest-moved")], CancellationToken.None));

		// The message must name the PROJECTION and both destinations. A set-wide fingerprint could only
		// name the subscription, which left a consumer bisecting their own registration list.
		exception.Message.ShouldContain("Proj.A");
		exception.Message.ShouldContain("dest-a");
		exception.Message.ShouldContain("dest-moved");

		_ = await store.BindSubscriptionAsync("sub", [Member("Proj.A", 'A')], CancellationToken.None);
		(await store.GetCheckpointAsync("sub", "Proj.A", CancellationToken.None)).ShouldBe(7);
	}

	/// <summary>
	/// Adding a projection is additive, and a sibling's cursor is observably untouched.
	/// </summary>
	/// <remarks>
	/// The LIVENESS arm for the refusal above. A store that refused every bind would satisfy
	/// <see cref="RefuseAChangedMemberAndKeepTheOriginal"/> completely, so the permitted change must be
	/// asserted too or an inert implementation passes.
	/// </remarks>
	[Fact]
	public async Task AcceptANewMemberWithoutDisturbingASibling()
	{
		var store = new InMemorySubscriptionCheckpointStore();
		_ = await store.BindSubscriptionAsync("sub", [Member("Proj.A", 'A')], CancellationToken.None);
		_ = await store.AdvanceBoundCheckpointAsync("sub", "Proj.A", null, 41, CancellationToken.None);

		var roster = await store.BindSubscriptionAsync(
			"sub", [Member("Proj.A", 'A'), Member("Proj.B", 'B')], CancellationToken.None);

		roster.Members.Count.ShouldBe(2);
		(await store.GetCheckpointAsync("sub", "Proj.A", CancellationToken.None)).ShouldBe(41);
	}

	/// <summary>A released member takes its checkpoint with it, atomically.</summary>
	[Fact]
	public async Task ReleaseAMemberWithItsCheckpointAndLeaveSiblingsAlone()
	{
		var store = new InMemorySubscriptionCheckpointStore();
		_ = await store.BindSubscriptionAsync(
			"sub", [Member("Proj.A", 'A'), Member("Proj.B", 'B')], CancellationToken.None);
		_ = await store.AdvanceBoundCheckpointAsync("sub", "Proj.A", null, 11, CancellationToken.None);
		_ = await store.AdvanceBoundCheckpointAsync("sub", "Proj.B", null, 22, CancellationToken.None);

		(await store.ReleaseBoundMemberAsync("sub", "Proj.A", CancellationToken.None)).ShouldBeTrue();

		(await store.GetCheckpointAsync("sub", "Proj.A", CancellationToken.None)).ShouldBeNull();
		(await store.GetCheckpointAsync("sub", "Proj.B", CancellationToken.None)).ShouldBe(22);
	}

	/// <summary>Releasing an unbound member reports false and writes nothing.</summary>
	[Fact]
	public async Task ReportFalseAndWriteNothingWhenReleasingAnUnboundMember()
	{
		var store = new InMemorySubscriptionCheckpointStore();
		_ = await store.BindSubscriptionAsync("sub", [Member("Proj.A", 'A')], CancellationToken.None);
		_ = await store.AdvanceBoundCheckpointAsync("sub", "Proj.A", null, 5, CancellationToken.None);

		(await store.ReleaseBoundMemberAsync("sub", "Proj.Absent", CancellationToken.None)).ShouldBeFalse();
		(await store.GetCheckpointAsync("sub", "Proj.A", CancellationToken.None)).ShouldBe(5);
	}

	/// <summary>
	/// The roster returns members the caller did not supply, which is how a process discovers work it
	/// cannot attest.
	/// </summary>
	[Fact]
	public async Task ReturnMembersTheCallerDidNotSupply()
	{
		var store = new InMemorySubscriptionCheckpointStore();
		_ = await store.BindSubscriptionAsync(
			"sub", [Member("Proj.A", 'A'), Member("Proj.B", 'B')], CancellationToken.None);

		var roster = await store.BindSubscriptionAsync("sub", [Member("Proj.A", 'A')], CancellationToken.None);

		roster.Members.Select(member => member.ProjectionKey).ShouldContain("Proj.B");
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	[InlineData("A")]
	[InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
	[InlineData("GGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGG")]
	public async Task RejectInvalidFingerprintsBeforeStorageAccess(string hash)
	{
		// Every store is constructed so that TOUCHING storage throws a distinguishable exception. The
		// assertion is ArgumentException, so a store that validated after opening a connection fails this
		// arm rather than passing it -- which is the property, not a side effect of ordering.
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
			_ = await Should.ThrowAsync<ArgumentException>(
				() => store.BindSubscriptionAsync("sub", [new SubscriptionMember("Proj.A", hash, "dest-a")], CancellationToken.None));
		}
	}

	/// <summary>A duplicate projection key in one roster is refused before storage access.</summary>
	[Fact]
	public async Task RejectADuplicateProjectionKeyBeforeStorageAccess()
	{
		var store = new InMemorySubscriptionCheckpointStore();

		_ = await Should.ThrowAsync<ArgumentException>(
			() => store.BindSubscriptionAsync(
				"sub", [Member("Proj.A", 'A'), Member("Proj.A", 'B')], CancellationToken.None));
	}
}
