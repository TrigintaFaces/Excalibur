// -----------------------------------------------------------------------
// <copyright file="TrimmabilityClaimTests.cs" company="Excalibur">
//     Licensed under the Excalibur License 1.0.
//     SPDX-License-Identifier: Excalibur-1.0 OR AGPL-3.0-or-later OR Apache-2.0
// </copyright>
// -----------------------------------------------------------------------

using System.Xml.Linq;

using Shouldly;

using Xunit;

namespace Boundary.Tests;

/// <summary>
/// <para>
/// <c>IsTrimmable=true</c> emits an assembly attribute telling a consumer's trimmer it is safe to strip
/// this package's unreferenced code. That is a promise made to a tool that will act on it silently, in
/// the consumer's build, where we never see the result. The repository policy is that the promise is
/// <b>derived from evidence</b> and never asserted by convention: a project declaring
/// <c>IsAotCompatible=true</c> gets trimmability derived by the SDK, and the converse does not hold.
/// The policy is written out in <c>src/Directory.Build.props</c>.
/// </para>
/// <para>
/// <b>Why this guard exists rather than the AOT gate.</b> <c>eng/ci/aot-promise-coherence-gate.py</c>
/// looks sufficient and is not: its population is "projects declaring <c>IsAotCompatible=true</c>", and
/// the token <c>IsTrimmable</c> does not appear in it at all. A project declaring
/// <c>IsAotCompatible=false</c> while asserting <c>IsTrimmable=true</c> is outside that gate's
/// population by construction, so the gate cannot fail on it no matter how often it runs. Two shipped
/// packages sat in exactly that blind spot, one of them declaring both claims three lines apart.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Architecture")]
public sealed class TrimmabilityClaimTests
{
    /// <summary>
    /// SAFETY: no shipped project promises trimmability it has not earned.
    /// </summary>
    [Fact]
    public void NoProjectAssertsTrimmability_WithoutAotEvidence()
    {
        var repoRoot = TestHelpers.GetRepositoryRoot();
        var offenders = TestHelpers
            .GetCsprojFiles(repoRoot, "src")
            .Where(path => AssertsTrimmabilityWithoutAotEvidence(XDocument.Load(path)))
            .Select(path => Path.GetRelativePath(repoRoot, path))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        offenders.ShouldBeEmpty(
            "IsTrimmable=true tells a consumer's trimmer this package is safe to strip. Declare "
            + "IsAotCompatible=true instead, once Invoke-AotCoverageGate produces the evidence for it, "
            + "and let the SDK derive trimmability. Offending project(s): "
            + string.Join(", ", offenders));
    }

    /// <summary>
    /// LIVENESS: the scan actually reaches projects. Without this, deleting the src tree, renaming the
    /// property, or breaking the file walk would leave the safety arm above passing over an empty set.
    /// </summary>
    [Fact]
    public void TheScan_ActuallyReachesShippedProjects()
    {
        var repoRoot = TestHelpers.GetRepositoryRoot();
        var projects = TestHelpers.GetCsprojFiles(repoRoot, "src");

        projects.Count.ShouldBeGreaterThan(100,
            "the safety arm is vacuous if the csproj walk returns nothing; this repository ships well "
            + "over a hundred projects under src/.");
    }

    /// <summary>
    /// NON-VACUITY: the predicate must actually reject the shape it exists to reject, and must accept
    /// the three legitimate shapes. Asserted against synthetic documents so the arm keeps its meaning
    /// when the repository is clean — a guard proven only by the absence of offenders is indistinguishable
    /// from one that cannot fire.
    /// </summary>
    [Theory]
    // the defect: a bare trimmability claim, and the same claim beside an explicit refusal of AOT
    [InlineData("<Project><PropertyGroup><IsTrimmable>true</IsTrimmable></PropertyGroup></Project>", true)]
    [InlineData("<Project><PropertyGroup><IsTrimmable>true</IsTrimmable>"
        + "<IsAotCompatible>false</IsAotCompatible></PropertyGroup></Project>", true)]
    // earned: the SDK derives trimmability from this, so stating it too is redundant but not dishonest
    [InlineData("<Project><PropertyGroup><IsTrimmable>true</IsTrimmable>"
        + "<IsAotCompatible>true</IsAotCompatible></PropertyGroup></Project>", false)]
    // not a promise: false is a refusal, and silence claims nothing
    [InlineData("<Project><PropertyGroup><IsTrimmable>false</IsTrimmable></PropertyGroup></Project>", false)]
    [InlineData("<Project><PropertyGroup><IsAotCompatible>true</IsAotCompatible></PropertyGroup></Project>", false)]
    [InlineData("<Project><PropertyGroup /></Project>", false)]
    public void ThePredicate_RejectsOnlyTheUnearnedClaim(string projectXml, bool expected) =>
        AssertsTrimmabilityWithoutAotEvidence(XDocument.Parse(projectXml)).ShouldBe(expected);

    /// <summary>
    /// A project makes an unearned trimmability promise when it asserts <c>IsTrimmable=true</c> without
    /// also declaring <c>IsAotCompatible=true</c>, which is the only declaration that evidences it.
    /// </summary>
    private static bool AssertsTrimmabilityWithoutAotEvidence(XDocument project) =>
        DeclaresTrue(project, "IsTrimmable") && !DeclaresTrue(project, "IsAotCompatible");

    private static bool DeclaresTrue(XDocument project, string property) =>
        project.Descendants()
            .Any(el => el.Name.LocalName.Equals(property, StringComparison.Ordinal)
                && bool.TryParse(el.Value.Trim(), out var value)
                && value);
}
