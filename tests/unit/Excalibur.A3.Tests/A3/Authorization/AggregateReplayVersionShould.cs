// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Events;
using Excalibur.A3.Authorization.Grants;
using Excalibur.A3.Authorization.Roles;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;

using Grant = Excalibur.A3.Authorization.Grants.Grant;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Replay-version coverage for the event-sourced A3 Core aggregates (<see cref="Role"/> and
/// <see cref="Grant"/>).
/// </summary>
/// <remarks>
/// <para>
/// These locks bind the <em>property</em> that a multi-event aggregate rehydrates from its stream
/// with the correct applied-event <see cref="AggregateRoot{TKey}.Version"/> — not the mechanism that
/// supplies it. Version is authoritative on the <see cref="HistoricEvent"/> envelope (the durable
/// stream position the store assigned at append), never on the event payload, so an event that only
/// implements <c>IDomainEvent</c> (such as <see cref="Excalibur.A3.Authorization.Roles.Events.RoleCreated"/>)
/// replays identically to one deriving <c>DomainEvent</c> (such as <see cref="GrantAdded"/>).
/// </para>
/// <para>
/// The safety/property arm raises two or more events, replays through the real
/// <see cref="AggregateRoot{TKey}.LoadFromHistory"/> path, and asserts the aggregate returns with the
/// correct state and <c>Version == event count</c>. The liveness arm proves the single-event stream
/// still rehydrates (a boundary a version-stamping regression would leave green while the multi-event
/// case throws).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AggregateReplayVersionShould : UnitTestBase
{
	#region Role

	[Fact]
	public void Role_RehydratesWithCorrectStateAndVersion_AfterMultiEventReplay()
	{
		// Arrange -- raise two events through the real RaiseEvent path.
		var original = new Role("role-1", "Admin", "Desc", "tenant-1", ["GroupA"], null, "creator");
		original.Modify("SuperAdmin", "Updated");

		var uncommitted = original.GetUncommittedEvents();
		uncommitted.Count.ShouldBe(2);

		var history = ToHistory(uncommitted);

		// Act -- replay through the real LoadFromHistory path.
		var rebuilt = Role.FromEvents("role-1", history);

		// Assert -- state rehydrated AND Version reflects the two applied events.
		rebuilt.Name.ShouldBe("SuperAdmin");
		rebuilt.Description.ShouldBe("Updated");
		rebuilt.State.ShouldBe(RoleState.Active);
		rebuilt.Version.ShouldBe(2);
	}

	[Fact]
	public void Role_RehydratesSingleEventStream_WithVersionOne()
	{
		// Arrange -- a single Created event.
		var original = new Role("role-1", "Admin", null, null, ["GroupA"], null, "creator");
		var uncommitted = original.GetUncommittedEvents();
		uncommitted.Count.ShouldBe(1);

		// Act
		var rebuilt = Role.FromEvents("role-1", ToHistory(uncommitted));

		// Assert -- the liveness arm: the one-event case still reloads cleanly.
		rebuilt.Name.ShouldBe("Admin");
		rebuilt.State.ShouldBe(RoleState.Active);
		rebuilt.Version.ShouldBe(1);
	}

	#endregion

	#region Grant (positive control -- events derive DomainEvent)

	[Fact]
	public void Grant_RehydratesWithCorrectStateAndVersion_AfterMultiEventReplay()
	{
		// Arrange -- two events at stream positions 0 and 1.
		var grantedOn = DateTimeOffset.UtcNow.AddMinutes(-10);
		var revokedOn = DateTimeOffset.UtcNow.AddMinutes(-1);

		var added = new GrantAdded(
			"user-1", "John Doe", "TestApp", "tenant-1", "role", "admin",
			null, "admin-user", grantedOn);
		var revoked = new GrantRevoked(
			"user-1", "John Doe", "TestApp", "tenant-1", "role", "admin",
			null, "other-admin", revokedOn);

		// Act
		var rebuilt = Grant.FromEvents(
			"user-1:tenant-1:role:admin",
			[new HistoricEvent(added, 0), new HistoricEvent(revoked, 1)]);

		// Assert -- control is unaffected: state rehydrates AND Version == 2.
		rebuilt.IsRevoked().ShouldBeTrue();
		rebuilt.RevokedBy.ShouldBe("other-admin");
		rebuilt.Version.ShouldBe(2);
	}

	[Fact]
	public void Grant_RehydratesSingleEventStream_WithVersionOne()
	{
		// Arrange
		var added = new GrantAdded(
			"user-1", "John Doe", "TestApp", "tenant-1", "role", "admin",
			null, "admin-user", DateTimeOffset.UtcNow);

		// Act
		var rebuilt = Grant.FromEvents("user-1:tenant-1:role:admin", [new HistoricEvent(added, 0)]);

		// Assert -- liveness arm.
		rebuilt.UserId.ShouldBe("user-1");
		rebuilt.IsRevoked().ShouldBeFalse();
		rebuilt.Version.ShouldBe(1);
	}

	#endregion

	private static HistoricEvent[] ToHistory(IReadOnlyList<IDomainEvent> events)
	{
		// Assign the contiguous, zero-based stream positions a real event store stamps at append time.
		var history = new HistoricEvent[events.Count];
		for (var i = 0; i < events.Count; i++)
		{
			history[i] = new HistoricEvent(events[i], i);
		}

		return history;
	}
}
