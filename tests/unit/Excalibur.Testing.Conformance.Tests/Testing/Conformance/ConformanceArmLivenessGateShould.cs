// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.Testing.Conformance;

using Shouldly;

using Xunit;

// Wired here rather than in the shipped package: the attribute applies to the assembly that declares
// it, and the package must not carry a test-framework attribute of its own. Every test assembly that
// runs conformance kit arms needs this line.
[assembly: AssemblyFixture(typeof(Excalibur.Testing.Conformance.ConformanceArmLivenessGate))]

namespace Excalibur.Testing.Conformance.Tests.Testing.Conformance;

/// <summary>
/// Non-vacuity proof for <see cref="ConformanceArmLivenessGate"/> — the gate that fails a conformance run
/// in which arms were attempted and not one executed.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rule is driven as a pure function, never through the live ledger.</b>
/// <see cref="ConformanceArmLedger"/> is process-wide state shared with the run these tests execute inside,
/// so a proof that staged a blind run by mutating it would corrupt the ledger the real gate reads at the end
/// of this very assembly. It could turn a healthy run red, or erase the evidence of a broken one. Passing
/// the counts in keeps the proof and the run disjoint.
/// </para>
/// <para>
/// <b>What these arms do NOT establish, stated rather than implied.</b> They prove the rule is correct and
/// that the gate is declared as this assembly's fixture. They cannot prove the runner actually disposes it:
/// a passing run is silent evidence either way, because a live assembly (this one executes many arms) makes
/// the gate return null whether it ran or not. That half is structural — the runner's contract — and no
/// assertion inside the run can observe it.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConformanceArmLivenessGateShould
{
	/// <summary>
	/// SAFETY. A run that attempted arms and executed none is the blind run the gate exists to catch.
	/// </summary>
	[Fact]
	public void FailWhenArmsWereAttemptedAndNoneExecuted()
	{
		var failure = ConformanceArmLivenessGate.EvaluateFailure(
			armsExecuted: 0, armsSkipped: 3, description: "  - Suite.Arm [ICapability]: not supported");

		failure.ShouldNotBeNull(
			"three arms were attempted and none ran, so the run verified nothing while reporting three "
			+ "passes - this is the exact state the gate exists to make visible");

		// Narrowed to string so the assertions below bind Shouldly's string overload; on a string? they
		// bind IEnumerable<char> and stop being substring assertions at all.
		var message = failure!;

		message.Contains("CONFORMANCE VERIFIED NOTHING IN THIS RUN", StringComparison.Ordinal).ShouldBeTrue(
			"the failure has to say what happened in its first line; a reader who has to infer it will "
			+ "assume a flake and re-run");
		message.Contains("Suite.Arm", StringComparison.Ordinal).ShouldBeTrue(
			"the ledger's own account must reach the failure message, or the reader knows a run was blind "
			+ "and not which arms were blind");
	}

	/// <summary>
	/// LIVENESS. A run that executed even one arm is not blind and the gate must stay silent.
	/// </summary>
	/// <remarks>
	/// Without this arm the rule is satisfied by a gate that fails every run, which would be detected
	/// immediately but only by the whole team's builds going red — and the fix a hurried reader reaches for
	/// is deleting the gate.
	/// </remarks>
	[Theory]
	[InlineData(1, 0)]
	[InlineData(1, 99)]
	[InlineData(500, 12)]
	public void StaySilentWhenAnyArmExecuted(int executed, int skipped)
	{
		ConformanceArmLivenessGate.EvaluateFailure(executed, skipped, "irrelevant").ShouldBeNull(
			"at least one arm ran its body, so this run verified something and the gate has nothing to say");
	}

	/// <summary>
	/// LIVENESS, and the arm that keeps the gate usable. A filtered run that selected no conformance arm
	/// has an empty ledger and must not be failed for it.
	/// </summary>
	/// <remarks>
	/// This is the false-RED the gate would otherwise produce on every targeted debugging run and on every
	/// CI shard that happens to contain no conformance suite. A gate that cries wolf is deleted, and then
	/// the gap it closed is open again with nobody watching.
	/// </remarks>
	[Fact]
	public void StaySilentWhenNothingWasAttempted()
	{
		ConformanceArmLivenessGate.EvaluateFailure(
			armsExecuted: 0, armsSkipped: 0, description: "irrelevant").ShouldBeNull(
			"no conformance arm was selected in this run, so there is nothing to be blind about - an "
			+ "unconditional demand here fails runs that were never asked to verify anything");
	}

	/// <summary>
	/// WIRING. The gate is declared as this assembly's fixture, so it is reached at the end of the run
	/// rather than shipped and never invoked.
	/// </summary>
	/// <remarks>
	/// A correct rule that nothing calls is the advertised-but-unwired shape applied to a gate. This arm
	/// asserts the declaration, which is what a reader can check; it does not assert that the runner
	/// disposes the fixture, which no assertion inside the run can observe. Removing the assembly attribute
	/// reddens this arm, which is the mutation that matters here.
	/// </remarks>
	[Fact]
	public void BeDeclaredAsThisAssemblysFixture()
	{
		var declared = typeof(ConformanceArmLivenessGateShould).Assembly
			.GetCustomAttributes<AssemblyFixtureAttribute>()
			.Select(static a => a.AssemblyFixtureType)
			.ToList();

		declared.ShouldContain(
			typeof(ConformanceArmLivenessGate),
			"this assembly runs conformance kit arms, so it must carry the gate that fails a run in which "
			+ "none of them executed - otherwise every arm can skip and the run still exits zero");
	}
}
