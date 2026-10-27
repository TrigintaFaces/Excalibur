// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.Dispatch;

namespace Excalibur.Benchmarks.Authorization;

/// <summary>
/// BenchmarkDotNet benchmarks for <see cref="AuthorizationPolicy"/> grant evaluation, including the
/// wildcard fallback scan.
/// </summary>
/// <remarks>
/// <para>
/// <b>Gate, from <c>management/specs/authorization-wildcard-grants-spec.md</c>:</b> exact match stays an
/// O(1) dictionary lookup at under 100ns, and the wildcard fallback stays under 1ms for up to 500 wildcard
/// grants per user, scanning O(W).
/// </para>
/// <para>
/// <b>Why these claims live here and not in a unit test.</b> They used to be three xUnit arms asserting
/// microseconds per call, plus two asserting a ratio of timings. A unit test is the wrong instrument for a
/// timing claim and the measurements proved it: the cost arms failed on a loaded hosted runner while the
/// code was correct, and the ratio arms were defeated by the optimiser -- a deliberately quadratic
/// reference, 100x the work, measured 85x on a developer box in Debug and 10.7x, 14.6x and 26.1x on the
/// three hosted runners in Release, because the JIT folds a synthetic loop once it is optimised. At 10.7x a
/// quadratic is indistinguishable from a linear function, so no threshold could separate them.
/// </para>
/// <para>
/// This is also what Microsoft does: the BCL asserts behaviour in its unit suites and keeps speed claims in
/// BenchmarkDotNet under the dotnet/performance harness, on pinned hardware, where warmup, pilot sizing,
/// multimodality and dead-code elimination are the harness's responsibility rather than the test author's.
/// </para>
/// <para>
/// <b>What still guards this code in CI</b> is
/// <c>tests/unit/Excalibur.A3.Tests/A3/Authorization/AuthorizationPolicyWildcardPerformanceShould.cs</c>,
/// which asserts the ALLOCATION invariant -- that per-call allocation does not grow with the grant count.
/// Byte counts are integers from the runtime's own accounting, identical on every machine and in every
/// configuration, so they cannot flake and the optimiser cannot defeat them. These benchmarks gate
/// nothing; run them on demand.
/// </para>
/// <para>
/// <b>Read <see cref="WildcardScanMiss"/> as the honest number.</b> A matching query short-circuits at the
/// first hit, so a match benchmark reports the position of that grant in the sorted list as much as the
/// cost of the scan. The miss walks every grant and is the real O(W) upper bound.
/// </para>
/// </remarks>
[SimpleJob(RuntimeMoniker.HostProcess)]
[MemoryDiagnoser]
public class AuthorizationPolicyBenchmarks
{
    private const string Tenant = "tenant-1";

    /// <summary>Gets or sets the number of wildcard grants the policy under test carries.</summary>
    /// <value>The wildcard grant count; 500 is the upper bound the specification names.</value>
    [Params(0, 50, 500)]
    public int WildcardGrantCount { get; set; }

    private AuthorizationPolicy _policy = null!;

    /// <summary>Builds the policy for the current <see cref="WildcardGrantCount"/>.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var grants = new Dictionary<string, object>();
        for (var i = 0; i < WildcardGrantCount; i++)
        {
            grants[$"{Tenant}:{GrantType.Activity}:Namespace{i}.*"] = true;
        }

        // One exact grant, so the O(1) path has something to find at every parameter value.
        grants[$"{Tenant}:{GrantType.Activity}:ExactActivity"] = true;

        _policy = new AuthorizationPolicy(
            grants,
            new Dictionary<string, IReadOnlyCollection<string>>(),
            new FixedTenantContext(Tenant),
            "user-1");
    }

    /// <summary>The O(1) dictionary hit. Spec gate: under 100ns, and flat in the wildcard count.</summary>
    /// <returns>Always <see langword="true"/>.</returns>
    [Benchmark(Baseline = true, Description = "Exact match (O(1) dictionary hit)")]
    public bool ExactMatch() => _policy.HasGrant("ExactActivity");

    /// <summary>
    /// The wildcard fallback terminating on a match. Reports the matching grant's position as much as the
    /// scan cost, so <see cref="WildcardScanMiss"/> is the number to quote for the O(W) bound.
    /// </summary>
    /// <returns><see langword="true"/> when a wildcard grant exists to match.</returns>
    [Benchmark(Description = "Wildcard fallback, match (early exit)")]
    public bool WildcardScanMatch() =>
        _policy.HasGrant(WildcardGrantCount == 0 ? "ExactActivity" : "Namespace0.Action");

    /// <summary>
    /// The wildcard fallback exhausting every grant. Spec gate: under 1ms at 500 grants. This is the O(W)
    /// upper bound, because nothing matches and no early exit is possible.
    /// </summary>
    /// <returns>Always <see langword="false"/>.</returns>
    [Benchmark(Description = "Wildcard fallback, full scan miss (O(W) upper bound)")]
    public bool WildcardScanMiss() => _policy.HasGrant("Unmatched.Action");

    /// <summary>
    /// A tenant context with a fixed identifier, hand-written so no mocking framework sits on the measured
    /// path -- a dynamic proxy's property call would be measured alongside the code under test.
    /// </summary>
    /// <param name="tenantId">The tenant identifier to report.</param>
    private sealed class FixedTenantContext(string tenantId) : ITenantContext
    {
        /// <inheritdoc/>
        public string? TenantId => tenantId;

        /// <inheritdoc/>
        public bool HasTenant => true;
    }
}
