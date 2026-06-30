// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Migration.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace Excalibur.Dispatch.Migration.Tests;

/// <summary>
/// bd-aph3ra — lock for AC-9 (diagnostic side): EXMIG0001 is reported at the
/// <c>AddMediatR</c> call so a migrating consumer is pointed at the portable registration.
/// Non-vacuous: the verifier asserts the exact diagnostic at the exact span — RED if the
/// analyzer stops flagging <c>AddMediatR</c>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compat")]
public sealed class AddMediatRRegistrationAnalyzerShould
{
    [Fact] // AC-9: EXMIG0001 reported at the AddMediatR invocation identifier.
    public async Task ReportEXMIG0001_AtTheAddMediatRCall()
    {
        const string source = """
            namespace Microsoft.Extensions.DependencyInjection
            {
                public interface IServiceCollection { }

                public static class ConsumerStartup
                {
                    public static void Configure(IServiceCollection services)
                    {
                        {|#0:AddMediatR|}(services);
                    }

                    private static void AddMediatR(IServiceCollection services) { }
                }
            }
            """;

        var test = new CSharpAnalyzerTest<AddMediatRRegistrationAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };
        // Reference by ID + severity (descriptors are internal); EXMIG0001 = Info.
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("EXMIG0001", DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("AddMediatR"));

        await test.RunAsync();
    }

    [Fact] // No false positive on an unrelated method name.
    public async Task NotReport_WhenNoAddMediatRCall()
    {
        const string source = """
            namespace Microsoft.Extensions.DependencyInjection
            {
                public interface IServiceCollection { }

                public static class ConsumerStartup
                {
                    public static void Configure(IServiceCollection services)
                    {
                        AddDispatch(services);
                    }

                    private static void AddDispatch(IServiceCollection services) { }
                }
            }
            """;

        var test = new CSharpAnalyzerTest<AddMediatRRegistrationAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }
}
