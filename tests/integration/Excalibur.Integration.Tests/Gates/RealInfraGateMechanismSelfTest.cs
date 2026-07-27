// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Gates;

/// <summary>
/// Permanent non-vacuity self-test <b>fixture</b> for the real-infra tenant-isolation CI gate — the
/// deterministic, Docker-free planted-violation the gate's own <c>--self-test</c> runs to prove its
/// exit-code mechanism can still <b>redden</b>, forever, even after every real <c>Infra=Required</c>
/// lock has gone green.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> The authoritative real-infra gate maps test outcomes to a three-state exit:
/// <c>0</c> PASS (all selected <c>Category=Integration&amp;Infra=Required</c> tests green), <c>1</c> FAIL
/// (a selected test failed), <c>2</c> REFUSE (real infrastructure could not be provisioned — no Docker /
/// no service container). A gate whose FAIL path is never exercised is the false-safety class this
/// keystone exists to kill: a required check that goes green because it silently stopped detecting
/// anything. This fixture is the <i>planted violation</i> the gate mechanism is run against so a green
/// gate is a green it earned.
/// </para>
/// <para>
/// <b>Why it is Docker-free and green-by-default.</b> It carries a dedicated
/// <c>[Trait("Category","GateSelfTest")]</c> — deliberately <b>not</b> <c>Integration</c>, <b>not</b>
/// <c>Unit</c>, <b>not</b> <c>Infra=Required</c> — so it is excluded from every real shard, from the
/// authoritative <c>Infra=Required</c> gate, and from the nightly <c>Category=Integration|EndToEnd</c>
/// run. On top of that categorical exclusion, its outcome is driven by the
/// <c>EXCALIBUR_TGO35C_SELFTEST</c> environment variable and defaults to <b>PASS when unset</b>, so a
/// full-solution <c>dotnet test</c> (sprint-close verification) that happens to include it stays green.
/// It reddens <b>only</b> when the gate's <c>--self-test</c> arms it explicitly. That is the standard
/// "planted violation toggled by the harness" shape (mirrors the <c>--self-test</c> blocks in
/// <c>eng/ci/*.sh</c>).
/// </para>
/// <para>
/// <b>Consumption contract (handed to the gate wiring owner — the gate reads this, the fixture does not
/// touch any workflow/gate file):</b>
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Filter:</b> <c>dotnet test --filter "Category=GateSelfTest"</c> selects exactly this fixture.
/// </description></item>
/// <item><description>
/// <b>PASS mapping proof:</b> run the filter with <c>EXCALIBUR_TGO35C_SELFTEST=pass</c> (or unset) ⇒
/// <b>0 failed tests</b> ⇒ the gate must map to exit <c>0</c>.
/// </description></item>
/// <item><description>
/// <b>FAIL mapping proof:</b> run the filter with <c>EXCALIBUR_TGO35C_SELFTEST=fail</c> ⇒ <b>exactly one
/// failed test</b> (<see cref="Redden_The_Gate_Only_When_Explicitly_Armed"/>) ⇒ the gate must map to
/// exit <c>1</c>. If the gate stays green here, the gate is vacuous.
/// </description></item>
/// <item><description>
/// <b>REFUSE mapping proof (exit <c>2</c>) is a GATE-LEVEL concern, not expressible from test output:</b>
/// REFUSE = infrastructure was never provisioned, which the gate detects by its own pre-run probe (e.g.
/// <c>docker info</c> failing) and must surface non-green <b>without</b> running the suite and <b>without</b>
/// <c>|| true</c>. The gate <c>--self-test</c> proves it by stubbing the probe to unavailable and asserting
/// exit <c>2</c>. At the <i>test</i> level the corresponding contract is the fail-closed
/// <c>DockerAvailable.ShouldBeTrue("…never skipped")</c> the real <c>Infra=Required</c> locks carry — a
/// missing container fails the lock closed, it never skips green.
/// </description></item>
/// </list>
/// </remarks>
[Trait("Category", "GateSelfTest")]
public sealed class RealInfraGateMechanismSelfTest
{
    /// <summary>The single knob the gate's <c>--self-test</c> uses to arm the planted violation.</summary>
    private const string SelfTestOutcomeVariable = "EXCALIBUR_TGO35C_SELFTEST";

    private const string Fail = "fail";

    private static string? Outcome =>
        Environment.GetEnvironmentVariable(SelfTestOutcomeVariable);

    /// <summary>
    /// PASS arm — always green. Proves the gate maps an all-green selected set to exit <c>0</c>, and is
    /// the arm that keeps a full-solution run green when this category is not excluded by filter.
    /// </summary>
    [Fact]
    public void Pass_The_Gate_When_Not_Armed()
    {
        // Deliberately a real, unconditional truth — the PASS case must be green in every context
        // (armed pass, armed fail on OTHER arms, or unset), so only the FAIL arm below can redden.
        (Outcome is null or "pass" || Outcome == Fail).ShouldBeTrue(
            "the PASS arm must remain green regardless of how the self-test is armed — only " +
            $"{nameof(Redden_The_Gate_Only_When_Explicitly_Armed)} is allowed to fail, and only under " +
            $"'{SelfTestOutcomeVariable}={Fail}'.");
    }

    /// <summary>
    /// FAIL arm — the planted violation. Green by default; fails <b>only</b> when the gate's
    /// <c>--self-test</c> sets <c>EXCALIBUR_TGO35C_SELFTEST=fail</c>. Its failure is the deterministic,
    /// Docker-free signal the gate must translate into exit <c>1</c>. Do NOT weaken or delete this arm to
    /// get a green — that re-vacuates the gate mechanism it proves.
    /// </summary>
    [Fact]
    public void Redden_The_Gate_Only_When_Explicitly_Armed()
    {
        if (!string.Equals(Outcome, Fail, StringComparison.Ordinal))
        {
            // Unarmed (the normal state everywhere except the gate self-test): stay green.
            return;
        }

        // Armed: emit a deterministic failure the gate self-test asserts reddens the mechanism (exit 1).
        // This is the whole point of the fixture — a planted violation that proves the gate is not vacuous.
        false.ShouldBeTrue(
            $"PLANTED VIOLATION (armed via {SelfTestOutcomeVariable}={Fail}): this failure is intentional " +
            "and proves the real-infra gate's FAIL path maps to exit 1. If you are reading this outside the " +
            "gate self-test, the environment variable was set unexpectedly — unset it; do not edit this arm.");
    }
}
