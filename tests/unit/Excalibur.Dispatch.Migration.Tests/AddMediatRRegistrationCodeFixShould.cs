// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Migration.Analyzers;
using Excalibur.Dispatch.Migration.CodeFixes;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace Excalibur.Dispatch.Migration.Tests;

/// <summary>
/// bd-6t7eqm — lock for AC-9 (FR-10/FR-11): the EXMIG0001 code-fix rewrites <c>AddMediatR(...)</c>
/// to <c>AddMediatRCompat(...)</c> while **preserving the call's arguments** (e.g. the assembly /
/// scan lambda). Non-vacuous: asserts the diagnostic span AND that the fixed call keeps its args.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compat")]
public sealed class AddMediatRRegistrationCodeFixShould
{
    // Stubs for both registration entry points so before/after both compile (analyzer + fix are
    // purely syntactic — name match + identifier rename).
    private const string RegistrationStubs = """
        using System.Reflection;

        namespace Microsoft.Extensions.DependencyInjection
        {
            public interface IServiceCollection { }

            public static class Reg
            {
                public static IServiceCollection AddMediatR(this IServiceCollection services, Assembly assembly) => services;
                public static IServiceCollection AddMediatRCompat(this IServiceCollection services, Assembly assembly) => services;
            }
        }
        """;

    [Fact] // AC-9: AddMediatR → AddMediatRCompat, assembly argument preserved.
    public async Task RewriteToAddMediatRCompat_PreservingTheAssemblyArgument()
    {
        const string source = """
            using System.Reflection;
            using Microsoft.Extensions.DependencyInjection;

            public static class Startup
            {
                public static void Configure(IServiceCollection services, Assembly asm)
                {
                    services.{|#0:AddMediatR|}(asm);
                }
            }
            """;

        const string fixedSource = """
            using System.Reflection;
            using Microsoft.Extensions.DependencyInjection;

            public static class Startup
            {
                public static void Configure(IServiceCollection services, Assembly asm)
                {
                    services.AddMediatRCompat(asm);
                }
            }
            """;

        var test = new CSharpCodeFixTest<AddMediatRRegistrationAnalyzer, AddMediatRRegistrationCodeFixProvider, DefaultVerifier>
        {
            TestState = { Sources = { RegistrationStubs, source } },
            FixedState = { Sources = { RegistrationStubs, fixedSource } },
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("EXMIG0001", DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("AddMediatR"));

        await test.RunAsync();
    }
}
