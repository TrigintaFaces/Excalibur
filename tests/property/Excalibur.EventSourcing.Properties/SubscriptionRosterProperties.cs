// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;

using Shouldly;

using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Subscriptions;

namespace Excalibur.EventSourcing.Properties;

/// <summary>
/// Generated properties over roster-versus-registry set differences — the R3 rung for the subscription
/// seam.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this covers that an example-based arm cannot.</b> The catastrophic failure on this seam is a
/// host attesting freshness over work it does not host. One example arm pins one shape of that
/// divergence; the divergence is a SET DIFFERENCE, and the shapes are combinatorial — member absent
/// locally, extra local registration, disjoint sets, empty roster, single member, case-distinct keys
/// that must stay distinct. A suite that enumerates the examples someone imagined is bounded by that
/// imagination; this one is bounded by the generator.
/// </para>
/// <para>
/// <b>It binds the PRODUCTION computation, not a restatement of it.</b> Each case drives
/// <see cref="AsyncProjectionProcessingHost.UnattestableMembers(SubscriptionRoster, IReadOnlyList{ProjectionRegistration})"/>
/// — the same method the host calls to decide whether it may attest. A suite computing the difference
/// itself would assert that its own arithmetic is self-consistent and say nothing about the host.
/// </para>
/// <para>
/// <b>THE GENERATOR IS PROVEN TO REACH THE FAILING SHAPE, and that guard is the point.</b> This
/// repository's only previous property suite contains an arm that is alive, correct and blind: its
/// generator cannot produce the precondition the property needs, so it passes for a defect it was
/// written to catch. An R3 artifact existing is not R3 discharged. Every arm here therefore counts how
/// many generated cases actually exhibited the interesting shape and FAILS if that count is zero —
/// so a generator that stops reaching the divergence reddens the suite instead of silently passing.
/// </para>
/// <para>
/// <b>A failure reproduces.</b> Cases are drawn from a fixed root seed, and every failure message
/// carries the seed plus the two sets that produced it.
/// </para>
/// </remarks>
[Trait("Category", "Property")]
[Trait("Component", "EventStore")]
public sealed class SubscriptionRosterProperties
{
	/// <summary>
	/// Fixed so the suite is deterministic. A generated suite drawing from a random seed is a different
	/// suite on every run, so a red cannot be reproduced and a green cannot be trusted.
	/// </summary>
	private const int RootSeed = 20261027;

	private const int CaseCount = 400;

	/// <summary>A projection type pool. Distinct CLR types so registrations carry real keys.</summary>
	private sealed class P0;

	private sealed class P1;

	private sealed class P2;

	private sealed class P3;

	private sealed class P4;

	private static readonly Type[] Pool = [typeof(P0), typeof(P1), typeof(P2), typeof(P3), typeof(P4)];

	private static string KeyOf(Type type) => type.FullName ?? type.Name;

	// The projection instance is an opaque object on this record and nothing here reads it: every arm
	// asserts over ProjectionType, which is what a roster key is derived from. A real MultiStreamProjection
	// would need the type at compile time, and the pool is a runtime Type[] precisely so the generator can
	// draw subsets of it.
	private static ProjectionRegistration RegistrationFor(Type type) =>
		new(type, ProjectionMode.Async, new object(), inlineApply: null);

	/// <summary>One generated case: what the store holds, and what this process hosts.</summary>
	private sealed record Case(
		SubscriptionRoster Roster,
		IReadOnlyList<ProjectionRegistration> Registrations,
		IReadOnlySet<string> RosterKeys,
		IReadOnlySet<string> HostedKeys);

	private static Case GenerateCase(int seed)
	{
		var random = new Random(seed);

		// Independent subsets, so every relationship between the two sets is reachable: equal, disjoint,
		// overlapping in either direction, and either side empty. Drawing the hosted set as a subset of
		// the roster would make the interesting shape -- a member absent locally -- unreachable, which is
		// precisely the generator defect this suite exists not to repeat.
		var rosterTypes = Pool.Where(_ => random.Next(2) == 0).ToArray();
		var hostedTypes = Pool.Where(_ => random.Next(2) == 0).ToArray();

		var members = rosterTypes
			.Select(type => new SubscriptionMember(KeyOf(type), new string('A', 64), "dest-a"))
			.ToArray();

		return new Case(
			new SubscriptionRoster("generated", members),
			[.. hostedTypes.Select(RegistrationFor)],
			rosterTypes.Select(KeyOf).ToHashSet(StringComparer.Ordinal),
			hostedTypes.Select(KeyOf).ToHashSet(StringComparer.Ordinal));
	}

	/// <summary>
	/// The unattestable set is EXACTLY the roster minus what this process hosts — never a subset.
	/// </summary>
	/// <remarks>
	/// A subset would be the defect wearing a smaller hat: any member dropped from this set is a member
	/// the host will attest without hosting it.
	/// </remarks>
	[Fact]
	public void Report_exactly_the_roster_members_the_process_does_not_host()
	{
		var casesWithDivergence = 0;

		for (var caseIndex = 0; caseIndex < CaseCount; caseIndex++)
		{
			var seed = RootSeed + caseIndex;
			var generated = GenerateCase(seed);
			var expected = generated.RosterKeys.Except(generated.HostedKeys, StringComparer.Ordinal)
				.ToHashSet(StringComparer.Ordinal);

			var actual = AsyncProjectionProcessingHost
				.UnattestableMembers(generated.Roster, generated.Registrations)
				.ToHashSet(StringComparer.Ordinal);

			if (expected.Count > 0)
			{
				casesWithDivergence++;
			}

			actual.SetEquals(expected).ShouldBeTrue(
				$"Unattestable set wrong on generated case seed {seed.ToString(CultureInfo.InvariantCulture)}. "
				+ $"roster=[{string.Join(",", generated.RosterKeys.Order(StringComparer.Ordinal))}] "
				+ $"hosted=[{string.Join(",", generated.HostedKeys.Order(StringComparer.Ordinal))}] "
				+ $"expected=[{string.Join(",", expected.Order(StringComparer.Ordinal))}] "
				+ $"actual=[{string.Join(",", actual.Order(StringComparer.Ordinal))}]");
		}

		// NON-VACUITY. Without this the arm passes for a generator that only ever produces fully hosted
		// rosters -- the shape in which there is nothing to get wrong.
		casesWithDivergence.ShouldBeGreaterThan(0,
			"The generator never produced a roster member absent from the local registry, so this arm "
			+ "proved nothing about the case it exists for.");
	}

	/// <summary>
	/// A host that hosts every roster member has nothing unattestable — the liveness half.
	/// </summary>
	/// <remarks>
	/// The arm above is satisfied by an implementation that reports EVERY member as unattestable, which
	/// would deny every attestation forever: safe, inert, and useless. This arm is the one that fails for
	/// such an implementation.
	/// </remarks>
	[Fact]
	public void Report_nothing_unattestable_when_the_process_hosts_the_whole_roster()
	{
		var casesWithANonEmptyRoster = 0;

		for (var caseIndex = 0; caseIndex < CaseCount; caseIndex++)
		{
			var seed = RootSeed + caseIndex;
			var generated = GenerateCase(seed);

			// Host exactly the roster, whatever it was generated to be.
			var registrations = generated.RosterKeys
				.Select(key => RegistrationFor(Pool.Single(type => string.Equals(KeyOf(type), key, StringComparison.Ordinal))))
				.ToArray();

			if (registrations.Length > 0)
			{
				casesWithANonEmptyRoster++;
			}

			AsyncProjectionProcessingHost.UnattestableMembers(generated.Roster, registrations)
				.ShouldBeEmpty(
					"A process hosting the whole roster must have nothing unattestable. Generated case seed "
					+ seed.ToString(CultureInfo.InvariantCulture));
		}

		casesWithANonEmptyRoster.ShouldBeGreaterThan(0,
			"Every generated roster was empty, so this arm never exercised a hosted member.");
	}

	/// <summary>
	/// Extra local registrations never make anything unattestable.
	/// </summary>
	/// <remarks>
	/// A process may legitimately host projections this subscription does not contain. Counting those
	/// would make a save report behind for work no subscription asked for.
	/// </remarks>
	[Fact]
	public void Ignore_local_registrations_the_roster_does_not_contain()
	{
		var casesWithExtraLocal = 0;

		for (var caseIndex = 0; caseIndex < CaseCount; caseIndex++)
		{
			var seed = RootSeed + caseIndex;
			var generated = GenerateCase(seed);
			var extraLocal = generated.HostedKeys.Except(generated.RosterKeys, StringComparer.Ordinal).Count();
			if (extraLocal > 0)
			{
				casesWithExtraLocal++;
			}

			var unattestable = AsyncProjectionProcessingHost
				.UnattestableMembers(generated.Roster, generated.Registrations);

			unattestable.ShouldAllBe(key => generated.RosterKeys.Contains(key),
				"Only a ROSTER member can be unattestable; a purely local registration is not subscription "
				+ "work. Generated case seed " + seed.ToString(CultureInfo.InvariantCulture));
		}

		casesWithExtraLocal.ShouldBeGreaterThan(0,
			"The generator never produced a local registration outside the roster, so this arm proved "
			+ "nothing about the case it exists for.");
	}

	/// <summary>
	/// Case-distinct projection keys stay distinct members.
	/// </summary>
	/// <remarks>
	/// The comparison is ordinal in storage, so two keys differing only by case are two members. Folding
	/// them together here would drop one from the unattestable set and permit an attestation over work
	/// the process does not host — the same defect, reached through a collation instead of a registry.
	/// </remarks>
	[Fact]
	public void Treat_case_distinct_projection_keys_as_distinct_members()
	{
		for (var caseIndex = 0; caseIndex < 64; caseIndex++)
		{
			var seed = RootSeed + caseIndex;
			var key = KeyOf(Pool[seed % Pool.Length]);
			var roster = new SubscriptionRoster("generated",
			[
				new SubscriptionMember(key, new string('A', 64), "dest-a"),
				new SubscriptionMember(key.ToUpperInvariant(), new string('B', 64), "dest-b"),
			]);

			// Host only the exact-case one.
			var unattestable = AsyncProjectionProcessingHost.UnattestableMembers(
				roster, [RegistrationFor(Pool[seed % Pool.Length])]);

			unattestable.ShouldHaveSingleItem().ShouldBe(key.ToUpperInvariant(),
				"The upper-cased key is a DIFFERENT member and is not hosted, so it must remain "
				+ "unattestable. Generated case seed " + seed.ToString(CultureInfo.InvariantCulture));
		}
	}
}
