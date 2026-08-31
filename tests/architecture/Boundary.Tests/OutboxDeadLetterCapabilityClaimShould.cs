// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

namespace Boundary.Tests;

/// <summary>
/// Binds the outbox dead-letter startup failure message to what the shipped stores actually implement.
/// </summary>
/// <remarks>
/// <para>
/// The validator's failure message told the consumer that every shipped Excalibur outbox store supports
/// terminal dead-lettering. Four of them do not. A consumer who registered one of those hit a startup
/// failure and was told to do something the message implied was already true, which sends them hunting a
/// configuration error that does not exist.
/// </para>
/// <para>
/// This asserts the CONDITIONAL, which is what makes it survive the population changing: while any shipped
/// outbox provider lacks the capability, the message must not claim universality. If every provider gains
/// it later, the claim becomes true and this arm stops constraining the wording — by design.
/// </para>
/// <para>
/// The census is a source scan rather than a reflection walk because Boundary.Tests does not reference the
/// ten provider packages, and adding ten references to assert a wording property would be a heavier
/// coupling than the property is worth.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class OutboxDeadLetterCapabilityClaimShould
{
    private static readonly string RepoRoot = TestHelpers.GetRepositoryRoot();

    private const string ValidatorPath =
        "src/Dispatch/Excalibur.Dispatch/Options/Delivery/OutboxDeadLetterCapabilityValidator.cs";

    /// <summary>Phrases that assert the capability holds for every shipped store.</summary>
    private static readonly Regex UniversalityClaim = new(
        @"all\s+(shipped\s+)?(Excalibur\s+)?outbox\s+stores|all\s+shipped\s+Excalibur\s+outbox",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [Fact]
    public void NotClaimUniversalSupportWhileAShippedStoreLacksIt()
    {
        var (withCapability, without) = CensusOutboxProviders();

        // POSITIVE CONTROL: the scan must actually find providers, or "none lack it" would be vacuous.
        (withCapability.Count + without.Count).ShouldBeGreaterThan(
            5,
            "the provider census found almost nothing, so this arm is measuring the scan and not the code");
        withCapability.ShouldNotBeEmpty(
            "no provider was detected as implementing the capability, which means the detection is broken");

        if (without.Count == 0)
        {
            return; // every shipped store supports it; a universality claim would be true.
        }

        var validator = File.ReadAllText(Path.Combine(RepoRoot, ValidatorPath));

        UniversalityClaim.IsMatch(validator).ShouldBeFalse(
            "the validator still tells the consumer that every shipped store supports terminal "
            + "dead-lettering, while these do not: " + string.Join(", ", without));
    }

    [Fact]
    public void StillTellTheConsumerWhichCapabilityIsMissing()
    {
        // LIVENESS: removing the false claim must not remove the actionable part. A message that named
        // nothing would satisfy the arm above while being useless at 3am.
        var validator = File.ReadAllText(Path.Combine(RepoRoot, ValidatorPath));

        validator.ShouldContain(
            "IDeadLetterableOutboxStore",
            Case.Sensitive,
            "the failure must still name the interface the consumer has to implement or obtain");
    }

    private static (List<string> WithCapability, List<string> Without) CensusOutboxProviders()
    {
        var withCapability = new List<string>();
        var without = new List<string>();

        var outboxRoot = Path.Combine(RepoRoot, "src", "Excalibur");

        foreach (var directory in Directory.GetDirectories(outboxRoot, "Excalibur.Outbox.*"))
        {
            var sources = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .ToList();

            // Only directories that actually ship a store are part of the population. The discriminator is
            // the store FILE, not a ": IOutboxStore" base-list match: several providers declare the interface
            // across a line break or reach it through IFencedOutboxStore, and a textual base-list scan finds
            // five of the eleven -- an under-count that would silently shrink the population being asserted.
            var shipsStore = sources.Any(f =>
                Path.GetFileName(f).EndsWith("OutboxStore.cs", StringComparison.Ordinal));
            if (!shipsStore)
            {
                continue;
            }

            var name = Path.GetFileName(directory);
            if (sources.Any(f => File.ReadAllText(f).Contains("IDeadLetterableOutboxStore", StringComparison.Ordinal)))
            {
                withCapability.Add(name);
            }
            else
            {
                without.Add(name);
            }
        }

        return (withCapability, without);
    }
}
