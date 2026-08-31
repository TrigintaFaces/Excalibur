// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Excalibur.Data.Analyzers.Tests;

/// <summary>
/// Shared harness for the relational tenancy analyzers.
/// </summary>
/// <remarks>
/// <para>
/// <b>The stub is not scenery.</b> Both analyzers resolve their subjects by metadata name at compilation
/// start and return immediately when the type is absent. A snippet that omits <see cref="TenancySurfaceStub"/>
/// therefore produces a green that consulted no analyzer at all, and a "reports nothing" arm written over such
/// a snippet is vacuous however carefully it is phrased. Every arm in this assembly includes the stub, and one
/// arm documents the bail-out explicitly so nobody mistakes it for coverage.
/// </para>
/// <para>
/// Diagnostic IDs are referenced as string literals on purpose. The descriptors are <c>internal</c> to the
/// analyzer assembly, but the ID is the part a consumer pins in their <c>.editorconfig</c> and reads in their
/// build log, so the ID string is the contract these locks are meant to bind.
/// </para>
/// </remarks>
internal static class AnalyzerTestHarness
{
    public const string JustificationIsEmpty = "EXDATA001";
    public const string TenantPartitionParameterIsDiscarded = "EXDATA002";

    /// <summary>
    /// The minimum surface both analyzers resolve: the relational request base, the two partition-valued
    /// types, the confinement vocabulary, and the attribute.
    /// </summary>
    /// <remarks>
    /// <c>ITenantContext</c> is included although no analyzer resolves it, because one arm's whole purpose is
    /// to show that an unused context parameter is deliberately <em>not</em> a subject.
    /// </remarks>
    public const string TenancySurfaceStub = """
        namespace Excalibur.Dispatch
        {
            public interface ITenantContext { string TenantId { get; } }

            public readonly struct TenantScope
            {
                public string TenantId => "t";
            }

            public sealed class KeyedTenantPartition
            {
                public string TenantId => "t";
            }
        }

        namespace Excalibur.Data
        {
            public abstract class DataRequestBase<TConnection, TModel>
            {
                protected DataRequestBase() { }

                // Binds the scope rather than swallowing it. An earlier version of this stub took the
                // parameter and dropped it, which is precisely what EXDATA002 reports -- so the stub
                // emitted a diagnostic of its own into every fixture that included it, and every
                // StaySilent_ arm failed on a diagnostic none of them had authored. The analyzer was
                // right about the fixture; a base that discards a partition is the worst instance of
                // the defect, because every derived type inherits the lie.
                protected DataRequestBase(Excalibur.Dispatch.TenantScope scope)
                {
                    Partition = scope;
                }

                protected Excalibur.Dispatch.TenantScope Partition { get; }
            }

            public abstract class DataRequest<TModel> : DataRequestBase<object, TModel> { }

            public enum TenantConfinement
            {
                EstateWide,
                IdentityAddressed,
                ForeignKeyConfined,
                NoTenantDimension,
            }

            [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
            public sealed class NoTenantTermAttribute : System.Attribute
            {
                public NoTenantTermAttribute(TenantConfinement confinement, string justification)
                {
                    Confinement = confinement;
                    Justification = justification;
                }

                public TenantConfinement Confinement { get; }

                public string Justification { get; }
            }
        }
        """;

    public static ReferenceAssemblies ReferenceAssemblies => Microsoft.CodeAnalysis.Testing.ReferenceAssemblies.Net.Net90;

    /// <summary>Builds a single-analyzer test over the stub plus the supplied sources.</summary>
    public static CSharpAnalyzerTest<TAnalyzer, DefaultVerifier> For<TAnalyzer>(params string[] sources)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies,
        };

        test.TestState.Sources.Add(TenancySurfaceStub);

        foreach (var source in sources)
        {
            test.TestState.Sources.Add(source);
        }

        return test;
    }

    /// <summary>
    /// Builds a test over the supplied sources WITHOUT the stub, so the bail-out path can be shown rather than
    /// relied upon.
    /// </summary>
    public static CSharpAnalyzerTest<TAnalyzer, DefaultVerifier> WithoutTenancySurface<TAnalyzer>(params string[] sources)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies,
        };

        foreach (var source in sources)
        {
            test.TestState.Sources.Add(source);
        }

        return test;
    }

    /// <summary>
    /// Asserts the analyzer reports nothing for the supplied sources — the liveness half of every lock here.
    /// A guard asserted only on its safety half is satisfied by an analyzer that has been accidentally
    /// disabled, mis-registered, or short-circuited to a no-op.
    /// </summary>
    public static Task ShouldReportNothingAsync<TAnalyzer>(params string[] sources)
        where TAnalyzer : DiagnosticAnalyzer, new()
        => For<TAnalyzer>(sources).RunAsync();

    public static DiagnosticResult Expect(string id, int location, params string[] arguments)
        => new DiagnosticResult(id, DiagnosticSeverity.Warning)
            .WithLocation(location)
            .WithArguments(arguments);
}
