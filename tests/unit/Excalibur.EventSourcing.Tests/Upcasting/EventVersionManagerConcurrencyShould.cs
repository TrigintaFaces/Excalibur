// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// CA1031: these tests deliberately capture ANY exception thrown by concurrent RegisterUpgrader calls
// (the pre-fix non-thread-safe Dictionary throws assorted corruption exceptions) so the assertion can
// prove the fixed serialized path throws nothing.
#pragma warning disable CA1031

using System.Collections.Concurrent;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Upcasting;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Upcasting;

/// <summary>
/// Author≠impl regression lock for S850 Lane D · <c>b5ebtk</c> (TOCTOU / non-thread-safe registry on
/// <see cref="EventVersionManager.RegisterUpgrader"/>).
/// </summary>
/// <remarks>
/// <para>
/// Authored by FrontendDeveloper (did NOT implement the fix — independence per
/// <c>issue-remediation-protocol</c>) against the frozen GUIDE seam (msg 15508): the backing store moves
/// from a plain <see cref="Dictionary{TKey, TValue}"/> to a <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// with a top-level <c>Lock</c> that serializes the GetOrAdd → conflict-check → Add sequence, mirroring the
/// hardened sibling <c>SnapshotVersionManager</c>.
/// </para>
/// <para>
/// <b>Non-vacuity (RED on the pre-fix surface):</b> the pre-fix <c>Dictionary</c> is not safe for concurrent
/// writes. Two failure modes are locked:
/// <list type="number">
/// <item><b>Distinct keys</b> — concurrent <c>Add</c>s to different keys corrupt the dictionary's internal
/// buckets during a resize → an intermittent throw (e.g. <c>IndexOutOfRangeException</c> /
/// "collection was modified") or silently dropped entries.</item>
/// <item><b>Same key</b> — the classic check-then-act: two threads both observe "key absent", both create a
/// fresh list and assign, so one list (and its upgraders) is lost; concurrent <c>List.Add</c> on the shared
/// list also loses entries.</item>
/// </list>
/// Either way the final upgrader count is less than what was registered, or an exception escapes — RED. The
/// fixed serialized path registers every upgrader exactly once with no throw — GREEN. The GREEN side is
/// deterministic (the serialized implementation is always correct); only the pre-fix RED is racy, which is
/// the property a non-vacuous concurrency lock needs.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class EventVersionManagerConcurrencyShould
{
	[Fact]
	public void RegisterDistinctEventTypesConcurrently_WithoutCorruptionOrLoss()
	{
		// Arrange — pre-create the fakes so the parallel region exercises only the SUT (RegisterUpgrader),
		// not FakeItEasy proxy construction.
		var manager = new EventVersionManager(NullLogger<EventVersionManager>.Instance);
		const int count = 4096;
		var upgraders = Enumerable.Range(0, count)
			.Select(i => CreateUpgrader($"Event-{i}", fromVersion: 1, toVersion: 2))
			.ToArray();
		var failures = new ConcurrentBag<Exception>();

		// Act — register all upgraders concurrently across the thread pool.
		Parallel.For(0, count, i =>
		{
			try
			{
				manager.RegisterUpgrader(upgraders[i]);
			}
			catch (Exception ex)
			{
				failures.Add(ex);
			}
		});

		// Assert — no corruption-throw, and every distinct registration survived.
		failures.ShouldBeEmpty();
		var surviving = Enumerable.Range(0, count)
			.Sum(i => manager.GetUpgradersForEventType($"Event-{i}").Count());
		surviving.ShouldBe(count);
	}

	[Fact]
	public void RegisterDistinctVersionsForSameEventTypeConcurrently_WithoutLostUpdates()
	{
		// Arrange — every registration targets the SAME event type with a distinct, non-conflicting
		// version range. This is the pure check-then-act TOCTOU: all writes contend on one dictionary key
		// and one shared list.
		var manager = new EventVersionManager(NullLogger<EventVersionManager>.Instance);
		const int count = 2000;
		var upgraders = Enumerable.Range(0, count)
			.Select(i => CreateUpgrader("SharedEvent", fromVersion: i, toVersion: i + 1))
			.ToArray();
		var failures = new ConcurrentBag<Exception>();

		// Act
		Parallel.For(0, count, i =>
		{
			try
			{
				manager.RegisterUpgrader(upgraders[i]);
			}
			catch (Exception ex)
			{
				failures.Add(ex);
			}
		});

		// Assert — serialized registration loses nothing: all N upgraders are present under the one key.
		failures.ShouldBeEmpty();
		manager.GetUpgradersForEventType("SharedEvent").Count().ShouldBe(count);
	}

	private static IEventUpgrader CreateUpgrader(string eventType, int fromVersion, int toVersion)
	{
		var upgrader = A.Fake<IEventUpgrader>();
		_ = A.CallTo(() => upgrader.EventType).Returns(eventType);
		_ = A.CallTo(() => upgrader.FromVersion).Returns(fromVersion);
		_ = A.CallTo(() => upgrader.ToVersion).Returns(toVersion);
		return upgrader;
	}
}
