// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Xunit;

namespace Excalibur.Data.Analyzers.Tests;

/// <summary>
/// Locks for EXDATA001 — a declared absence of a tenant term must state its justification.
/// </summary>
/// <remarks>
/// The guarantee being preserved moved layers rather than being invented. The factory this attribute replaces
/// called <c>ThrowIfNullOrWhiteSpace</c> on its reason at run time; an attribute argument is a compile-time
/// constant that no constructor gets to reject, so without this rule the check would have been lost silently
/// in the migration. It was already only nominally alive: nothing ever read the property that would have run
/// the factory, so an empty reason shipped and threw nowhere.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class NoTenantTermJustificationAnalyzerShould
{
    [Fact]
    public async Task Flag_AnEmptyJustification()
    {
        const string source = """
            using Excalibur.Data;

            namespace Sample
            {
                [{|#0:NoTenantTerm(TenantConfinement.EstateWide, "")|}]
                public sealed class GetStatistics : DataRequestBase<object, int>
                {
                }
            }
            """;

        var test = AnalyzerTestHarness.For<NoTenantTermJustificationAnalyzer>(source);
        test.ExpectedDiagnostics.Add(
            AnalyzerTestHarness.Expect(
                AnalyzerTestHarness.JustificationIsEmpty,
                0,
                "GetStatistics",
                "TenantConfinement.EstateWide"));

        await test.RunAsync();
    }

    /// <summary>
    /// Whitespace is the interesting case: it is non-null and non-empty, so a naive null-or-empty check passes
    /// it, and it reads in a diff as though something were written.
    /// </summary>
    [Fact]
    public async Task Flag_AWhitespaceOnlyJustification()
    {
        const string source = """
            using Excalibur.Data;

            namespace Sample
            {
                [{|#0:NoTenantTerm(TenantConfinement.IdentityAddressed, "   ")|}]
                public sealed class MarkMessageSent : DataRequestBase<object, int>
                {
                }
            }
            """;

        var test = AnalyzerTestHarness.For<NoTenantTermJustificationAnalyzer>(source);
        test.ExpectedDiagnostics.Add(
            AnalyzerTestHarness.Expect(
                AnalyzerTestHarness.JustificationIsEmpty,
                0,
                "MarkMessageSent",
                "TenantConfinement.IdentityAddressed"));

        await test.RunAsync();
    }

    /// <summary>
    /// The attribute allows multiples, because a statement can carry two confinement arguments for two
    /// statements. Each application is judged on its own.
    /// </summary>
    [Fact]
    public async Task Flag_OnlyTheEmptyOne_WhenATypeCarriesTwoDeclarations()
    {
        const string source = """
            using Excalibur.Data;

            namespace Sample
            {
                [NoTenantTerm(TenantConfinement.ForeignKeyConfined, "reached through a foreign key to a unique id")]
                [{|#0:NoTenantTerm(TenantConfinement.IdentityAddressed, "")|}]
                public sealed class UpdateAggregateStatus : DataRequestBase<object, int>
                {
                }
            }
            """;

        var test = AnalyzerTestHarness.For<NoTenantTermJustificationAnalyzer>(source);
        test.ExpectedDiagnostics.Add(
            AnalyzerTestHarness.Expect(
                AnalyzerTestHarness.JustificationIsEmpty,
                0,
                "UpdateAggregateStatus",
                "TenantConfinement.IdentityAddressed"));

        await test.RunAsync();
    }

    [Fact]
    public async Task StaySilent_ForAStatedJustification()
    {
        const string source = """
            using Excalibur.Data;

            namespace Sample
            {
                [NoTenantTerm(TenantConfinement.EstateWide, "operator statistics; the caller is not a tenant")]
                public sealed class GetStatistics : DataRequestBase<object, int>
                {
                }
            }
            """;

        await AnalyzerTestHarness.ShouldReportNothingAsync<NoTenantTermJustificationAnalyzer>(source);
    }

    /// <summary>
    /// A request carrying no declaration at all is the ordinary case and is not a subject of this rule.
    /// </summary>
    [Fact]
    public async Task StaySilent_ForARequestWithNoDeclaration()
    {
        const string source = """
            using Excalibur.Data;

            namespace Sample
            {
                public sealed class LoadThing : DataRequestBase<object, string>
                {
                }
            }
            """;

        await AnalyzerTestHarness.ShouldReportNothingAsync<NoTenantTermJustificationAnalyzer>(source);
    }

    /// <summary>
    /// An unrelated attribute with an empty string argument must not be mistaken for this one. The rule
    /// compares the attribute's symbol, not its name.
    /// </summary>
    [Fact]
    public async Task StaySilent_ForAnUnrelatedAttributeWithAnEmptyStringArgument()
    {
        const string source = """
            using Excalibur.Data;

            namespace Sample
            {
                [System.AttributeUsage(System.AttributeTargets.Class)]
                public sealed class NoteAttribute : System.Attribute
                {
                    public NoteAttribute(TenantConfinement confinement, string text) { }
                }

                [Note(TenantConfinement.EstateWide, "")]
                public sealed class LoadThing : DataRequestBase<object, string>
                {
                }
            }
            """;

        await AnalyzerTestHarness.ShouldReportNothingAsync<NoTenantTermJustificationAnalyzer>(source);
    }

    /// <summary>
    /// Documents the bail-out, so the stub in every other arm is understood as load-bearing rather than
    /// decorative. This arm proves nothing about the rule itself.
    /// </summary>
    [Fact]
    public async Task ReportNothing_WhenTheAttributeIsAbsentFromTheCompilation()
    {
        const string source = """
            namespace Excalibur.Data
            {
                [System.AttributeUsage(System.AttributeTargets.Class)]
                public sealed class SomethingElseAttribute : System.Attribute
                {
                    public SomethingElseAttribute(string text) { }
                }

                [SomethingElse("")]
                public sealed class LoadThing
                {
                }
            }
            """;

        await AnalyzerTestHarness
            .WithoutTenancySurface<NoTenantTermJustificationAnalyzer>(source)
            .RunAsync();
    }
}
