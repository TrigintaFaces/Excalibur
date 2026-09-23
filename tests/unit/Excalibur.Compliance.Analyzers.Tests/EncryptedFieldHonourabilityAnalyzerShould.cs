// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Xunit;

namespace Excalibur.Compliance.Analyzers.Tests;

/// <summary>
/// Locks for EXCMP001-004 -- an encryption annotation the framework cannot honour is a build diagnostic.
/// </summary>
/// <remarks>
/// The class this protects already shipped once: an annotation on a <c>string</c> property was skipped by
/// every encryption path and the value was stored as plaintext while the model claimed it was encrypted. The
/// runtime now refuses such a model, but only when it is first encrypted. These arms bind the same judgement
/// at build time, and the liveness arm binds the other half: a model the framework CAN honour builds clean,
/// because an analyzer that flagged everything would satisfy every safety arm here.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptedFieldHonourabilityAnalyzerShould
{
    [Fact]
    public async Task Flag_AnEncryptedFieldOnATypeThatCannotCarryCiphertext()
    {
        const string source = """
            using Excalibur.Compliance;

            namespace Sample
            {
                public sealed class Account
                {
                    [{|#0:EncryptedField|}]
                    public int Pin { get; set; }
                }
            }
            """;

        var test = AnalyzerTestHarness.For(source);
        test.ExpectedDiagnostics.Add(AnalyzerTestHarness.Expect(
            AnalyzerTestHarness.UnsupportedPropertyType, 0, "[EncryptedField]", "Account.Pin", "int"));

        await test.RunAsync();
    }

    /// <summary>[Sensitive] selects for encryption exactly as [EncryptedField] does, and is named as written.</summary>
    [Fact]
    public async Task Flag_ASensitiveAnnotationOnATypeThatCannotCarryCiphertext()
    {
        const string source = """
            using Excalibur.Compliance;

            namespace Sample
            {
                public sealed class Integration
                {
                    [{|#0:Sensitive|}]
                    public System.Guid ApiKey { get; set; }
                }
            }
            """;

        var test = AnalyzerTestHarness.For(source);
        test.ExpectedDiagnostics.Add(AnalyzerTestHarness.Expect(
            AnalyzerTestHarness.UnsupportedPropertyType, 0, "[Sensitive]", "Integration.ApiKey", "System.Guid"));

        await test.RunAsync();
    }

    [Fact]
    public async Task Flag_AnEncryptedFieldOnAGetOnlyProperty()
    {
        const string source = """
            using Excalibur.Compliance;

            namespace Sample
            {
                public sealed class Customer
                {
                    [{|#0:EncryptedField|}]
                    public string Ssn { get; } = "";
                }
            }
            """;

        var test = AnalyzerTestHarness.For(source);
        test.ExpectedDiagnostics.Add(AnalyzerTestHarness.Expect(
            AnalyzerTestHarness.MissingAccessor, 0, "[EncryptedField]", "Customer.Ssn"));

        await test.RunAsync();
    }

    /// <summary>
    /// The silent case: the runtime never inspects a static or non-public property, so nothing at run time
    /// reports the annotation. This rule is the only place it is ever surfaced.
    /// </summary>
    [Fact]
    public async Task Flag_AnEncryptedFieldTheFrameworkNeverInspects()
    {
        const string source = """
            using Excalibur.Compliance;

            namespace Sample
            {
                public sealed class Settings
                {
                    [{|#0:EncryptedField|}]
                    public static string SharedSecret { get; set; } = "";

                    [{|#1:EncryptedField|}]
                    internal string ClientSecret { get; set; } = "";
                }
            }
            """;

        var test = AnalyzerTestHarness.For(source);
        test.ExpectedDiagnostics.Add(AnalyzerTestHarness.Expect(
            AnalyzerTestHarness.NotInspected, 0, "[EncryptedField]", "Settings.SharedSecret", "static"));
        test.ExpectedDiagnostics.Add(AnalyzerTestHarness.Expect(
            AnalyzerTestHarness.NotInspected, 1, "[EncryptedField]", "Settings.ClientSecret", "not public"));

        await test.RunAsync();
    }

    [Fact]
    public async Task Flag_ErasablePersonalDataEncryptedUnderASurvivingPurpose()
    {
        const string source = """
            using Excalibur.Compliance;

            namespace Sample
            {
                public sealed class Patient
                {
                    [PersonalData]
                    [{|#0:EncryptedField(Purpose = "billing")|}]
                    public string Name { get; set; } = "";
                }
            }
            """;

        var test = AnalyzerTestHarness.For(source);
        test.ExpectedDiagnostics.Add(AnalyzerTestHarness.Expect(
            AnalyzerTestHarness.ErasableAndSurvivingPurpose, 0, "Patient.Name", "billing"));

        await test.RunAsync();
    }

    /// <summary>
    /// The runtime reports the first problem a property has and moves on; so does the analyzer, so a
    /// consumer fixes the problem the framework would actually have refused on, not a list of three.
    /// </summary>
    [Fact]
    public async Task ReportOnlyTheFirstProblem_InTheOrderTheRuntimeChecks()
    {
        const string source = """
            using Excalibur.Compliance;

            namespace Sample
            {
                public sealed class Record
                {
                    [PersonalData]
                    [{|#0:EncryptedField(Purpose = "archive")|}]
                    public int Code { get; }
                }
            }
            """;

        var test = AnalyzerTestHarness.For(source);
        test.ExpectedDiagnostics.Add(AnalyzerTestHarness.Expect(
            AnalyzerTestHarness.ErasableAndSurvivingPurpose, 0, "Record.Code", "archive"));

        await test.RunAsync();
    }

    /// <summary>
    /// LIVENESS. Every shape the framework honours builds clean. Without this arm, an analyzer that flagged
    /// every annotation would pass every safety arm above.
    /// </summary>
    [Fact]
    public async Task StaySilent_OnAnnotationsTheFrameworkCanHonour()
    {
        const string source = """
            using Excalibur.Compliance;

            namespace Sample
            {
                public sealed class PaymentSettings
                {
                    [EncryptedField]
                    public string ApiKey { get; set; } = "";

                    [EncryptedField(Purpose = "webhooks")]
                    public byte[] SigningSecret { get; init; } = new byte[0];

                    [Sensitive]
                    public string? Password { get; set; }

                    [PersonalData]
                    [EncryptedField]
                    public string Email { get; private set; } = "";
                }

                public sealed record Customer(
                    string Id,
                    [property: EncryptedField] string Ssn);

                public sealed class Unannotated
                {
                    public int Count { get; }
                }
            }
            """;

        await AnalyzerTestHarness.For(source).RunAsync();
    }

    /// <summary>
    /// Documents the bail-out rather than relying on it: when the annotations are not referenced the analyzer
    /// does nothing. A "reports nothing" arm written without the stub would pass for that reason alone.
    /// </summary>
    [Fact]
    public async Task DoNothing_WhenTheAnnotationsAreNotReferenced()
    {
        const string source = """
            namespace Sample
            {
                public sealed class Plain
                {
                    public int Pin { get; set; }
                }
            }
            """;

        await AnalyzerTestHarness.WithoutAnnotationSurface(source).RunAsync();
    }
}
