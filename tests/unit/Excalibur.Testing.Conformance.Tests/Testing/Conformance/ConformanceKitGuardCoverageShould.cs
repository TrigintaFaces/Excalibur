// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Testing;
using Excalibur.Testing.Conformance;

using Shouldly;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Locks the presence of the suite-wiring guard on every published conformance kit.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ConformanceKitSuiteWiringShould"/> proves the guard WORKS. This proves the guard is
/// PRESENT — on every kit, including one added tomorrow. The two are different questions and the second
/// is the one that decays: a guard is inherited from <see cref="ConformanceTestKit"/>, so a new kit that
/// simply forgets the base list ships with no guard at all, and nothing about that reads as red. The kit
/// still compiles, its suites still pass, and an arm nobody wired still cannot fail.
/// </para>
/// <para>
/// A scan that matches nothing certifies everything, so this one carries a floor and fails below it
/// rather than reporting clean over an empty set.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConformanceKitGuardCoverageShould
{
	/// <summary>
	/// The number of kits below which the scan is treated as broken rather than as a clean result.
	/// </summary>
	/// <remarks>
	/// Deliberately just under the count at the time of writing. It exists to catch a scan that stopped
	/// matching — a renamed suffix, a moved namespace, an assembly that did not load — not to pin an
	/// exact inventory: pinning the exact number would make every legitimately added kit a failure, and
	/// the response to that failure is to edit the number, which is how a floor stops meaning anything.
	/// </remarks>
	private const int MinimumExpectedKits = 40;

	private const string KitSuffix = "ConformanceTestKit";

	private static string ScopeStatement(int examined) =>
		$"SCOPE: public abstract types in {typeof(ConformanceTestKit).Assembly.GetName().Name} whose name "
		+ $"ends in '{KitSuffix}' ({examined} examined). EXCLUDES: ConformanceTestKit itself (it is the "
		+ "shared base, not a kit), non-public and concrete types, every other assembly, and whether a "
		+ "DERIVING SUITE wires the guard as a runner-visible test — presence on the kit is what is "
		+ "checked here, not that a given consumer surfaced it.";

	/// <summary>
	/// Enumerates the published kits, newest-first in declaration order being irrelevant.
	/// </summary>
	/// <returns> Every public abstract type in the shipped package whose name marks it as a kit. </returns>
	private static IReadOnlyList<Type> PublishedKits() =>
		[.. typeof(ConformanceTestKit).Assembly
			.GetExportedTypes()
			.Where(static t =>
				t.IsAbstract
				&& t.Name.EndsWith(KitSuffix, StringComparison.Ordinal)
				&& t != typeof(ConformanceTestKit))
			.OrderBy(static t => t.FullName, StringComparer.Ordinal)];

	/// <summary>
	/// The check under test, taking its population as an argument so it can be pointed at a planted one.
	/// </summary>
	/// <param name="kits"> The kits to check. </param>
	private static void AssertEveryKitCarriesTheGuard(IReadOnlyList<Type> kits)
	{
		ArgumentNullException.ThrowIfNull(kits);

		if (kits.Count < MinimumExpectedKits)
		{
			throw new TestFixtureAssertionException(
				$"The conformance-kit scan found {kits.Count} kit(s), below the floor of {MinimumExpectedKits}. "
				+ "A scan that matches nothing certifies every kit it was pointed at, which is the defect it "
				+ "exists to detect, so too few matches is a failure and not a clean result. "
				+ ScopeStatement(kits.Count));
		}

		var unguarded = kits
			.Where(static t => !typeof(ConformanceTestKit).IsAssignableFrom(t))
			.Select(static t => t.Name)
			.OrderBy(static n => n, StringComparer.Ordinal)
			.ToList();

		if (unguarded.Count > 0)
		{
			throw new TestFixtureAssertionException(
				$"{unguarded.Count} of {kits.Count} published conformance kit(s) do not derive "
				+ $"{nameof(ConformanceTestKit)}, so they inherit no suite-wiring guard: a suite deriving one "
				+ "of them can omit an arm and still be certified, because an arm that never runs cannot "
				+ $"fail. Add ': {nameof(ConformanceTestKit)}' to the base list. Unguarded: "
				+ string.Join(", ", unguarded) + ". " + ScopeStatement(kits.Count));
		}
	}

	[Fact]
	public void CoverEveryPublishedKitWithTheWiringGuard()
	{
		var kits = PublishedKits();

		AssertEveryKitCarriesTheGuard(kits);

		kits.Count.ShouldBeGreaterThanOrEqualTo(
			MinimumExpectedKits,
			"the scope statement's count is only meaningful if the scan actually matched the package");
	}

	[Fact]
	public void FailAndNameTheKitWhenOneDoesNotCarryTheGuard()
	{
		// Built from the kits that DO carry the guard, plus one planted kit that does not, so the probe
		// asserts exactly one unguarded entry no matter what state the real package is in. Passing the raw
		// package population here would make this probe fail for a second reason the moment a real kit lost
		// its base list — reporting the planted kit and the real one together, which reads as a broken probe
		// rather than as the finding it is. The real population is CoverEveryPublishedKitWithTheWiringGuard's
		// job; this one exists only to prove the check can go red and name the type.
		var planted = new List<Type>(PublishedKits().Where(static t => typeof(ConformanceTestKit).IsAssignableFrom(t)))
		{
			typeof(UnguardedProbeConformanceTestKit)
		};

		var thrown = Should.Throw<TestFixtureAssertionException>(
			() => AssertEveryKitCarriesTheGuard(planted));

		thrown.Message.ShouldContain(
			nameof(UnguardedProbeConformanceTestKit),
			Case.Sensitive,
			"the failure must name the kit that lost its guard - 'some kit is unguarded' is not actionable");

		thrown.Message.ShouldContain(
			"1 of",
			Case.Sensitive,
			"the count must be exact, so a scan that silently stopped enumerating is not mistaken for one "
			+ "that found a single gap");
	}

	[Fact]
	public void FailRatherThanCertifyNothingWhenTheScanMatchesTooFew()
	{
		var truncated = PublishedKits()
			.Where(static t => typeof(ConformanceTestKit).IsAssignableFrom(t))
			.Take(MinimumExpectedKits - 1)
			.ToList();

		var thrown = Should.Throw<TestFixtureAssertionException>(
			() => AssertEveryKitCarriesTheGuard(truncated));

		thrown.Message.ShouldContain(
			"below the floor",
			Case.Sensitive,
			"an under-matching scan must fail on the floor, not pass over the kits it happened to find");
	}

	[Fact]
	public void EnumerateAtLeastOneArmOnEveryPublishedKit()
	{
		const BindingFlags Declared = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

		var kits = PublishedKits();

		var armless = kits
			.Where(static t => !t.GetMethods(Declared).Any(static m =>
				(m.ReturnType == typeof(Task) || m.ReturnType == typeof(void))
				&& m.GetParameters().Length == 0
				&& m.IsVirtual
				&& !m.IsFinal
				&& !m.IsSpecialName
				&& !string.Equals(
					m.Name,
					nameof(ConformanceTestKit.ConformanceSuite_ShouldWireEveryArm),
					StringComparison.Ordinal)))
			.Select(static t => t.Name)
			.OrderBy(static n => n, StringComparer.Ordinal)
			.ToList();

		armless.ShouldBeEmpty(
			$"a kit whose arms do not have the shape the guard enumerates (public, virtual, non-final, no "
			+ "parameters, returning Task or void) makes the guard throw on every suite that derives it, so "
			+ "the guard is unusable there rather than merely quiet. "
			+ ScopeStatement(kits.Count));
	}

	/// <summary>
	/// A kit-shaped type that deliberately does not derive the base, used to prove the scan goes red.
	/// </summary>
	private abstract class UnguardedProbeConformanceTestKit
	{
		public virtual Task DoSomething_ShouldSucceed() => Task.CompletedTask;
	}
}
