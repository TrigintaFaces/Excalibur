// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Snapshots;
using Excalibur.EventSourcing.Upcasting;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Upcasting;

/// <summary>
/// Both version managers resolve an upgrade chain through one shared search, so the guarantees they offer
/// must hold identically for events and for snapshots.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class UpgradePathResolutionShould
{
	private const string EventType = "OrderPlaced";
	private const string AggregateType = "Order";

	private readonly EventVersionManager _events = new(NullLogger<EventVersionManager>.Instance);
	private readonly SnapshotVersionManager _snapshots = new([], NullLogger<SnapshotVersionManager>.Instance);

	/// <summary>
	/// A backward or self-referential upgrader can never contribute to a forward chain, so it is a
	/// registration error. Rejecting it at startup replaces a confusing failure during replay, long after
	/// the mistake was made, with an immediate one at the point of the mistake.
	/// </summary>
	[Theory]
	[InlineData(2, 1)]
	[InlineData(1, 1)]
	public void RejectANonForwardEventUpgraderAtRegistration(int from, int to)
	{
		var upgrader = EventUpgrader(from, to);

		_ = Should.Throw<ArgumentException>(() => _events.RegisterUpgrader(upgrader));
	}

	[Theory]
	[InlineData(2, 1)]
	[InlineData(1, 1)]
	public void RejectANonForwardSnapshotUpgraderAtRegistration(int from, int to)
	{
		var upgrader = SnapshotUpgrader(from, to);

		_ = Should.Throw<ArgumentException>(() => _snapshots.RegisterUpgrader(upgrader));
	}

	/// <summary>
	/// Liveness arm: rejecting backward upgraders must not reject forward ones.
	/// </summary>
	[Fact]
	public void StillAcceptAForwardUpgrader()
	{
		Should.NotThrow(() => _events.RegisterUpgrader(EventUpgrader(1, 2)));
		Should.NotThrow(() => _snapshots.RegisterUpgrader(SnapshotUpgrader(1, 2)));
	}

	/// <summary>
	/// Where two chains are equally short, the one chosen must not depend on the order the upgraders were
	/// registered — otherwise replaying the same stream in two processes can apply different upgraders.
	/// </summary>
	[Fact]
	public void ChooseTheSameChainRegardlessOfRegistrationOrder()
	{
		// 1 -> 2 -> 4 and 1 -> 3 -> 4 are both two hops.
		var forward = new SnapshotVersionManager([], NullLogger<SnapshotVersionManager>.Instance);
		foreach (var (from, to) in new[] { (1, 2), (1, 3), (2, 4), (3, 4) })
		{
			forward.RegisterUpgrader(SnapshotUpgrader(from, to));
		}

		var reversed = new SnapshotVersionManager([], NullLogger<SnapshotVersionManager>.Instance);
		foreach (var (from, to) in new[] { (3, 4), (2, 4), (1, 3), (1, 2) })
		{
			reversed.RegisterUpgrader(SnapshotUpgrader(from, to));
		}

		Route(forward, 1, 4).ShouldBe(Route(reversed, 1, 4));
	}

	/// <summary>
	/// A path resolved before a new upgrader is registered must not be served afterwards: the new upgrader
	/// can complete a chain that previously did not exist.
	/// </summary>
	[Fact]
	public void SeeAChainThatBecomesReachableOnlyAfterALaterRegistration()
	{
		_snapshots.RegisterUpgrader(SnapshotUpgrader(1, 2));

		// Asked first, while 2 -> 3 is still missing, so the absent answer is the one cached.
		_snapshots.CanUpgrade(AggregateType, 1, 3).ShouldBeFalse();

		_snapshots.RegisterUpgrader(SnapshotUpgrader(2, 3));

		_snapshots.CanUpgrade(AggregateType, 1, 3).ShouldBeTrue();
	}

	[Fact]
	public void ResolveTheShortestOfTwoUnequalChains()
	{
		foreach (var (from, to) in new[] { (1, 2), (2, 3), (1, 3) })
		{
			_snapshots.RegisterUpgrader(SnapshotUpgrader(from, to));
		}

		// The direct 1 -> 3 edge is one hop; the 1 -> 2 -> 3 chain is two.
		Route(_snapshots, 1, 3).ShouldBe("-3");
	}

	/// <summary>
	/// Returns the chain the manager selects, as the ordered list of hops it actually applies. Each fake
	/// upgrader appends its own target version to the payload, so the result names the route taken.
	/// </summary>
	private static string Route(SnapshotVersionManager manager, int from, int to) =>
		System.Text.Encoding.UTF8.GetString(manager.UpgradeSnapshot(AggregateType, [], from, to));

	private static IEventUpgrader EventUpgrader(int from, int to)
	{
		var upgrader = A.Fake<IEventUpgrader>();
		_ = A.CallTo(() => upgrader.EventType).Returns(EventType);
		_ = A.CallTo(() => upgrader.FromVersion).Returns(from);
		_ = A.CallTo(() => upgrader.ToVersion).Returns(to);
		return upgrader;
	}

	private static ISnapshotUpgrader SnapshotUpgrader(int from, int to)
	{
		var upgrader = A.Fake<ISnapshotUpgrader>();
		_ = A.CallTo(() => upgrader.AggregateType).Returns(AggregateType);
		_ = A.CallTo(() => upgrader.FromVersion).Returns(from);
		_ = A.CallTo(() => upgrader.ToVersion).Returns(to);
		// Append this hop to the payload so a completed chain spells out the route it took.
		_ = A.CallTo(() => upgrader.Upgrade(A<byte[]>._)).ReturnsLazily(
			(byte[] data) => [.. data, .. System.Text.Encoding.UTF8.GetBytes($"-{to}")]);
		return upgrader;
	}
}
