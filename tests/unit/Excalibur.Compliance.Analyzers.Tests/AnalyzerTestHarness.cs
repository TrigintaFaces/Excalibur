// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Excalibur.Compliance.Analyzers.Tests;

/// <summary>
/// Shared harness for the field-encryption annotation analyzer.
/// </summary>
/// <remarks>
/// <para>
/// <b>The stub is not scenery.</b> The analyzer resolves the annotations by metadata name at compilation start
/// and returns immediately when they are absent, so a snippet without <see cref="AnnotationSurfaceStub"/> is a
/// green that consulted no analyzer at all. Every arm includes the stub, and one arm shows the bail-out
/// explicitly so nobody mistakes it for coverage.
/// </para>
/// <para>
/// Diagnostic IDs are string literals on purpose: the descriptors are internal, but the ID is what a consumer
/// pins in an <c>.editorconfig</c> and reads in a build log, so the ID is the contract these locks bind.
/// </para>
/// </remarks>
internal static class AnalyzerTestHarness
{
    public const string UnsupportedPropertyType = "EXCMP001";
    public const string MissingAccessor = "EXCMP002";
    public const string NotInspected = "EXCMP003";
    public const string ErasableAndSurvivingPurpose = "EXCMP004";

    /// <summary>The three annotations the analyzer resolves, in their real namespace.</summary>
    public const string AnnotationSurfaceStub = """
        namespace Excalibur.Compliance
        {
            [System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
            public sealed class EncryptedFieldAttribute : System.Attribute
            {
                public string? Purpose { get; set; }
                public bool RequireFipsCompliance { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
            public sealed class SensitiveAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
            public sealed class PersonalDataAttribute : System.Attribute { }
        }
        """;

    public static ReferenceAssemblies ReferenceAssemblies => Microsoft.CodeAnalysis.Testing.ReferenceAssemblies.Net.Net90;

    /// <summary>Builds a test over the stub plus the supplied sources.</summary>
    public static CSharpAnalyzerTest<EncryptedFieldHonourabilityAnalyzer, DefaultVerifier> For(params string[] sources)
    {
        var test = WithoutAnnotationSurface(sources);
        test.TestState.Sources.Add(("AnnotationSurface.cs", AnnotationSurfaceStub));
        return test;
    }

    /// <summary>Builds a test WITHOUT the stub, so the bail-out path can be shown rather than relied upon.</summary>
    public static CSharpAnalyzerTest<EncryptedFieldHonourabilityAnalyzer, DefaultVerifier> WithoutAnnotationSurface(params string[] sources)
    {
        var test = new CSharpAnalyzerTest<EncryptedFieldHonourabilityAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies,
        };

        foreach (var source in sources)
        {
            test.TestState.Sources.Add(source);
        }

        return test;
    }

    public static DiagnosticResult Expect(string id, int location, params string[] arguments)
        => new DiagnosticResult(id, DiagnosticSeverity.Warning)
            .WithLocation(location)
            .WithArguments(arguments);
}
