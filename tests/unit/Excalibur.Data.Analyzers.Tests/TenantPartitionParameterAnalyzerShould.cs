// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Xunit;

namespace Excalibur.Data.Analyzers.Tests;

/// <summary>
/// Locks for EXDATA002 — a request that accepts a tenant partition and discards it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The silent arms are the important ones here, and they outnumber the firing arms deliberately.</b> This
/// analyzer ships under a build that promotes warnings to errors, so a false positive does not produce a noisy
/// log — it fails a consumer's compilation over code that is correct. The failure mode is severe and entirely
/// one-sided, so the rule is built to decline rather than guess, and the arms below pin every shape it must
/// decline on.
/// </para>
/// <para>
/// <b>The load-bearing one is <c>StaySilent_ForAnIdentityAddressedRequest…</c>.</b> A statement already
/// addressed by a primary key must not carry a tenant term: the term selects a subset of at-most-one-row, so
/// it cannot admit a foreign row and its only reachable effect is turning the correct row into zero rows. A
/// framework-wide consistency pass that added tenant terms uniformly is what previously stopped the outbox
/// marking messages it had claimed. An analyzer that asked such a request for a tenant term — or that made it
/// annotate its way out — would be that pass running forever. It passes with no annotation because it is not a
/// subject: it never accepted a partition.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TenantPartitionParameterAnalyzerShould
{
    // ---- fires -------------------------------------------------------------------------------------------

    [Fact]
    public async Task Flag_ARequestThatAcceptsATenantScopeAndNeverUsesIt()
    {
        const string source = """
            using Excalibur.Data;
            using Excalibur.Dispatch;

            namespace Sample
            {
                public sealed class LoadThing : DataRequestBase<object, string>
                {
                    public LoadThing(string id, TenantScope {|#0:scope|})
                    {
                        System.Console.WriteLine(id);
                    }
                }
            }
            """;

        var test = AnalyzerTestHarness.For<TenantPartitionParameterAnalyzer>(source);
        test.ExpectedDiagnostics.Add(
            AnalyzerTestHarness.Expect(
                AnalyzerTestHarness.TenantPartitionParameterIsDiscarded, 0, "LoadThing", "scope"));

        await test.RunAsync();
    }

    [Fact]
    public async Task Flag_ARequestThatAcceptsAKeyedTenantPartitionAndNeverUsesIt()
    {
        const string source = """
            using Excalibur.Data;
            using Excalibur.Dispatch;

            namespace Sample
            {
                public sealed class LoadThing : DataRequestBase<object, string>
                {
                    public LoadThing(KeyedTenantPartition {|#0:partition|}) { }
                }
            }
            """;

        var test = AnalyzerTestHarness.For<TenantPartitionParameterAnalyzer>(source);
        test.ExpectedDiagnostics.Add(
            AnalyzerTestHarness.Expect(
                AnalyzerTestHarness.TenantPartitionParameterIsDiscarded, 0, "LoadThing", "partition"));

        await test.RunAsync();
    }

    /// <summary>
    /// Derivation is transitive. Most requests in this framework derive through an intermediate base, so a
    /// rule that only checked the immediate base type would be silent on nearly all of them.
    /// </summary>
    [Fact]
    public async Task Flag_ARequestThatDerivesThroughAnIntermediateBase()
    {
        const string source = """
            using Excalibur.Data;
            using Excalibur.Dispatch;

            namespace Sample
            {
                public sealed class CountThings : DataRequest<int>
                {
                    public CountThings(TenantScope {|#0:scope|}) { }
                }
            }
            """;

        var test = AnalyzerTestHarness.For<TenantPartitionParameterAnalyzer>(source);
        test.ExpectedDiagnostics.Add(
            AnalyzerTestHarness.Expect(
                AnalyzerTestHarness.TenantPartitionParameterIsDiscarded, 0, "CountThings", "scope"));

        await test.RunAsync();
    }

    /// <summary>
    /// A primary constructor puts the parameter list between the type name and the base list, which is a shape
    /// a naive declaration pattern cannot see at all.
    /// </summary>
    [Fact]
    public async Task Flag_APrimaryConstructorParameterThatIsNeverUsed()
    {
        const string source = """
            using Excalibur.Data;
            using Excalibur.Dispatch;

            namespace Sample
            {
                public sealed class ListThings(TenantScope {|#0:scope|}) : DataRequestBase<object, string>
                {
                    public int Count => 0;
                }
            }
            """;

        var test = AnalyzerTestHarness.For<TenantPartitionParameterAnalyzer>(source);
        test.ExpectedDiagnostics.Add(
            AnalyzerTestHarness.Expect(
                AnalyzerTestHarness.TenantPartitionParameterIsDiscarded, 0, "ListThings", "scope"));

        await test.RunAsync();
    }

    /// <summary>
    /// An abstract base is a subject too, and this arm exists because the decision was first made by
    /// accident.
    /// </summary>
    /// <remarks>
    /// The test harness stub originally declared a base constructor that took a scope and dropped it, purely
    /// so a <c>base(scope)</c> fixture would compile. The analyzer reported it, one diagnostic leaked into
    /// every fixture that included the stub, and twelve arms went red over a defect none of them had
    /// authored. The analyzer was right: a base that accepts a partition and discards it is the worst
    /// instance of the defect, because every derived type calls <c>base(scope)</c> believing it does
    /// something. Pinning it here turns that from an accident into a decision.
    /// </remarks>
    [Fact]
    public async Task Flag_AnAbstractBaseThatAcceptsAPartitionAndDiscardsIt()
    {
        const string source = """
            using Excalibur.Data;
            using Excalibur.Dispatch;

            namespace Sample
            {
                public abstract class LeakyRequestBase : DataRequestBase<object, string>
                {
                    protected LeakyRequestBase(TenantScope {|#0:scope|}) { }
                }
            }
            """;

        var test = AnalyzerTestHarness.For<TenantPartitionParameterAnalyzer>(source);
        test.ExpectedDiagnostics.Add(
            AnalyzerTestHarness.Expect(
                AnalyzerTestHarness.TenantPartitionParameterIsDiscarded, 0, "LeakyRequestBase", "scope"));

        await test.RunAsync();
    }

    // ---- stays silent ------------------------------------------------------------------------------------

    /// <summary>
    /// The load-bearing silent arm: a request addressed by a unique key, carrying no tenant term and no
    /// annotation, must pass. If this case only passed by being suppressed, the analyzer would be teaching the
    /// defect it exists to prevent.
    /// </summary>
    [Fact]
    public async Task StaySilent_ForAnIdentityAddressedRequestWithNoTenantTermAndNoAttribute()
    {
        const string source = """
            using Excalibur.Data;

            namespace Sample
            {
                public sealed class MarkMessageSent : DataRequestBase<object, int>
                {
                    public MarkMessageSent(System.Guid id)
                    {
                        Id = id;
                    }

                    public System.Guid Id { get; }
                }
            }
            """;

        await AnalyzerTestHarness.ShouldReportNothingAsync<TenantPartitionParameterAnalyzer>(source);
    }

    /// <summary>
    /// A deliberately estate-wide request takes no tenant at all, so it is likewise not a subject and needs no
    /// opt-out to stay silent.
    /// </summary>
    [Fact]
    public async Task StaySilent_ForAnEstateWideRequestThatTakesNoTenant()
    {
        const string source = """
            using Excalibur.Data;

            namespace Sample
            {
                [NoTenantTerm(TenantConfinement.EstateWide, "operator statistics; the caller is not a tenant")]
                public sealed class GetStatistics : DataRequestBase<object, int>
                {
                    public GetStatistics(int batchSize)
                    {
                        BatchSize = batchSize;
                    }

                    public int BatchSize { get; }
                }
            }
            """;

        await AnalyzerTestHarness.ShouldReportNothingAsync<TenantPartitionParameterAnalyzer>(source);
    }

    [Fact]
    public async Task StaySilent_WhenTheScopeIsBoundIntoTheRequest()
    {
        const string source = """
            using Excalibur.Data;
            using Excalibur.Dispatch;

            namespace Sample
            {
                public sealed class LoadThing : DataRequestBase<object, string>
                {
                    public LoadThing(TenantScope scope)
                    {
                        TenantId = scope.TenantId;
                    }

                    public string TenantId { get; }
                }
            }
            """;

        await AnalyzerTestHarness.ShouldReportNothingAsync<TenantPartitionParameterAnalyzer>(source);
    }

    /// <summary>
    /// Any use silences the rule, including one that only forwards the value. The analyzer does not try to
    /// prove the value reaches the outgoing parameters; a proof that gives up is indistinguishable from a
    /// defect, and here that costs a broken build.
    /// </summary>
    [Fact]
    public async Task StaySilent_WhenTheScopeIsOnlyPassedToTheBaseConstructor()
    {
        const string source = """
            using Excalibur.Data;
            using Excalibur.Dispatch;

            namespace Sample
            {
                public sealed class LoadThing : DataRequestBase<object, string>
                {
                    public LoadThing(TenantScope scope) : base(scope) { }
                }
            }
            """;

        await AnalyzerTestHarness.ShouldReportNothingAsync<TenantPartitionParameterAnalyzer>(source);
    }

    [Fact]
    public async Task StaySilent_WhenTheScopeIsUsedOnlyInsideALambda()
    {
        const string source = """
            using Excalibur.Data;
            using Excalibur.Dispatch;

            namespace Sample
            {
                public sealed class LoadThing : DataRequestBase<object, string>
                {
                    public LoadThing(TenantScope scope)
                    {
                        Resolve = () => scope.TenantId;
                    }

                    public System.Func<string> Resolve { get; }
                }
            }
            """;

        await AnalyzerTestHarness.ShouldReportNothingAsync<TenantPartitionParameterAnalyzer>(source);
    }

    /// <summary>
    /// An ambient context is excluded on purpose: it can legitimately be present for an authorization decision
    /// without being a filter term, and including it would push authors toward adding tenant predicates to
    /// statements that must not have them.
    /// </summary>
    [Fact]
    public async Task StaySilent_ForAnUnusedTenantContextParameter()
    {
        const string source = """
            using Excalibur.Data;
            using Excalibur.Dispatch;

            namespace Sample
            {
                public sealed class LoadThing : DataRequestBase<object, string>
                {
                    public LoadThing(ITenantContext context) { }
                }
            }
            """;

        await AnalyzerTestHarness.ShouldReportNothingAsync<TenantPartitionParameterAnalyzer>(source);
    }

    /// <summary>
    /// The subject is a relational data request. An unrelated type that happens to accept a partition and drop
    /// it is out of scope, whatever else may be true of it.
    /// </summary>
    [Fact]
    public async Task StaySilent_ForATypeThatIsNotADataRequest()
    {
        const string source = """
            using Excalibur.Dispatch;

            namespace Sample
            {
                public sealed class NotARequest
                {
                    public NotARequest(TenantScope scope) { }
                }
            }
            """;

        await AnalyzerTestHarness.ShouldReportNothingAsync<TenantPartitionParameterAnalyzer>(source);
    }

    /// <summary>
    /// A primary constructor's parameters are in scope across every part of a partial type. Reading one part
    /// would let a use in another part read as an absence, so the rule declines to judge.
    /// </summary>
    [Fact]
    public async Task StaySilent_ForAPartialTypeWithAPrimaryConstructor()
    {
        const string first = """
            using Excalibur.Data;
            using Excalibur.Dispatch;

            namespace Sample
            {
                public partial class ListThings(TenantScope scope) : DataRequestBase<object, string>
                {
                }
            }
            """;

        const string second = """
            namespace Sample
            {
                public partial class ListThings
                {
                    public string TenantId => scope.TenantId;
                }
            }
            """;

        await AnalyzerTestHarness.ShouldReportNothingAsync<TenantPartitionParameterAnalyzer>(first, second);
    }

    // ---- the bail-out, shown rather than relied upon -------------------------------------------------------

    /// <summary>
    /// Documents that the analyzer returns immediately when the relational surface is absent from the
    /// compilation.
    /// </summary>
    /// <remarks>
    /// This arm is a green over a snippet that would otherwise fire, and it is the one arm here that proves
    /// nothing about the rule. It exists so a reader knows why every other arm carries the stub: without it,
    /// they would all be this arm wearing a different name.
    /// </remarks>
    [Fact]
    public async Task ReportNothing_WhenTheRelationalSurfaceIsAbsentFromTheCompilation()
    {
        const string source = """
            namespace Excalibur.Dispatch
            {
                public readonly struct TenantScope { }
            }

            namespace Sample
            {
                public sealed class LoadThing
                {
                    public LoadThing(Excalibur.Dispatch.TenantScope scope) { }
                }
            }
            """;

        await AnalyzerTestHarness
            .WithoutTenancySurface<TenantPartitionParameterAnalyzer>(source)
            .RunAsync();
    }
}
